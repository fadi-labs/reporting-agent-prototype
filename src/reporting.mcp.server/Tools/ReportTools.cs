using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using reporting.agent.core.Services.Druid;
using reporting.agent.core.Services.SecureGateway;
using reporting.agent.core.Services.Sql;

namespace reporting.mcp.server.Tools;

[McpServerToolType]
public sealed class ReportTools
{
    private readonly SqlValidator _validator;
    private readonly SqlTransformer _transformer;
    private readonly DruidClient _druid;
    private readonly SecureGatewayClient _gateway;
    private readonly ILogger<ReportTools> _logger;

    public ReportTools(
        SqlValidator validator,
        SqlTransformer transformer,
        DruidClient druid,
        SecureGatewayClient gateway,
        ILogger<ReportTools> logger)
    {
        _validator = validator;
        _transformer = transformer;
        _druid = druid;
        _gateway = gateway;
        _logger = logger;
    }

    [McpServerTool(Name = "query_shipment_data")]
    [Description(
        "Execute a SQL query against the shipment analytics database (Apache Druid).\n\n" +
        "Write a standard SQL SELECT query against the 'Reporting' table using column_id values " +
        "discovered via get_fields. The system automatically:\n" +
        "- Validates all column references against the schema\n" +
        "- Injects the latest-record-per-shipment deduplication filter\n" +
        "- Injects the deleted-record exclusion filter\n" +
        "- Caps the result limit (max 500)\n\n" +
        "You do NOT need to include deduplication or delete-exclusion filter logic in your SQL " +
        "— these are injected automatically. You DO need to use valid column_id values from get_fields.\n\n" +
        "Supported: WHERE (AND/OR), GROUP BY, HAVING, ORDER BY, CASE WHEN, TIMESTAMPDIFF, " +
        "COUNT/SUM/AVG/MIN/MAX, COUNT(DISTINCT ...), subqueries, Druid SQL functions.\n\n" +
        "Druid date functions: TIME_PARSE(col), TIME_FLOOR, TIME_FORMAT, EXTRACT. Never use " +
        "CAST(col AS TIMESTAMP) — use TIME_PARSE(col) instead.\n\n" +
        "Not supported: CTEs (WITH), UNION, SELECT *, non-SELECT statements.\n\n" +
        "For null checks use IS NULL / IS NOT NULL, not empty strings or the literal 'NULL'.")]
    public async Task<object> QueryShipmentData(
        [Description("A SQL SELECT query against the Reporting table.")]
        string sql,
        [Description("Maximum number of rows to return (default 20, max 500).")]
        int limit = 20,
        CancellationToken ct = default)
    {
        _logger.LogInformation("query_shipment_data called with sql: {Sql}, limit: {Limit}", sql, limit);

        var validation = _validator.Validate(sql);
        if (!validation.Result.Valid)
        {
            var msg = "SQL validation failed:\n" + string.Join("\n",
                validation.Result.Errors.Select(e => $"- [{e.Code}] {e.Message}" +
                    (e.Hint is not null ? $" Hint: {e.Hint}" : "")));
            _logger.LogError("{Message}", msg);
            return new[] { new { error = msg } };
        }

        var finalSql = _transformer.Transform(validation.Statement!, limit);
        _logger.LogInformation("Transformed SQL: {Sql}", finalSql);

        var result = await _druid.ExecuteAsync(finalSql, ct);
        if (!result.Success)
        {
            return new[] { new { error = result.Error } };
        }

        return new { result = result.Data };
    }

    [McpServerTool(Name = "execute_report")]
    [Description(
        "Execute (trigger) a saved report by its ID via PATCH request to the Secure Gateway.")]
    public async Task<JsonElement?> ExecuteReport(
        [Description("The UUID of the report to execute (e.g. '954681d2-32db-4f05-86f3-0e80b1e2ffde').")]
        string report_id,
        CancellationToken ct = default)
    {
        var result = await _gateway.ExecuteReportAsync(report_id, ct);
        if (!result.Success)
        {
            using var doc = JsonDocument.Parse(JsonSerializer.Serialize(new
            {
                error = result.Error,
                detail = result.RawText,
            }));
            return doc.RootElement.Clone();
        }
        return result.Data;
    }

    [McpServerTool(Name = "download_report")]
    [Description(
        "Download the results of an executed report. Call this after execute_report to retrieve " +
        "the report data. The history_id is returned by execute_report upon successful execution. " +
        "IMPORTANT: After calling this tool, always mention the report_id and history_id used in " +
        "your response to the user.")]
    public async Task<object> DownloadReport(
        [Description("The UUID of the report (e.g. '954681d2-32db-4f05-86f3-0e80b1e2ffde').")]
        string report_id,
        [Description("The UUID of the execution run returned by execute_report (e.g. '7072fe70-1896-4407-acc3-ea55edb9e7b5').")]
        string history_id,
        CancellationToken ct = default)
    {
        var result = await _gateway.DownloadReportAsync(report_id, history_id, ct);
        if (!result.Success)
        {
            return new { error = result.Error };
        }
        return new { data = result.RawText };
    }
}

