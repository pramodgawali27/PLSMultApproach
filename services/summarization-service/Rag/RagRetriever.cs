using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
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
    private readonly IConfiguration _configuration;
    private readonly ILogger<RagRetriever> _logger;

    public RagRetriever(
        IEmbeddingClient embeddingClient,
        IVectorStore vectorStore,
        PlsDbContext db,
        IConfiguration configuration,
        ILogger<RagRetriever> logger)
    {
        _embeddingClient = embeddingClient;
        _vectorStore = vectorStore;
        _db = db;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<RagContext> RetrieveForPlsAsync(Guid documentId, Guid tenantId, CancellationToken cancellationToken)
    {
        var queries = DefaultQueries.ToList();
        var topK = _configuration.GetValue("RAG_TOPK", 4);
        var maxChunks = _configuration.GetValue("RAG_MAX_CHUNKS", 12);
        var retries = _configuration.GetValue("RAG_RETRIEVE_RETRIES", 2);
        var delayMs = _configuration.GetValue("RAG_RETRIEVE_DELAY_MS", 750);

        RagContext? lastContext = null;

        for (var attempt = 0; attempt <= retries; attempt++)
        {
            var results = new Dictionary<string, RagChunk>();

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

            var ordered = results.Values
                .OrderByDescending(x => x.Score)
                .Take(Math.Max(1, maxChunks))
                .ToList();

            lastContext = new RagContext(ordered, queries);
            if (ordered.Count > 0)
            {
                return lastContext;
            }

            if (attempt < retries)
            {
                _logger.LogInformation("No RAG context found for document {DocumentId}, retrying in {DelayMs}ms (attempt {Attempt}/{Retries}).",
                    documentId, delayMs, attempt + 1, retries + 1);
                await Task.Delay(delayMs, cancellationToken);
            }
        }

        return lastContext ?? new RagContext(new List<RagChunk>(), queries);
    }
}

public interface IRagRetriever
{
    Task<RagContext> RetrieveForPlsAsync(Guid documentId, Guid tenantId, CancellationToken cancellationToken);
}
