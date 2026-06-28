using System.Text.Json;
using Microsoft.Extensions.Options;
using reporting.agent.core.Configuration;
using reporting.agent.core.Models;

namespace reporting.agent.core.Services.Sql;

/// Singleton that loads and caches column metadata for SQL validation.
/// Mirrors services/sql/column_registry.py.
public sealed class ColumnRegistry
{
    private static readonly HashSet<string> SystemColumns = new(StringComparer.Ordinal) { "__time" };

    public IReadOnlySet<string> ValidIds { get; }
    public IReadOnlyDictionary<string, string> ColumnTypes { get; }

    public ColumnRegistry(IOptions<FieldRetrievalOptions> options)
    {
        var dir = options.Value.ColumnsDirectory;
        if (!Path.IsPathRooted(dir))
        {
            dir = Path.Combine(AppContext.BaseDirectory, dir);
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var types = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var path in Directory.EnumerateFiles(dir, "*.json").OrderBy(p => p, StringComparer.Ordinal))
        {
            using var stream = File.OpenRead(path);
            var columns = JsonSerializer.Deserialize<List<ColumnDefinition>>(stream) ?? new();
            foreach (var col in columns)
            {
                ids.Add(col.Id);
                types[col.Id] = string.IsNullOrEmpty(col.DataType) ? "" : col.DataType;
            }
        }

        ValidIds = ids;
        ColumnTypes = types;
    }

    public bool IsValidColumn(string name) =>
        ValidIds.Contains(name) || SystemColumns.Contains(name);

    public string? GetDataType(string name) =>
        ColumnTypes.TryGetValue(name, out var t) ? t : null;
}

