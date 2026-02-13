using System.Collections.Concurrent;

namespace SummarizationService.Rag;

public sealed class InMemoryVectorStore : IVectorStore
{
    private readonly ConcurrentDictionary<string, VectorChunk> _chunks = new();

    public Task UpsertChunksAsync(IReadOnlyList<VectorChunk> chunks, CancellationToken cancellationToken)
    {
        foreach (var chunk in chunks)
        {
            _chunks[chunk.ChunkId] = chunk;
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<VectorHit>> SearchAsync(VectorSearchRequest request, CancellationToken cancellationToken)
    {
        var hits = _chunks.Values
            .Where(chunk => chunk.DocumentId == request.DocumentId && chunk.TenantId == request.TenantId)
            .Select(chunk => new VectorHit(
                chunk.ChunkId,
                CosineSimilarity(request.QueryEmbedding, chunk.Embedding),
                chunk.Page,
                chunk.Section,
                chunk.Content))
            .OrderByDescending(hit => hit.Score)
            .Take(request.TopK)
            .ToList();

        return Task.FromResult<IReadOnlyList<VectorHit>>(hits);
    }

    private static double CosineSimilarity(float[] left, float[] right)
    {
        if (left.Length != right.Length || left.Length == 0)
        {
            return 0;
        }

        double dot = 0;
        double normLeft = 0;
        double normRight = 0;
        for (var i = 0; i < left.Length; i++)
        {
            dot += left[i] * right[i];
            normLeft += left[i] * left[i];
            normRight += right[i] * right[i];
        }

        if (normLeft == 0 || normRight == 0)
        {
            return 0;
        }

        return dot / (Math.Sqrt(normLeft) * Math.Sqrt(normRight));
    }
}
