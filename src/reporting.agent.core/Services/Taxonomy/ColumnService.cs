using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using reporting.agent.core.Configuration;
using reporting.agent.core.Models;

namespace reporting.agent.core.Services.Taxonomy;

/// CRUD over the resources/columns/*.json files with MD5 ETag-based
/// optimistic concurrency. Mirrors services/column_service.py.
public sealed class ColumnService
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly FieldRetrievalOptions _options;
    private readonly ILogger<ColumnService> _logger;

    public ColumnService(IOptions<FieldRetrievalOptions> options, ILogger<ColumnService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    private string ColumnsDir
    {
        get
        {
            var dir = _options.ColumnsDirectory;
            return Path.IsPathRooted(dir) ? dir : Path.Combine(AppContext.BaseDirectory, dir);
        }
    }

    private string FilePath(string universeKey)
    {
        if (!UniverseMap.ByFileStem.ContainsKey(universeKey))
        {
            throw new ArgumentException($"Unknown universe: {universeKey}");
        }
        return Path.Combine(ColumnsDir, $"{universeKey}.json");
    }

    private static string ComputeEtag(byte[] data) =>
        Convert.ToHexString(MD5.HashData(data)).ToLowerInvariant();

    private (List<ColumnDefinition> Columns, string Etag) Read(string universeKey)
    {
        var path = FilePath(universeKey);
        var raw = File.ReadAllBytes(path);
        var columns = JsonSerializer.Deserialize<List<ColumnDefinition>>(raw) ?? new();
        return (columns, ComputeEtag(raw));
    }

    private string Write(string universeKey, List<ColumnDefinition> columns)
    {
        var path = FilePath(universeKey);
        var raw = JsonSerializer.SerializeToUtf8Bytes(columns, WriteOptions);
        File.WriteAllBytes(path, raw);
        return ComputeEtag(raw);
    }

    public List<UniverseSummary> GetUniverses()
    {
        var result = new List<UniverseSummary>();
        foreach (var (universe, key) in UniverseMap.FileStem.OrderBy(kv => kv.Value, StringComparer.Ordinal))
        {
            var path = Path.Combine(ColumnsDir, $"{key}.json");
            if (!File.Exists(path)) continue;

            var raw = File.ReadAllBytes(path);
            var columns = JsonSerializer.Deserialize<List<ColumnDefinition>>(raw) ?? new();

            result.Add(new UniverseSummary
            {
                Key = key,
                Name = UniverseMap.DisplayName[universe],
                Total = columns.Count,
                Enabled = columns.Count(c => c.Enabled),
            });
        }
        return result;
    }

    public (List<ColumnDefinition> Columns, string Etag) GetColumns(
        string universeKey,
        string? search = null,
        string? category = null,
        string? dataType = null,
        string? tag = null,
        bool? enabled = null)
    {
        var (columns, etag) = Read(universeKey);
        IEnumerable<ColumnDefinition> filtered = columns;

        if (!string.IsNullOrEmpty(search))
        {
            var s = search.ToLowerInvariant();
            filtered = filtered.Where(c =>
                c.Name.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                c.Id.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                (c.Description ?? "").Contains(s, StringComparison.OrdinalIgnoreCase));
        }
        if (!string.IsNullOrEmpty(category))
        {
            filtered = filtered.Where(c => c.Category == category);
        }
        if (!string.IsNullOrEmpty(dataType))
        {
            filtered = filtered.Where(c => c.DataType == dataType);
        }
        if (!string.IsNullOrEmpty(tag))
        {
            filtered = filtered.Where(c => c.Tags.Contains(tag));
        }
        if (enabled.HasValue)
        {
            filtered = filtered.Where(c => c.Enabled == enabled.Value);
        }

        return (filtered.ToList(), etag);
    }

    public IReadOnlyDictionary<string, int> GetTags(string universeKey)
    {
        var (columns, _) = Read(universeKey);
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var col in columns)
        {
            foreach (var t in col.Tags)
            {
                counts[t] = counts.GetValueOrDefault(t) + 1;
            }
        }
        return counts.OrderByDescending(kv => kv.Value)
            .ToDictionary(kv => kv.Key, kv => kv.Value);
    }

    public (int UpdatedCount, string NewEtag) UpdateColumns(
        string universeKey,
        IReadOnlyList<ColumnUpdate> updates,
        string expectedEtag)
    {
        var (columns, currentEtag) = Read(universeKey);
        if (currentEtag != expectedEtag)
        {
            throw new ConflictException(
                $"File was modified by another user. Expected ETag {expectedEtag}, current is {currentEtag}");
        }

        var byId = columns.ToDictionary(c => c.Id, c => c);
        var changed = 0;

        foreach (var update in updates)
        {
            if (string.IsNullOrEmpty(update.Id) || !byId.TryGetValue(update.Id, out var col))
            {
                _logger.LogWarning("Column ID not found for update: {Id}", update.Id);
                continue;
            }

            var modified = false;
            if (update.Enabled.HasValue && update.Enabled.Value != col.Enabled)
            {
                col.Enabled = update.Enabled.Value;
                modified = true;
            }
            if (update.Tags is not null && !TagsEqual(update.Tags, col.Tags))
            {
                col.Tags = update.Tags;
                modified = true;
            }
            if (update.Description is not null && update.Description != col.Description)
            {
                col.Description = update.Description;
                modified = true;
            }
            if (update.Category is not null && update.Category != col.Category)
            {
                col.Category = update.Category;
                modified = true;
            }
            if (update.AllowedValues is not null && !TagsEqual(update.AllowedValues, col.AllowedValues ?? new()))
            {
                col.AllowedValues = update.AllowedValues;
                modified = true;
            }

            if (modified) changed++;
        }

        var newEtag = Write(universeKey, columns);
        return (changed, newEtag);
    }

    public (ColumnDefinition Created, string NewEtag) AddColumn(
        string universeKey,
        ColumnDefinition column,
        string expectedEtag)
    {
        if (string.IsNullOrEmpty(column.Id))
        {
            throw new ArgumentException("Column 'id' is required");
        }
        if (string.IsNullOrEmpty(column.Name))
        {
            throw new ArgumentException("Column 'name' is required");
        }

        var (columns, currentEtag) = Read(universeKey);
        if (currentEtag != expectedEtag)
        {
            throw new ConflictException(
                $"File was modified by another user. Expected ETag {expectedEtag}, current is {currentEtag}");
        }

        if (columns.Any(c => c.Id == column.Id))
        {
            throw new DuplicateException($"Column with id '{column.Id}' already exists");
        }

        var maxOrder = columns.Count == 0 ? 0 : columns.Max(c => c.DefaultOrder);
        var newColumn = new ColumnDefinition
        {
            Id = column.Id,
            Name = column.Name,
            Enabled = column.Enabled,
            Category = string.IsNullOrEmpty(column.Category) ? "Other" : column.Category,
            DataType = string.IsNullOrEmpty(column.DataType) ? "String" : column.DataType,
            Description = column.Description ?? "",
            DefaultOrder = column.DefaultOrder == 0 ? maxOrder + 1 : column.DefaultOrder,
            Tags = column.Tags ?? new(),
            LinkedIds = column.LinkedIds,
            AllowedValues = column.AllowedValues,
        };

        columns.Add(newColumn);
        var newEtag = Write(universeKey, columns);
        return (newColumn, newEtag);
    }

    private static bool TagsEqual(List<string> a, List<string> b)
    {
        if (a.Count != b.Count) return false;
        for (var i = 0; i < a.Count; i++)
        {
            if (a[i] != b[i]) return false;
        }
        return true;
    }
}

