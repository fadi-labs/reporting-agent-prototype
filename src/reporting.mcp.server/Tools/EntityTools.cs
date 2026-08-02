using System.ComponentModel;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using reporting.agent.core.Services.Database;

namespace reporting.mcp.server.Tools;

[McpServerToolType]
public sealed class EntityTools
{
    private readonly ReportingDbService _db;
    private readonly ILogger<EntityTools> _logger;

    public EntityTools(ReportingDbService db, ILogger<EntityTools> logger)
    {
        _db = db;
        _logger = logger;
    }

    [McpServerTool(Name = "get_customer_order_details")]
    [Description(
        "Retrieve all fields of a customer order from the staging database by its order number. " +
        "Returns a dictionary of column-value pairs for the matching order, or null if no order " +
        "matches the given number.")]
    public async Task<IDictionary<string, object?>?> GetCustomerOrderDetails(
        [Description("The unique customer order number to look up.")]
        string customer_order_number,
        CancellationToken ct = default)
    {
        const string sql = "SELECT * FROM customer_order WHERE customer_order_number = $1";
        return await _db.QuerySingleAsync(DatabaseKind.Stage, sql, new object?[] { customer_order_number }, ct);
    }

    [McpServerTool(Name = "get_customer_order_details")]
    [Description(
        "Retrieve all fields of a customer order from the staging database by its order number. " +
        "Returns a dictionary of column-value pairs for the matching order, or null if no order " +
        "matches the given number.")]
    public async Task<IDictionary<string, object?>?> GetCustomerOrderDetails(
        [Description("The unique customer order number to look up.")]
        string customer_order_number,
        CancellationToken ct = default)
    {
        const string sql = "SELECT * FROM customer_order WHERE customer_order_number = $1";
        return await _db.QuerySingleAsync(DatabaseKind.Stage, sql, new object?[] { customer_order_number }, ct);
    }

    [McpServerTool(Name = "get_container_latest_milestone")]
    [Description(
        "Retrieve the most recent milestone or status event for a given container number. " +
        "Queries the events_and_milestones table and returns the single most recent event record, " +
        "ordered by event timestamp descending.")]
    public async Task<IDictionary<string, object?>?> GetContainerLatestMilestone(
        [Description("The equipment/container number to look up (e.g. 'TCKU1234567').")]
        string container_number,
        CancellationToken ct = default)
    {
        _logger.LogInformation("get_container_latest_milestone called with container_number={Container}", container_number);
        const string sql = """
            SELECT data FROM events_and_milestones
            WHERE equipment_number = $1
            ORDER BY event_timestamp DESC
            LIMIT 1
            """;
        return await _db.QuerySingleAsync(DatabaseKind.Stage, sql, new object?[] { container_number }, ct);
    }

    [McpServerTool(Name = "get_entity_relationships")]
    [Description(
        "Given any one entity identifier, returns related customer orders, shipper bookings, " +
        "carrier bookings, cargo stuffings, shipping instructions, and equipment numbers from the " +
        "scm_data_relations table. At least one argument must be non-null.")]
    public async Task<IDictionary<string, object?>?> GetEntityRelationships(
        [Description("Customer-assigned order ID.")]
        string? customer_order_id,
        [Description("Shipper booking identifier (typically starts with 'SBK').")]
        string? shipper_booking_number,
        [Description("Carrier booking identifier (typically starts with 'CBK').")]
        string? carrier_booking_public_id,
        [Description("Cargo stuffing / CLR identifier (typically starts with 'CLR').")]
        string? cargo_stuffing_number,
        [Description("Shipping instruction identifier (typically starts with 'SIN').")]
        string? shipping_instruction_number,
        [Description("Equipment/container number (e.g. 'TCKU1234567').")]
        string? equipment_number,
        CancellationToken ct = default)
    {
        var clauses = new List<string>();
        var values = new List<object?>();

        void Add(string column, string? value)
        {
            if (string.IsNullOrEmpty(value)) return;
            values.Add(value);
            clauses.Add($"{column} = ${values.Count}");
        }

        Add("customer_order_id", customer_order_id);
        Add("shipper_booking_number", string.IsNullOrEmpty(shipper_booking_number)
            ? null
            : shipper_booking_number.Replace("SBK", "", StringComparison.Ordinal));
        Add("carrier_booking_public_id", carrier_booking_public_id);
        Add("cargo_stuffing_number", string.IsNullOrEmpty(cargo_stuffing_number)
            ? null
            : cargo_stuffing_number.Replace("CLR", "", StringComparison.Ordinal));
        Add("shipping_instruction_number", shipping_instruction_number);
        Add("equipment_number", equipment_number);

        if (clauses.Count == 0)
        {
            throw new ArgumentException("At least one entity identifier must be provided.");
        }

        var sql = $"""
            SELECT customer_order_id, shipper_booking_number, carrier_booking_public_id,
                   cargo_stuffing_number, shipping_instruction_number, equipment_number
            FROM scm_data_relations
            WHERE {string.Join(" AND ", clauses)}
            """;

        return await _db.QuerySingleAsync(DatabaseKind.Stage, sql, values, ct);
    }

    [McpServerTool(Name = "get_entity_identification_rules")]
    [Description(
        "Return pattern-matching rules to identify entities from " +
        "user input. Each entity has a distinct identifier format. Use these rules to " +
        "determine which entity type a user-provided value refers to, so downstream tools can " +
        "query the correct field.")]
    public Task<string> GetEntityIdentificationRules(CancellationToken ct = default)
    {
        _logger.LogInformation("get_entity_identification_rules called");
        const string rules =
            "Customer Order: A free-form identifier assigned by the customer. It has no fixed prefix — any value that does not match another entity's prefix pattern should be treated as a customer order number.\n" +
            "Shipper Booking: Prefixed with 'SBK' followed by digits (e.g. SBK1234567). Represents a booking created by the shipper.\n" +
            "Carrier Booking: Prefixed with 'CBK' followed by digits (e.g. CBK1234567). Represents a booking created by the carrier.\n" +
            "Cargo Stuffing: Prefixed with 'CLR' followed by digits (e.g. CLR1234567). Represents a cargo stuffing / container loading record.\n" +
            "Shipping Instruction: Prefixed with 'SIN' followed by digits (e.g. SIN1234567). Represents shipping documentation instructions.\n" +
            "Container: Follows the ISO 6346 standard — four uppercase letters (three-letter owner code + category identifier 'U', 'J', or 'Z') followed by seven digits (six-digit serial + check digit), e.g. TCKU1234567.\n\n" +
            "Identification priority: check prefixed patterns (SBK, CBK, CLR, SIN) first, then the ISO 6346 container pattern, and fall back to Customer Order if none match.";
        return Task.FromResult(rules);
    }
}

