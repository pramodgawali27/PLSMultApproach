namespace SummarizationService.Rag;

public interface IVectorStore
{
    Task UpsertChunksAsync(IReadOnlyList<VectorChunk> chunks, CancellationToken cancellationToken);
    Task<IReadOnlyList<VectorHit>> SearchAsync(VectorSearchRequest request, CancellationToken cancellationToken);
}
