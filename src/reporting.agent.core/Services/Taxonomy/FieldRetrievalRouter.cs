using Microsoft.Extensions.Options;
using reporting.agent.core.Configuration;
using reporting.agent.core.Models;

namespace reporting.agent.core.Services.Taxonomy;

/// Picks the active IFieldRetriever per call based on FieldRetrievalOptions.Mode.
/// Both backends are registered so the mode can be flipped via configuration
/// without restarting (re-resolves options on each call).
public sealed class FieldRetrievalRouter : IFieldRetriever
{
    private readonly IOptionsMonitor<FieldRetrievalOptions> _options;
    private readonly TaxonomyRetriever _taxonomy;
    private readonly Lazy<IFieldRetriever> _vector;

    public FieldRetrievalRouter(
        IOptionsMonitor<FieldRetrievalOptions> options,
        TaxonomyRetriever taxonomy,
        Lazy<IFieldRetriever> vector)
    {
        _options = options;
        _taxonomy = taxonomy;
        _vector = vector;
    }

    private IFieldRetriever Active =>
        _options.CurrentValue.Mode == FieldRetrievalMode.Vector ? _vector.Value : _taxonomy;

    public Task<IReadOnlyDictionary<string, int>> GetTagsForUniverseAsync(string universe, CancellationToken ct = default) =>
        Active.GetTagsForUniverseAsync(universe, ct);

    public Task<IReadOnlyList<FieldResult>> RetrieveAsync(
        IReadOnlyList<string> universes,
        IReadOnlyList<string> tags,
        int topK,
        CancellationToken ct = default) =>
        Active.RetrieveAsync(universes, tags, topK, ct);
}

