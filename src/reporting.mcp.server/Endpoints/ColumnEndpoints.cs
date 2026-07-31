using System.Text.Json;
using Microsoft.AspNetCore.Http;
using reporting.agent.core.Models;
using reporting.agent.core.Services.Taxonomy;

namespace reporting.mcp.server.Endpoints;

/// REST endpoints for column management — mirrors routes/column_routes.py.
public static class ColumnEndpoints
{
    public static IEndpointRouteBuilder MapColumnEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/columns", (ColumnService service) =>
            Results.Ok(service.GetUniverses()));

        app.MapGet("/api/columns/{universe}", (
            string universe,
            HttpRequest request,
            ColumnService service) =>
        {
            var query = request.Query;
            bool? enabled = query.TryGetValue("enabled", out var enabledValue)
                ? string.Equals(enabledValue.ToString(), "true", StringComparison.OrdinalIgnoreCase)
                : null;

            // Test comment
            try
            {
                var (columns, etag) = service.GetColumns(
                    universe,
                    search: NullIfEmpty(query, "search"),
                    category: NullIfEmpty(query, "category"),
                    dataType: NullIfEmpty(query, "dataType"),
                    tag: NullIfEmpty(query, "tag"),
                    enabled: enabled);

                return Results.Ok(columns).WithEtag(etag);
            }
            catch (ArgumentException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        });

        app.MapPost("/api/columns/{universe}", async (
            string universe,
            HttpRequest request,
            ColumnService columnService,
            TaxonomyRetriever taxonomy) =>
        {
            var expectedEtag = request.Headers.IfMatch.ToString();
            if (string.IsNullOrEmpty(expectedEtag))
            {
                return Results.Json(new { error = "If-Match header is required" }, statusCode: 428);
            }

            ColumnCreateRequest? body;
            try
            {
                body = await JsonSerializer.DeserializeAsync<ColumnCreateRequest>(
                    request.Body,
                    new JsonSerializerOptions(JsonSerializerDefaults.Web));
            }
            catch (JsonException)
            {
                return Results.BadRequest(new { error = "Invalid JSON body" });
            }

            if (body?.Column is null || string.IsNullOrEmpty(body.Column.Id))
            {
                return Results.BadRequest(new { error = "Missing 'column' in request body" });
            }

            try
            {
                var (created, newEtag) = columnService.AddColumn(universe, body.Column, NormalizeEtag(expectedEtag));
                taxonomy.Reload();
                return Results.Json(new { column = created, etag = newEtag }, statusCode: 201).WithEtag(newEtag);
            }
            catch (ConflictException ex)
            {
                return Results.Json(new { error = ex.Message }, statusCode: 412);
            }
            catch (DuplicateException ex)
            {
                return Results.Json(new { error = ex.Message }, statusCode: 409);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        app.MapPatch("/api/columns/{universe}", async (
            string universe,
            HttpRequest request,
            ColumnService columnService,
            TaxonomyRetriever taxonomy) =>
        {
            var expectedEtag = request.Headers.IfMatch.ToString();
            if (string.IsNullOrEmpty(expectedEtag))
            {
                return Results.Json(new { error = "If-Match header is required for updates" }, statusCode: 428);
            }

            ColumnsBulkPatchRequest? body;
            try
            {
                body = await JsonSerializer.DeserializeAsync<ColumnsBulkPatchRequest>(
                    request.Body,
                    new JsonSerializerOptions(JsonSerializerDefaults.Web));
            }
            catch (JsonException)
            {
                return Results.BadRequest(new { error = "Invalid JSON body" });
            }

            if (body is null || body.Updates.Count == 0)
            {
                return Results.BadRequest(new { error = "No updates provided" });
            }

            try
            {
                var (updated, newEtag) = columnService.UpdateColumns(universe, body.Updates, NormalizeEtag(expectedEtag));
                taxonomy.Reload();
                return Results.Json(new { updated, etag = newEtag }).WithEtag(newEtag);
            }
            catch (ConflictException ex)
            {
                return Results.Json(new { error = ex.Message }, statusCode: 412);
            }
            catch (ArgumentException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        });

        app.MapGet("/api/columns/{universe}/tags", (string universe, ColumnService service) =>
        {
            try
            {
                return Results.Ok(service.GetTags(universe));
            }
            catch (ArgumentException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        });

        return app;
    }

    private static string? NullIfEmpty(IQueryCollection query, string key) =>
        query.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value.ToString())
            ? value.ToString()
            : null;

    private static string NormalizeEtag(string value) =>
        value.Trim().Trim('"');
}

internal static class ResultsExtensions
{
    public static IResult WithEtag(this IResult result, string etag) => new EtaggedResult(result, etag);

    private sealed class EtaggedResult(IResult inner, string etag) : IResult
    {
        public async Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.Headers.ETag = $"\"{etag}\"";
            await inner.ExecuteAsync(httpContext);
        }
    }
}

