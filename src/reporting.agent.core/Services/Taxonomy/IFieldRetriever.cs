using reporting.agent.core.Models;

namespace reporting.agent.core.Services.Taxonomy;

/// Abstraction over the two field-retrieval modes (taxonomy / vector).
/// Both modes serve the same get_field_tags / get_fields MCP tools so the
/// retrieval source can be switched via configuration without changing tools.
public interface IFieldRetriever
{
    Task<IReadOnlyDictionary<string, int>> GetTagsForUniverseAsync(
        string universe, CancellationToken ct = default);

    Task<IReadOnlyList<FieldResult>> RetrieveAsync(
        IReadOnlyList<string> universes,
        IReadOnlyList<string> tags,
        int topK,
        CancellationToken ct = default);
}

