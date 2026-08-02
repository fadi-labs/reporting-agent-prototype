using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using reporting.agent.core.Configuration;
using reporting.agent.core.Models;
using reporting.agent.core.Services.Vector;

const int VectorSize = 1536; // text-embedding-3-small

var config = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true)
    .AddJsonFile("../reporting.mcp.server/appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .AddCommandLine(args)
    .Build();

var services = new ServiceCollection();
services.AddLogging(b => b.AddSimpleConsole(o => o.SingleLine = true));
services.Configure<QdrantOptions>(config.GetSection(QdrantOptions.SectionName));
services.Configure<AzureOpenAIOptions>(config.GetSection(AzureOpenAIOptions.SectionName));
services.Configure<FieldRetrievalOptions>(config.GetSection(FieldRetrievalOptions.SectionName));
services.AddSingleton<IEmbeddingService, AzureOpenAIEmbeddingService>();
services.AddSingleton<QdrantClient>(sp =>
{
    var opts = sp.GetRequiredService<IOptions<QdrantOptions>>().Value;
    var uri = new Uri(opts.Url);
    return new QdrantClient(uri.Host, uri.Port, https: uri.Scheme == Uri.UriSchemeHttps, apiKey: opts.ApiKey);
});

await using var provider = services.BuildServiceProvider();
var log = provider.GetRequiredService<ILoggerFactory>().CreateLogger("Seeder");
var qdrantOpts = provider.GetRequiredService<IOptions<QdrantOptions>>().Value;
var retrievalOpts = provider.GetRequiredService<IOptions<FieldRetrievalOptions>>().Value;
var qdrant = provider.GetRequiredService<QdrantClient>();
var embeddings = provider.GetRequiredService<IEmbeddingService>();

var columnsDir = ResolveColumnsDirectory(args, retrievalOpts.ColumnsDirectory);
log.LogInformation("Reading columns from {Dir}", columnsDir);
log.LogInformation("Qdrant: {Url}, collection: {Collection}", qdrantOpts.Url, qdrantOpts.ReportingColumnsCollection);

var columns = LoadColumns(columnsDir);
log.LogInformation("Loaded {Count} columns across all universes", columns.Count);

await RecreateCollection(qdrant, qdrantOpts.ReportingColumnsCollection, log);

const int batchSize = 64;
var allTexts = columns.Select(BuildEmbeddingText).ToList();
var totalUpserted = 0;

for (var i = 0; i < columns.Count; i += batchSize)
{
    var batch = columns.GetRange(i, Math.Min(batchSize, columns.Count - i));
    var texts = allTexts.GetRange(i, batch.Count);
    var vectors = await embeddings.EmbedBatchAsync(texts);

    var points = new List<PointStruct>(batch.Count);
    for (var j = 0; j < batch.Count; j++)
    {
        var col = batch[j];
        var point = new PointStruct
        {
            Id = new PointId { Uuid = Guid.NewGuid().ToString() },
            Vectors = vectors[j].ToArray(),
        };
        point.Payload["metadata"] = new Value { StructValue = BuildMetadata(col) };
        point.Payload["page_content"] = new Value { StringValue = texts[j] };
        points.Add(point);
    }

    await qdrant.UpsertAsync(qdrantOpts.ReportingColumnsCollection, points);
    totalUpserted += batch.Count;
    log.LogInformation("Upserted {Done}/{Total}", totalUpserted, columns.Count);
}

log.LogInformation("Done. {Total} columns embedded into '{Collection}'.",
    totalUpserted, qdrantOpts.ReportingColumnsCollection);

static string ResolveColumnsDirectory(string[] args, string fallback)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (args[i] == "--columns") return args[i + 1];
    }

    var candidates = new[]
    {
        fallback,
        Path.Combine(AppContext.BaseDirectory, fallback),
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "reporting.mcp.server", "Resources", "columns"),
    };

    foreach (var path in candidates)
    {
        if (Directory.Exists(path)) return Path.GetFullPath(path);
    }
    throw new DirectoryNotFoundException(
        $"Columns directory not found. Tried: {string.Join(" ; ", candidates)}. Pass --columns <path> to override.");
}

static List<ColumnRow> LoadColumns(string dir)
{
    var all = new List<ColumnRow>();
    foreach (var path in Directory.EnumerateFiles(dir, "*.json").OrderBy(p => p, StringComparer.Ordinal))
    {
        var stem = Path.GetFileNameWithoutExtension(path);
        if (!UniverseMap.ByFileStem.TryGetValue(stem, out var universe)) continue;
        var universeName = UniverseMap.DisplayName[universe];
        using var stream = File.OpenRead(path);
        var defs = JsonSerializer.Deserialize<List<ColumnDefinition>>(stream) ?? new();
        foreach (var d in defs)
        {
            all.Add(new ColumnRow(
                ColumnId: d.Id,
                ColumnName: d.Name,
                Universe: universeName,
                DataType: d.DataType,
                Description: d.Description ?? "",
                Tags: d.Tags ?? new(),
                LinkedIds: d.LinkedIds ?? new(),
                AllowedValues: d.AllowedValues ?? new(),
                Enabled: d.Enabled));
        }
    }
    return all;
}

static string BuildEmbeddingText(ColumnRow c)
{
    var parts = new List<string>
    {
        $"column_id: {c.ColumnId}",
        $"column_name: {c.ColumnName}",
        $"universe: {c.Universe}",
        $"data_type: {c.DataType}",
        $"description: {c.Description}",
        $"tags: {string.Join(", ", c.Tags)}",
    };
    if (c.AllowedValues.Count > 0)
    {
        parts.Add($"allowed_values: {string.Join(", ", c.AllowedValues)}");
    }
    return string.Join("\n", parts);
}

static Struct BuildMetadata(ColumnRow c)
{
    var s = new Struct();
    s.Fields["column_id"] = new Value { StringValue = c.ColumnId };
    s.Fields["column_name"] = new Value { StringValue = c.ColumnName };
    s.Fields["universe"] = new Value { StringValue = c.Universe };
    s.Fields["data_type"] = new Value { StringValue = c.DataType };
    s.Fields["description"] = new Value { StringValue = c.Description };
    s.Fields["enabled"] = new Value { BoolValue = c.Enabled };
    s.Fields["tags"] = new Value { ListValue = ToList(c.Tags) };
    s.Fields["linked_ids"] = new Value { ListValue = ToList(c.LinkedIds) };
    return s;
}

static ListValue ToList(IEnumerable<string> values)
{
    var list = new ListValue();
    foreach (var v in values) list.Values.Add(new Value { StringValue = v });
    return list;
}

static async Task RecreateCollection(QdrantClient client, string name, ILogger log)
{
    var exists = await client.CollectionExistsAsync(name);
    if (exists)
    {
        log.LogInformation("Deleting existing collection '{Name}'", name);
        await client.DeleteCollectionAsync(name);
    }
    log.LogInformation("Creating collection '{Name}' (size={Size}, distance=Cosine)", name, VectorSize);
    await client.CreateCollectionAsync(name, new VectorParams
    {
        Size = VectorSize,
        Distance = Distance.Cosine,
    });
}

internal sealed record ColumnRow(
    string ColumnId,
    string ColumnName,
    string Universe,
    string DataType,
    string Description,
    List<string> Tags,
    List<string> LinkedIds,
    List<string> AllowedValues,
    bool Enabled);

