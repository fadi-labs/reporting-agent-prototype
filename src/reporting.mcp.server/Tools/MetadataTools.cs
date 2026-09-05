using System.ComponentModel;
using ModelContextProtocol.Server;
using reporting.agent.core.Models;
using reporting.agent.core.Services.Taxonomy;

namespace reporting.mcp.server.Tools;

public sealed record UniverseInfo(string Name, string Description);

[McpServerToolType]
public sealed class MetadataTools
{
    private static readonly IReadOnlyList<UniverseInfo> Universes =
    [
        new("Customer Order",             "Customer orders and their lifecycle, including order status, line items, and shipment assignments."),
        new("Shipper Booking",            "Bookings made by shippers to reserve capacity, covering booking status and cargo details."),
        new("Carrier Booking",            "Carrier-side bookings confirming vessel space, including carrier references and booking status."),
        new("Cargo Stuffing",             "Cargo stuffing operations linking cargo to containers, including stuffing status and dates."),
        new("Shipping Instruction",       "Shipping instructions submitted by shippers, containing cargo and routing instruction details."),
        new("Events And Milestones",      "Shipment events and milestones tracking the physical movement of goods across the supply chain."),
        new("Destination",               "Destination-leg data covering final delivery information and port-of-discharge details."),
        new("Customer Messaging Service", "Messages exchanged with customers via the customer messaging service, including message status."),
    ];

    private static readonly IReadOnlyList<string> ValidUniverses =
        Universes.Select(u => u.Name).ToList();

    private static readonly IReadOnlyList<string> ValidTags =
    [
        "identifier", "status", "date", "milestone", "leg", "location", "partner", "quantity",
        "weight", "volume", "cost", "currency", "demurrage", "detention", "container", "document",
        "reference", "flag", "vessel", "mode", "cargo", "service", "customs"
    ];


    private readonly IFieldRetriever _fields;
    private readonly ILogger<MetadataTools> _logger;

    public MetadataTools(IFieldRetriever fields, ILogger<MetadataTools> logger)
    {
        _fields = fields;
        _logger = logger;
    }

    [McpServerTool(Name = "list_universes")]
    [Description(
        "List all available reporting universes with a short description of each.\n\n" +
        "Call this FIRST to discover which universes exist, then call get_field_tags(universe) " +
        "to explore the available field categories within a universe.\n\n" +
        "Returns: ordered list of objects with 'name' (the exact string to pass to other tools) " +
        "and 'description' (one-line domain summary).")]
    public Task<IReadOnlyList<UniverseInfo>> ListUniverses(CancellationToken ct = default)
    {
        _logger.LogInformation("list_universes called");
        _logger.LogInformation("list_universes returned {Count} universes", Universes.Count);
        return Task.FromResult(Universes);
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

        var errors = new List<string>();

        if (universes.Length == 0)
        {
            errors.Add("universes must contain at least one entry.");
        }
        else
        {
            var unknownUniverses = universes
                .Where(u => !ValidUniverses.Any(v => v.Equals(u, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            if (unknownUniverses.Count > 0)
                errors.Add($"Unknown universes: {string.Join(", ", unknownUniverses.Select(u => $"\"{u}\""))}. " +
                           $"Valid values: {string.Join(", ", ValidUniverses)}.");
        }

        if (tags.Length == 0)
        {
            errors.Add("tags must contain at least one entry.");
        }
        else
        {
            var unknownTags = tags
                .Where(t => !ValidTags.Any(v => v.Equals(t, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            if (unknownTags.Count > 0)
                errors.Add($"Unknown tags: {string.Join(", ", unknownTags.Select(t => $"\"{t}\""))}. " +
                           $"Valid values: {string.Join(", ", ValidTags)}.");
        }

        if (top_k < 1 || top_k > 100)
            errors.Add($"top_k must be between 1 and 100 inclusive (got {top_k}).");

        if (errors.Count > 0)
            throw new ArgumentException(string.Join(" ", errors));

        var results = await _fields.RetrieveAsync(universes, tags, top_k, ct);

        var matched = results.Count(r => r.Role == "matched");
        var deps = results.Count(r => r.Role == "dependency");
        _logger.LogInformation("get_fields returned {Matched} matched + {Deps} dependency fields", matched, deps);

        return results;
    }
}

