using System.Data;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using reporting.agent.core.Configuration;

namespace reporting.agent.core.Services.Database;

public enum DatabaseKind
{
    Report,
    Stage,
}

/// PostgreSQL access for the entity tools. Mirrors services/reporting_db_service.py
/// but uses Npgsql with dynamic-style row dictionaries returned to MCP clients.
public sealed class ReportingDbService
{
    private readonly PostgresOptions _options;
    private readonly ILogger<ReportingDbService> _logger;

    public ReportingDbService(IOptions<PostgresOptions> options, ILogger<ReportingDbService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    private string ConnectionString(DatabaseKind kind)
    {
        var db = kind == DatabaseKind.Stage ? _options.StageDatabase : _options.ReportDatabase;
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = _options.Host,
            Port = _options.Port,
            Username = _options.User,
            Password = _options.Password,
            Database = db,
        };
        return builder.ConnectionString;
    }

    public async Task<IReadOnlyList<IDictionary<string, object?>>> QueryAsync(
        DatabaseKind kind,
        string sql,
        IReadOnlyList<object?>? parameters = null,
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(ConnectionString(kind));
        await conn.OpenAsync(ct);

        await using var cmd = new NpgsqlCommand(sql, conn);
        BindParameters(cmd, parameters);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var rows = new List<IDictionary<string, object?>>();
        while (await reader.ReadAsync(ct))
        {
            rows.Add(ReadRow(reader));
        }
        return rows;
    }

    public async Task<IDictionary<string, object?>?> QuerySingleAsync(
        DatabaseKind kind,
        string sql,
        IReadOnlyList<object?>? parameters = null,
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(ConnectionString(kind));
        await conn.OpenAsync(ct);

        await using var cmd = new NpgsqlCommand(sql, conn);
        BindParameters(cmd, parameters);

        await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow, ct);
        if (!await reader.ReadAsync(ct)) return null;
        return ReadRow(reader);
    }

    private static void BindParameters(NpgsqlCommand cmd, IReadOnlyList<object?>? parameters)
    {
        if (parameters is null) return;
        foreach (var value in parameters)
        {
            cmd.Parameters.AddWithValue(value ?? DBNull.Value);
        }
    }

    private static Dictionary<string, object?> ReadRow(NpgsqlDataReader reader)
    {
        var row = new Dictionary<string, object?>(reader.FieldCount, StringComparer.Ordinal);
        for (var i = 0; i < reader.FieldCount; i++)
        {
            var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
            row[reader.GetName(i)] = value;
        }
        return row;
    }
}

