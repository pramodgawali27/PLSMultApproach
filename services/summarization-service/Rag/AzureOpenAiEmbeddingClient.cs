using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Azure.Core;
using Azure.Identity;

namespace SummarizationService.Rag;

public sealed class AzureOpenAiEmbeddingClient : IEmbeddingClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public AzureOpenAiEmbeddingClient(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken)
    {
        var endpoint = _configuration["AZURE_OPENAI_ENDPOINT"] ?? string.Empty;
        var deployment = _configuration["AZURE_OPENAI_EMBEDDING_DEPLOYMENT"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(deployment))
        {
            throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT and AZURE_OPENAI_EMBEDDING_DEPLOYMENT are required.");
        }

        var baseEndpoint = endpoint.TrimEnd('/');
        if (!baseEndpoint.EndsWith("/openai/v1", StringComparison.OrdinalIgnoreCase))
        {
            baseEndpoint = $"{baseEndpoint}/openai/v1";
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseEndpoint}/embeddings")
        {
            Content = JsonContent.Create(new EmbeddingRequest(deployment, text))
        };

        var apiKey = _configuration["AZURE_OPENAI_KEY"];
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            request.Headers.Add("api-key", apiKey);
        }
        else
        {
            var credential = new DefaultAzureCredential();
            var token = await credential.GetTokenAsync(
                new TokenRequestContext(new[] { "https://cognitiveservices.azure.com/.default" }),
                cancellationToken);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        }

        var client = _httpClientFactory.CreateClient("azureopenai");
        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<EmbeddingResponse>(cancellationToken: cancellationToken);
        var embedding = payload?.Data?.FirstOrDefault()?.Embedding;
        if (embedding is null || embedding.Count == 0)
        {
            throw new InvalidOperationException("Embedding response was empty.");
        }

        return embedding.ToArray();
    }

    private sealed record EmbeddingRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("input")] string Input);

    private sealed record EmbeddingResponse(
        [property: JsonPropertyName("data")] List<EmbeddingItem>? Data);

    private sealed record EmbeddingItem(
        [property: JsonPropertyName("embedding")] List<float> Embedding);
}
