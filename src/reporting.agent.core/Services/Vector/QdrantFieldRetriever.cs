using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using reporting.agent.core.Configuration;
using reporting.agent.core.Models;
using reporting.agent.core.Services.Taxonomy;

namespace reporting.agent.core.Services.Vector;

/// Semantic field retrieval over the reporting_columns Qdrant collection.
/// Used when FieldRetrieval:Mode = Vector. get_field_tags falls back to the
/// taxonomy index because tag counts are a property of the JSON schema,
/// not the embedding space.
public sealed class QdrantFieldRetriever : IFieldRetriever
{
    private readonly QdrantClient _client;
    private readonly IEmbeddingService _embeddings;
    private readonly TaxonomyRetriever _taxonomyFallback;
    private readonly QdrantOptions _options;
    private readonly ILogger<QdrantFieldRetriever> _logger;

    public QdrantFieldRetriever(
        QdrantClient client,
        IEmbeddingService embeddings,
        TaxonomyRetriever taxonomyFallback,
        IOptions<QdrantOptions> options,
        ILogger<QdrantFieldRetriever> logger)
    {
        _client = client;
        _embeddings = embeddings;
        _taxonomyFallback = taxonomyFallback;
        _options = options.Value;
        _logger = logger;
    }

    public Task<IReadOnlyDictionary<string, int>> GetTagsForUniverseAsync(
        string universe, CancellationToken ct = default) =>
        _taxonomyFallback.GetTagsForUniverseAsync(universe, ct);

    public async Task<IReadOnlyList<FieldResult>> RetrieveAsync(
        IReadOnlyList<string> universes,
        IReadOnlyList<string> tags,
        int topK,
        CancellationToken ct = default)
    {
        var query = BuildQueryText(universes, tags);
        var embedding = await _embeddings.EmbedAsync(query, ct);

        var filter = BuildUniverseFilter(universes);

        var hits = await _client.QueryAsync(
            collectionName: _options.ReportingColumnsCollection,
            query: embedding.ToArray(),
            filter: filter,
            limit: (ulong)Math.Max(1, topK),
            payloadSelector: true,
            cancellationToken: ct);

        var results = new List<FieldResult>(hits.Count);
        foreach (var hit in hits)
        {
            var metadata = hit.Payload.TryGetValue("metadata", out var m) && m.KindCase == Value.KindOneofCase.StructValue
                ? m.StructValue.Fields
                : hit.Payload;

            results.Add(new FieldResult
            {
                ColumnId = ReadString(metadata, "column_id"),
                ColumnName = ReadString(metadata, "column_name"),
                Universe = ReadString(metadata, "universe"),
                DataType = ReadString(metadata, "data_type"),
                Description = ReadString(metadata, "description"),
                Tags = ReadList(metadata, "tags") ?? new(),
                LinkedIds = ReadList(metadata, "linked_ids") ?? new(),
                Role = "matched",
                Score = Math.Round(hit.Score, 4),
            });
        }

        _logger.LogInformation("Qdrant returned {Count} matches for query '{Query}'", results.Count, query);
        return results;
    }

    private static string BuildQueryText(IReadOnlyList<string> universes, IReadOnlyList<string> tags) =>
        $"universes: {string.Join(", ", universes)}; tags: {string.Join(", ", tags)}";

    private static Filter? BuildUniverseFilter(IReadOnlyList<string> universes)
    {
        if (universes.Count == 0) return null;
        var conditions = universes.Select(u => new Condition
        {
            Field = new FieldCondition
            {
                Key = "metadata.universe",
                Match = new Match { Keyword = u },
            },
        });

        var filter = new Filter();
        filter.Should.AddRange(conditions);
        return filter;
    }

    private static string ReadString(IReadOnlyDictionary<string, Value> map, string key) =>
        map.TryGetValue(key, out var v) && v.KindCase == Value.KindOneofCase.StringValue
            ? v.StringValue
            : "";

    private static List<string>? ReadList(IReadOnlyDictionary<string, Value> map, string key)
    {
        if (!map.TryGetValue(key, out var v)) return null;
        if (v.KindCase == Value.KindOneofCase.ListValue)
        {
            return v.ListValue.Values
                .Where(x => x.KindCase == Value.KindOneofCase.StringValue)
                .Select(x => x.StringValue)
                .ToList();
        }
        return null;
    }
}

