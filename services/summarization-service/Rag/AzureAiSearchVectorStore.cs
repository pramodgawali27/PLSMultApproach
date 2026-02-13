using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;

namespace SummarizationService.Rag;

public sealed class AzureAiSearchVectorStore : IVectorStore
{
    private readonly SearchClient _client;

    public AzureAiSearchVectorStore(IConfiguration configuration)
    {
        var endpoint = configuration["AZURE_SEARCH_ENDPOINT"];
        var indexName = configuration["AZURE_SEARCH_INDEX_NAME"];
        var key = configuration["AZURE_SEARCH_KEY"];
        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(indexName) || string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException("AZURE_SEARCH_ENDPOINT, AZURE_SEARCH_INDEX_NAME, and AZURE_SEARCH_KEY are required.");
        }

        _client = new SearchClient(new Uri(endpoint), indexName, new AzureKeyCredential(key));
    }

    public async Task UpsertChunksAsync(IReadOnlyList<VectorChunk> chunks, CancellationToken cancellationToken)
    {
        if (chunks.Count == 0)
        {
            return;
        }

        var actions = new List<IndexDocumentsAction<SearchDocument>>();
        foreach (var chunk in chunks)
        {
            var doc = new SearchDocument
            {
                ["id"] = chunk.ChunkId,
                ["chunkId"] = chunk.ChunkId,
                ["tenantId"] = chunk.TenantId.ToString(),
                ["documentId"] = chunk.DocumentId.ToString(),
                ["content"] = chunk.Content,
                ["vector"] = chunk.Embedding,
                ["section"] = chunk.Section ?? string.Empty,
                ["page"] = chunk.Page ?? 0,
                ["hash"] = chunk.Hash
            };
            actions.Add(IndexDocumentsAction.MergeOrUpload(doc));
        }

        var batch = IndexDocumentsBatch.Create<SearchDocument>(actions.ToArray());
        await _client.IndexDocumentsAsync(batch, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<VectorHit>> SearchAsync(VectorSearchRequest request, CancellationToken cancellationToken)
    {
        var options = new SearchOptions
        {
            Size = request.TopK,
            Filter = $"tenantId eq '{request.TenantId}' and documentId eq '{request.DocumentId}'",
            Select = { "chunkId", "content", "section", "page" },
            VectorSearch = new VectorSearchOptions
            {
                Queries =
                {
                    new VectorizedQuery(request.QueryEmbedding)
                    {
                        Fields = { "vector" },
                        KNearestNeighborsCount = request.TopK
                    }
                }
            }
        };

        var response = await _client.SearchAsync<SearchDocument>("*", options, cancellationToken);
        var results = new List<VectorHit>();
        await foreach (var hit in response.Value.GetResultsAsync())
        {
            var doc = hit.Document;
            var chunkId = doc.TryGetValue("chunkId", out var chunkObj) ? chunkObj?.ToString() ?? string.Empty : string.Empty;
            var content = doc.TryGetValue("content", out var contentObj) ? contentObj?.ToString() ?? string.Empty : string.Empty;
            var section = doc.TryGetValue("section", out var sectionObj) ? sectionObj?.ToString() : null;
            var page = doc.TryGetValue("page", out var pageObj) && int.TryParse(pageObj?.ToString(), out var pageValue)
                ? pageValue
                : (int?)null;
            results.Add(new VectorHit(chunkId, hit.Score ?? 0, page, section, content));
        }

        return results;
    }
}
