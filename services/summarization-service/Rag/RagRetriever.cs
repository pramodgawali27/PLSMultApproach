using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PLS.BuildingBlocks.Data;

namespace SummarizationService.Rag;

public sealed class RagRetriever : IRagRetriever
{
    private static readonly string[] DefaultQueries =
    {
        "study design and population",
        "interventions and comparisons",
        "main results and outcomes",
        "safety and adverse events",
        "limitations and caveats"
    };

    private readonly IEmbeddingClient _embeddingClient;
    private readonly IVectorStore _vectorStore;
    private readonly PlsDbContext _db;

    public RagRetriever(IEmbeddingClient embeddingClient, IVectorStore vectorStore, PlsDbContext db)
    {
        _embeddingClient = embeddingClient;
        _vectorStore = vectorStore;
        _db = db;
    }

    public async Task<RagContext> RetrieveForPlsAsync(Guid documentId, Guid tenantId, CancellationToken cancellationToken)
    {
        var queries = DefaultQueries.ToList();
        var results = new Dictionary<string, RagChunk>();
        var topK = 4;

        foreach (var query in queries)
        {
            var embedding = await _embeddingClient.EmbedAsync(query, cancellationToken);
            var hits = await _vectorStore.SearchAsync(
                new VectorSearchRequest(documentId, tenantId, embedding, topK),
                cancellationToken);

            var hitRecords = hits
                .Select(hit => new { hit.ChunkId, hit.Score, hit.Page, hit.Section })
                .ToList();

            var run = new RetrievalRun
            {
                Id = Guid.NewGuid(),
                DocumentId = documentId,
                TenantId = tenantId,
                Lane = PLS.Contracts.SummaryLane.LaneC,
                Query = query,
                HitsJson = JsonSerializer.Serialize(hitRecords),
                CreatedAt = DateTimeOffset.UtcNow
            };
            _db.RetrievalRuns.Add(run);

            foreach (var hit in hits)
            {
                if (results.TryGetValue(hit.ChunkId, out var existing))
                {
                    if (hit.Score > existing.Score)
                    {
                        results[hit.ChunkId] = new RagChunk(hit.ChunkId, hit.Content, hit.Page, hit.Section, hit.Score);
                    }
                }
                else
                {
                    results[hit.ChunkId] = new RagChunk(hit.ChunkId, hit.Content, hit.Page, hit.Section, hit.Score);
                }
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        var ordered = results.Values.OrderByDescending(x => x.Score).ToList();
        return new RagContext(ordered, queries);
    }
}

public interface IRagRetriever
{
    Task<RagContext> RetrieveForPlsAsync(Guid documentId, Guid tenantId, CancellationToken cancellationToken);
}
