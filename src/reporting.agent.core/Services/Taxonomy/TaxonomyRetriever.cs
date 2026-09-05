using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using reporting.agent.core.Configuration;
using reporting.agent.core.Models;

namespace reporting.agent.core.Services.Taxonomy;

/// Deterministic field retrieval via universe × tag filtering, mirroring the
/// Python TaxonomyRetriever. Indexes are rebuilt on Reload() so the column
/// management REST API can refresh after writes.
public sealed class TaxonomyRetriever : IFieldRetriever
{
    private readonly ILogger<TaxonomyRetriever> _logger;
    private readonly FieldRetrievalOptions _options;
    private readonly Lock _lock = new();

    private Dictionary<string, ColumnRecord> _byId = new();
    private Dictionary<string, List<ColumnRecord>> _byUniverse = new();
    private Dictionary<string, Dictionary<string, List<ColumnRecord>>> _tagIndex = new();

    public TaxonomyRetriever(IOptions<FieldRetrievalOptions> options, ILogger<TaxonomyRetriever> logger)
    {
        _options = options.Value;
        _logger = logger;
        Reload();
    }

    public void Reload()
    {
        var dir = _options.ColumnsDirectory;
        if (!Path.IsPathRooted(dir))
        {
            dir = Path.Combine(AppContext.BaseDirectory, dir);
        }

        var byId = new Dictionary<string, ColumnRecord>(StringComparer.Ordinal);
        var byUniverse = new Dictionary<string, List<ColumnRecord>>(StringComparer.Ordinal);
        var tagIndex = new Dictionary<string, Dictionary<string, List<ColumnRecord>>>(StringComparer.Ordinal);

        foreach (var path in Directory.EnumerateFiles(dir, "*.json").OrderBy(p => p, StringComparer.Ordinal))
        {
            var stem = Path.GetFileNameWithoutExtension(path);
            if (!UniverseMap.ByFileStem.TryGetValue(stem, out var universe))
            {
                _logger.LogWarning("Unknown column file {File}, skipping", path);
                continue;
            }

            var display = UniverseMap.DisplayName[universe];
            using var stream = File.OpenRead(path);
            var raw = JsonSerializer.Deserialize<List<ColumnDefinition>>(stream) ?? new();

            foreach (var col in raw)
            {
                var record = new ColumnRecord(
                    ColumnId: col.Id,
                    ColumnName: col.Name,
                    Universe: display,
                    DataType: string.IsNullOrEmpty(col.DataType) ? "String" : col.DataType,
                    Description: col.Description ?? "",
                    Category: col.Category ?? "",
                    Tags: col.Tags ?? new(),
                    LinkedIds: col.LinkedIds ?? new(),
                    AllowedValues: col.AllowedValues ?? new(),
                    Enabled: col.Enabled);

                byId[col.Id] = record;

                if (!byUniverse.TryGetValue(display, out var bucket))
                {
                    bucket = new List<ColumnRecord>();
                    byUniverse[display] = bucket;
                }
                bucket.Add(record);

                if (!tagIndex.TryGetValue(display, out var tagBucket))
                {
                    tagBucket = new Dictionary<string, List<ColumnRecord>>(StringComparer.Ordinal);
                    tagIndex[display] = tagBucket;
                }
                foreach (var tag in record.Tags)
                {
                    if (!tagBucket.TryGetValue(tag, out var list))
                    {
                        list = new List<ColumnRecord>();
                        tagBucket[tag] = list;
                    }
                    list.Add(record);
                }
            }
        }

        lock (_lock)
        {
            _byId = byId;
            _byUniverse = byUniverse;
            _tagIndex = tagIndex;
        }

        _logger.LogInformation("TaxonomyRetriever loaded {Fields} fields across {Universes} universes",
            byId.Count, byUniverse.Count);
    }

