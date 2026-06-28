using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using reporting.agent.core.Configuration;

namespace reporting.agent.core.Services.Druid;

public sealed record DruidQueryResult(bool Success, JsonElement? Data, string? Error);

/// Thin wrapper around Druid's /druid/v2/sql endpoint.
public sealed class DruidClient
{
    private readonly HttpClient _http;
    private readonly DruidOptions _options;
    private readonly ILogger<DruidClient> _logger;

    public DruidClient(HttpClient http, IOptions<DruidOptions> options, ILogger<DruidClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<DruidQueryResult> ExecuteAsync(string sql, CancellationToken ct = default)
    {
        var body = new
        {
            query = sql,
            resultFormat = "array",
            header = true,
            typesHeader = false,
            sqlTypesHeader = false,
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.Host.TrimEnd('/')}/druid/v2/sql")
        {
            Content = JsonContent.Create(body),
        };
        if (!string.IsNullOrEmpty(_options.AuthBase64))
        {
            request.Headers.TryAddWithoutValidation("Authorization", $"Basic {_options.AuthBase64}");
        }

        _logger.LogInformation("Executing Druid SQL: {Sql}", string.Join(' ', sql.Split('\n', StringSplitOptions.RemoveEmptyEntries)));

        using var response = await _http.SendAsync(request, ct);
        var contentStream = await response.Content.ReadAsStreamAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            try
            {
                using var doc = await JsonDocument.ParseAsync(contentStream, cancellationToken: ct);
                var errMsg = doc.RootElement.TryGetProperty("errorMessage", out var em)
                    ? em.GetString() ?? "Unknown error"
                    : "Unknown error";
                _logger.LogError("Druid query failed: {Status}: {Error}", (int)response.StatusCode, errMsg);
                return new DruidQueryResult(false, null, errMsg);
            }
            catch (JsonException)
            {
                return new DruidQueryResult(false, null, $"HTTP {(int)response.StatusCode}");
            }
        }

        var data = await JsonSerializer.DeserializeAsync<JsonElement>(contentStream, cancellationToken: ct);
        return new DruidQueryResult(true, data, null);
    }
}

