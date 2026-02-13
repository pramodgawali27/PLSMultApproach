using Microsoft.EntityFrameworkCore;
using PLS.BuildingBlocks.Data;
using SummarizationService.Extraction;

namespace SummarizationService.Rag;

public sealed class RagIndexer
{
    private readonly ITextExtractor _extractor;
    private readonly TextChunker _chunker;
    private readonly IEmbeddingClient _embeddingClient;
    private readonly IVectorStore _vectorStore;
    private readonly PlsDbContext _db;
    private readonly IConfiguration _configuration;

    public RagIndexer(
        ITextExtractor extractor,
        TextChunker chunker,
        IEmbeddingClient embeddingClient,
        IVectorStore vectorStore,
        PlsDbContext db,
        IConfiguration configuration)
    {
        _extractor = extractor;
        _chunker = chunker;
        _embeddingClient = embeddingClient;
        _vectorStore = vectorStore;
        _db = db;
        _configuration = configuration;
    }

    public async Task IndexAsync(Guid documentId, Guid tenantId, string storageUri, string contentType, CancellationToken cancellationToken)
    {
        var extraction = await _extractor.ExtractAsync(documentId, storageUri, contentType);
        if (!extraction.IsSuccess || extraction.Value is null)
        {
            throw new InvalidOperationException(extraction.Error ?? "Failed to extract text.");
        }

        var chunks = _chunker.Chunk(extraction.Value.Text);
        if (chunks.Count == 0)
        {
            return;
        }

        var storeText = _configuration.GetValue("RAG_STORE_TEXT", false);
        var vectorChunks = new List<VectorChunk>();
        var entities = new List<DocumentChunk>();

        foreach (var chunk in chunks)
        {
            var chunkId = TextChunker.BuildChunkId(documentId, chunk.Index, chunk.Content);
            var hash = TextChunker.HashContent(chunk.Content);
            var embedding = await _embeddingClient.EmbedAsync(chunk.Content, cancellationToken);

            vectorChunks.Add(new VectorChunk(
                chunkId,
                documentId,
                tenantId,
                chunk.Content,
                embedding,
                null,
                chunk.Section,
                hash));

            entities.Add(new DocumentChunk
            {
                Id = Guid.NewGuid(),
                DocumentId = documentId,
                TenantId = tenantId,
                ChunkId = chunkId,
                Page = null,
                Section = chunk.Section,
                Hash = hash,
                Text = storeText ? chunk.Content : null,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        var existing = _db.DocumentChunks.Where(x => x.DocumentId == documentId && x.TenantId == tenantId);
        _db.DocumentChunks.RemoveRange(existing);
        _db.DocumentChunks.AddRange(entities);
        await _db.SaveChangesAsync(cancellationToken);

        await _vectorStore.UpsertChunksAsync(vectorChunks, cancellationToken);
    }
}
