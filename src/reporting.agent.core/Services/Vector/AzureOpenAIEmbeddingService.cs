using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Options;
using OpenAI.Embeddings;
using reporting.agent.core.Configuration;

namespace reporting.agent.core.Services.Vector;

public sealed class AzureOpenAIEmbeddingService : IEmbeddingService
{
    private readonly EmbeddingClient? _client;
    private readonly string? _missingConfig;

    public AzureOpenAIEmbeddingService(IOptions<AzureOpenAIOptions> options)
    {
        var opts = options.Value;
        if (string.IsNullOrEmpty(opts.Endpoint) || string.IsNullOrEmpty(opts.ApiKey))
        {
            _missingConfig = "Azure OpenAI Endpoint/ApiKey are not configured. Set AzureOpenAI__Endpoint and AzureOpenAI__ApiKey to use vector field retrieval.";
            return;
        }

        var azure = new AzureOpenAIClient(new Uri(opts.Endpoint), new AzureKeyCredential(opts.ApiKey));
        _client = azure.GetEmbeddingClient(opts.EmbeddingsDeployment);
    }

    public async Task<ReadOnlyMemory<float>> EmbedAsync(string text, CancellationToken ct = default)
    {
        EnsureConfigured();
        var response = await _client!.GenerateEmbeddingAsync(text, cancellationToken: ct);
        return response.Value.ToFloats();
    }

    public async Task<IReadOnlyList<ReadOnlyMemory<float>>> EmbedBatchAsync(
        IReadOnlyList<string> texts, CancellationToken ct = default)
    {
        EnsureConfigured();
        var response = await _client!.GenerateEmbeddingsAsync(texts, cancellationToken: ct);
        return response.Value.Select(e => e.ToFloats()).ToList();
    }

    private void EnsureConfigured()
    {
        if (_missingConfig is not null)
            throw new InvalidOperationException(_missingConfig);
    }
}

