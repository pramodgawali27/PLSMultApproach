namespace SummarizationService.Rag;

public sealed record VectorChunk(
    string ChunkId,
    Guid DocumentId,
    Guid TenantId,
    string Content,
    float[] Embedding,
    int? Page,
    string? Section,
    string Hash);

public sealed record VectorSearchRequest(
    Guid DocumentId,
    Guid TenantId,
    float[] QueryEmbedding,
    int TopK);

public sealed record VectorHit(
    string ChunkId,
    double Score,
    int? Page,
    string? Section,
    string Content);

public sealed record RagChunk(
    string ChunkId,
    string Content,
    int? Page,
    string? Section,
    double Score);

public sealed record RagContext(
    IReadOnlyList<RagChunk> Chunks,
    IReadOnlyList<string> Queries);
