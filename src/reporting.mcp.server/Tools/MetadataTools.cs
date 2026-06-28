using System.ComponentModel;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using reporting.agent.core.Models;
using reporting.agent.core.Services.Taxonomy;

namespace reporting.mcp.server.Tools;

[McpServerToolType]
public sealed class MetadataTools
{
    private readonly IFieldRetriever _fields;
    private readonly ILogger<MetadataTools> _logger;

    public MetadataTools(IFieldRetriever fields, ILogger<MetadataTools> logger)
    {
        _fields = fields;
        _logger = logger;
    }

    [McpServerTool(Name = "get_field_tags")]
    [Description(
        "Get available tags and field counts for a universe.\n\n" +
        "Call this FIRST to discover what types of fields are available in a universe, " +
        "then use get_fields() with specific tags to retrieve matching fields.\n\n" +
        "Common tags: identifier, status, date, milestone, leg, location, partner, quantity, " +
        "weight, volume, cost, currency, demurrage, detention, container, document, reference, " +
        "flag, vessel, mode, cargo, service, customs.\n\n" +
        "Returns: dict mapping each tag to the number of enabled fields with that tag, " +
        "e.g. {\"status\": 8, \"date\": 15, \"cost\": 6}.")]
    public async Task<IReadOnlyDictionary<string, int>> GetFieldTags(
        [Description("The universe to explore. One of: Customer Order, Shipper Booking, Carrier Booking, Cargo Stuffing, Shipping Instruction, Events And Milestones, Destination, Customer Messaging Service.")]
        string universe,
        CancellationToken ct = default)
    {
        _logger.LogInformation("get_field_tags called for universe '{Universe}'", universe);
        var tags = await _fields.GetTagsForUniverseAsync(universe, ct);
        _logger.LogInformation("get_field_tags returned {Count} tags for '{Universe}'", tags.Count, universe);
        return tags;
    }

    [McpServerTool(Name = "get_fields")]
    [Description(
        "Get reporting fields filtered by universe and tags.\n\n" +
        "Returns fields matching ANY of the provided tags within the specified universes. " +
        "Also resolves linkedIds dependencies automatically.\n\n" +
        "IMPORTANT: The returned JSON contains a `column_id` field in each object. You MUST use " +
        "these exact `column_id` values as identifiers in downstream tools. Do NOT rename, modify, " +
        "or invent column IDs.\n\n" +
        "Each result includes: column_id, column_name, universe, data_type, description, tags, " +
        "linked_ids, role ('matched' or 'dependency'), and optionally allowed_values.")]
    public async Task<IReadOnlyList<FieldResult>> GetFields(
        [Description("One or more universe names. Fields from all listed universes are included (OR logic).")]
        string[] universes,
        [Description("One or more tags. Returns fields matching ANY tag (OR logic). Use get_field_tags() first to discover available tags.")]
        string[] tags,
        [Description("Max number of matched fields to return. Dependency fields (from linkedIds) are returned in addition.")]
        int top_k = 20,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "get_fields called with universes={Universes}, tags={Tags}, top_k={TopK}",
            string.Join(",", universes), string.Join(",", tags), top_k);

        var results = await _fields.RetrieveAsync(universes, tags, top_k, ct);

        var matched = results.Count(r => r.Role == "matched");
        var deps = results.Count(r => r.Role == "dependency");
        _logger.LogInformation("get_fields returned {Matched} matched + {Deps} dependency fields", matched, deps);

        return results;
    }
}

