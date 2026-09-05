using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using reporting.agent.core.Configuration;

namespace reporting.agent.core.Services.SecureGateway;

public sealed record SecureGatewayResult(bool Success, JsonElement? Data, string? RawText, string? Error, int? StatusCode);

/// Maersk Secure Gateway reporting API. Handles execute and download.
public sealed class SecureGatewayClient
{
    private readonly HttpClient _http;
    private readonly SecureGatewayOptions _options;
    private readonly ILogger<SecureGatewayClient> _logger;
    private string? _cachedUserPermissions;

    public SecureGatewayClient(HttpClient http, IOptions<SecureGatewayOptions> options, ILogger<SecureGatewayClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    private string GetUserPermissions()
    {
        if (_cachedUserPermissions is not null) return _cachedUserPermissions;
        var path = _options.UserPermissionsFile;
        if (!Path.IsPathRooted(path))
        {
            path = Path.Combine(AppContext.BaseDirectory, path);
        }
        _cachedUserPermissions = File.Exists(path) ? File.ReadAllText(path).Trim() : "";
        return _cachedUserPermissions;
    }

    public async Task<SecureGatewayResult> ExecuteReportAsync(string reportId, CancellationToken ct = default)
    {
        var url = $"{_options.Url.TrimEnd('/')}/reporting/reports/{reportId}";
        using var request = BuildRequest(HttpMethod.Patch, url);

        _logger.LogInformation("Executing report {ReportId}", reportId);

        using var response = await _http.SendAsync(request, ct);
        var text = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Report execution failed: {Status} - {Body}", (int)response.StatusCode, text);
            return new SecureGatewayResult(false, null, text, $"HTTP {(int)response.StatusCode}", (int)response.StatusCode);
        }

        try
        {
            var data = JsonSerializer.Deserialize<JsonElement>(text);
            return new SecureGatewayResult(true, data, text, null, (int)response.StatusCode);
        }
        catch (JsonException)
        {
            return new SecureGatewayResult(true, null, text, null, (int)response.StatusCode);
        }
    }

    public async Task<SecureGatewayResult> DownloadReportAsync(string reportId, string historyId, CancellationToken ct = default)
    {
        var url = $"{_options.Url.TrimEnd('/')}/reporting/reports/{reportId}/history/{historyId}";
        using var request = BuildRequest(HttpMethod.Get, url);

        _logger.LogInformation("Fetching history {HistoryId} for report {ReportId}", historyId, reportId);

        using var response = await _http.SendAsync(request, ct);
        var text = await response.Content.ReadAsStringAsync(ct);

        if ((int)response.StatusCode != 200)
        {
            return new SecureGatewayResult(false, null, null,
                "Report has not been executed yet. Please try after a while.",
                (int)response.StatusCode);
        }

        return new SecureGatewayResult(true, null, text, null, (int)response.StatusCode);
    }

    private HttpRequestMessage BuildRequest(HttpMethod method, string url)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Accept.ParseAdd("application/json, text/plain, */*");
        request.Headers.TryAddWithoutValidation("api-version", "1.0");
        request.Headers.TryAddWithoutValidation("service-key", _options.ServiceKey);
        request.Headers.TryAddWithoutValidation("user-permissions", GetUserPermissions());
        return request;
    }
}

