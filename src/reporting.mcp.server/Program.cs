using Qdrant.Client;
using reporting.agent.core.Configuration;
using reporting.agent.core.Services.Druid;
using reporting.agent.core.Services.SecureGateway;
using reporting.agent.core.Services.Sql;
using reporting.agent.core.Services.Taxonomy;
using reporting.agent.core.Services.Vector;
using reporting.mcp.server.Endpoints;
using reporting.mcp.server.Tools;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables();

builder.Services
    .Configure<DruidOptions>(builder.Configuration.GetSection(DruidOptions.SectionName))
    .Configure<QdrantOptions>(builder.Configuration.GetSection(QdrantOptions.SectionName))
    .Configure<AzureOpenAIOptions>(builder.Configuration.GetSection(AzureOpenAIOptions.SectionName))
    .Configure<SecureGatewayOptions>(builder.Configuration.GetSection(SecureGatewayOptions.SectionName))
    .Configure<FieldRetrievalOptions>(builder.Configuration.GetSection(FieldRetrievalOptions.SectionName));

builder.Services.AddSingleton<ColumnRegistry>();
builder.Services.AddSingleton<TaxonomyRetriever>();
builder.Services.AddSingleton<ColumnService>();
builder.Services.AddSingleton<SqlValidator>();
builder.Services.AddSingleton<SqlTransformer>();

builder.Services.AddHttpClient<DruidClient>();
builder.Services.AddHttpClient<SecureGatewayClient>();

builder.Services.AddSingleton<IEmbeddingService, AzureOpenAIEmbeddingService>();
builder.Services.AddSingleton<QdrantClient>(sp =>
{
    var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<QdrantOptions>>().Value;
    var uri = new Uri(opts.Url);
    return new QdrantClient(uri.Host, uri.Port, https: uri.Scheme == Uri.UriSchemeHttps, apiKey: opts.ApiKey);
});
builder.Services.AddSingleton<QdrantFieldRetriever>();

builder.Services.AddSingleton<IFieldRetriever>(sp => new FieldRetrievalRouter(
    sp.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<FieldRetrievalOptions>>(),
    sp.GetRequiredService<TaxonomyRetriever>(),
    new Lazy<IFieldRetriever>(() => sp.GetRequiredService<QdrantFieldRetriever>())));

builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();

var app = builder.Build();

app.MapMcp();
app.MapColumnEndpoints();
app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

app.Run();

public partial class Program;

