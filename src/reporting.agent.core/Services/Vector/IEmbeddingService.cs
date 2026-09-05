namespace reporting.agent.core.Services.Vector;

public interface IEmbeddingService
{
    Task<ReadOnlyMemory<float>> EmbedAsync(string text, CancellationToken ct = default);
    Task<IReadOnlyList<ReadOnlyMemory<float>>> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default);
}

