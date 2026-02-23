using SummarizationService.Rag;

namespace SummarizationService.LaneD.Skills;

/// <summary>
/// Provides per-query RAG retrieval for Lane D agent stages.
/// Retrieves topK chunks from the vector store for a specific query and document.
/// </summary>
public sealed class RetrievalSkill : IAgentSkill
{
    public string Name => "RetrievalSkill";

    private readonly IEmbeddingClient _embeddingClient;
    private readonly IVectorStore _vectorStore;

    public RetrievalSkill(IEmbeddingClient embeddingClient, IVectorStore vectorStore)
    {
        _embeddingClient = embeddingClient;
        _vectorStore = vectorStore;
    }

    /// <summary>Retrieve topK chunks for the given query and document.</summary>
    public async Task<IReadOnlyList<RagChunk>> RetrieveAsync(
        string query,
        Guid documentId,
        Guid tenantId,
        int topK,
        CancellationToken cancellationToken)
    {
        var embedding = await _embeddingClient.EmbedAsync(query, cancellationToken);
        var hits = await _vectorStore.SearchAsync(
            new VectorSearchRequest(documentId, tenantId, embedding, topK),
            cancellationToken);

        return hits
            .Select(h => new RagChunk(h.ChunkId, h.Content, h.Page, h.Section, h.Score))
            .OrderByDescending(c => c.Score)
            .ToList();
    }

    /// <summary>
    /// Run multiple queries in sequence and merge results, de-duplicating by chunkId,
    /// keeping the highest-scored copy. Returns up to maxTotal chunks.
    /// </summary>
    public async Task<IReadOnlyList<RagChunk>> RetrieveMultiAsync(
        IReadOnlyList<string> queries,
        Guid documentId,
        Guid tenantId,
        int topKPerQuery,
        int maxTotal,
        CancellationToken cancellationToken)
    {
        var merged = new Dictionary<string, RagChunk>(StringComparer.OrdinalIgnoreCase);

        foreach (var query in queries)
        {
            var chunks = await RetrieveAsync(query, documentId, tenantId, topKPerQuery, cancellationToken);
            foreach (var chunk in chunks)
            {
                if (!merged.TryGetValue(chunk.ChunkId, out var existing) || chunk.Score > existing.Score)
                {
                    merged[chunk.ChunkId] = chunk;
                }
            }
        }

        return merged.Values
            .OrderByDescending(c => c.Score)
            .Take(Math.Max(1, maxTotal))
            .ToList();
    }
}