    public Task<IReadOnlyDictionary<string, int>> GetTagsForUniverseAsync(
        string universe, CancellationToken ct = default)
    {
        var normalized = Normalize(universe);
        var result = new Dictionary<string, int>(StringComparer.Ordinal);

        Dictionary<string, Dictionary<string, List<ColumnRecord>>> tagIndex;
        lock (_lock) tagIndex = _tagIndex;

        if (tagIndex.TryGetValue(normalized, out var tagBucket))
        {
            foreach (var (tag, columns) in tagBucket)
            {
                var enabledCount = columns.Count(c => c.Enabled);
                if (enabledCount > 0) result[tag] = enabledCount;
            }
        }

        var sorted = result.OrderByDescending(kv => kv.Value)
            .ToDictionary(kv => kv.Key, kv => kv.Value);
        return Task.FromResult<IReadOnlyDictionary<string, int>>(sorted);
    }

    public Task<IReadOnlyList<FieldResult>> RetrieveAsync(
        IReadOnlyList<string> universes,
        IReadOnlyList<string> tags,
        int topK,
        CancellationToken ct = default)
    {
        Dictionary<string, ColumnRecord> byId;
        Dictionary<string, Dictionary<string, List<ColumnRecord>>> tagIndex;
        lock (_lock)
        {
            byId = _byId;
            tagIndex = _tagIndex;
        }

        var normalized = universes.Select(Normalize).ToList();
        var matchedIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var u in normalized)
        {
            if (!tagIndex.TryGetValue(u, out var uniTags)) continue;
            foreach (var tag in tags)
            {
                if (uniTags.TryGetValue(tag, out var cols))
                {
                    foreach (var col in cols)
                    {
                        if (col.Enabled) matchedIds.Add(col.ColumnId);
                    }
                }
            }
        }

        var matched = matchedIds
            .Where(byId.ContainsKey)
            .Select(id => byId[id])
            .OrderBy(c => c.ColumnName, StringComparer.Ordinal)
            .Take(topK)
            .ToList();

        var matchedSet = matched.Select(c => c.ColumnId).ToHashSet(StringComparer.Ordinal);
        var depIds = ResolveLinkedIds(byId, matchedSet);
        depIds.ExceptWith(matchedSet);

        var dependencies = depIds
            .Where(byId.ContainsKey)
            .Select(id => byId[id])
            .OrderBy(c => c.ColumnName, StringComparer.Ordinal)
            .ToList();

        var results = new List<FieldResult>(matched.Count + dependencies.Count);
        foreach (var col in matched) results.Add(Format(col, "matched"));
        foreach (var col in dependencies) results.Add(Format(col, "dependency"));

        return Task.FromResult<IReadOnlyList<FieldResult>>(results);
    }

    private static HashSet<string> ResolveLinkedIds(
        IReadOnlyDictionary<string, ColumnRecord> byId,
        IReadOnlyCollection<string> seed)
    {
        var resolved = new HashSet<string>(seed, StringComparer.Ordinal);
        var queue = new Queue<string>(seed);
        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            if (!byId.TryGetValue(id, out var col)) continue;
            foreach (var linked in col.LinkedIds)
            {
                if (linked == id) continue;
                if (resolved.Add(linked)) queue.Enqueue(linked);
            }
        }
        return resolved;
    }

    private static FieldResult Format(ColumnRecord col, string role) => new()
    {
        ColumnId = col.ColumnId,
        ColumnName = col.ColumnName,
        Universe = col.Universe,
        DataType = col.DataType,
        Description = col.Description,
        Tags = col.Tags,
        LinkedIds = col.LinkedIds,
        Role = role,
        AllowedValues = col.AllowedValues.Count > 0 ? col.AllowedValues : null,
    };

    private string Normalize(string universe)
    {
        Dictionary<string, List<ColumnRecord>> byUniverse;
        lock (_lock) byUniverse = _byUniverse;

        if (byUniverse.ContainsKey(universe)) return universe;
        if (UniverseMap.TryParse(universe, out var parsed))
        {
            return UniverseMap.DisplayName[parsed];
        }
        return universe;
    }

    private sealed record ColumnRecord(
        string ColumnId,
        string ColumnName,
        string Universe,
        string DataType,
        string Description,
        string Category,
        List<string> Tags,
        List<string> LinkedIds,
        List<string> AllowedValues,
        bool Enabled);
}

