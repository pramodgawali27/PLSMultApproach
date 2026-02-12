using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Azure.Core;
using Azure.Identity;

namespace SummarizationService.Llm;

public sealed class AzureOpenAiV1ChatClient : ILLMClient
{
    private readonly HttpClient _client;
    private readonly IConfiguration _configuration;

    public AzureOpenAiV1ChatClient(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _client = httpClientFactory.CreateClient("azureopenai");
        _configuration = configuration;
    }

    public async Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default)
    {
        var endpoint = _configuration["AZURE_OPENAI_ENDPOINT"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT is required.");
        }

        var baseEndpoint = endpoint.TrimEnd('/');
        if (!baseEndpoint.EndsWith("/openai/v1", StringComparison.OrdinalIgnoreCase))
        {
            baseEndpoint = $"{baseEndpoint}/openai/v1";
        }

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"{baseEndpoint}/chat/completions")
        {
            Content = JsonContent.Create(new ChatRequest(
                request.Deployment,
                new List<ChatMessage>
                {
                    new("system", request.SystemPrompt),
                    new("user", request.UserPrompt)
                },
                request.Temperature,
                request.MaxTokens ?? 1200))
        };

        var apiKey = _configuration["AZURE_OPENAI_KEY"];
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            httpRequest.Headers.Add("api-key", apiKey);
        }
        else
        {
            var credential = new DefaultAzureCredential();
            var token = await credential.GetTokenAsync(
                new TokenRequestContext(new[] { "https://cognitiveservices.azure.com/.default" }),
                cancellationToken);
            httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.Token);
        }

        using var response = await _client.SendAsync(httpRequest, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<ChatResponse>(cancellationToken: cancellationToken);
        var text = payload?.Choices?.FirstOrDefault()?.Message?.Content ?? string.Empty;
        var tokens = payload?.Usage?.TotalTokens ?? Math.Max(50, text.Length / 4);

        return new LlmResponse(text, tokens, request.Deployment);
    }

    private sealed record ChatRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] List<ChatMessage> Messages,
        [property: JsonPropertyName("temperature")] double Temperature,
        [property: JsonPropertyName("max_tokens")] int MaxTokens);

    private sealed record ChatMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed record ChatResponse(
        [property: JsonPropertyName("choices")] List<ChatChoice>? Choices,
        [property: JsonPropertyName("usage")] ChatUsage? Usage);

    private sealed record ChatChoice(
        [property: JsonPropertyName("message")] ChatResultMessage? Message);

    private sealed record ChatResultMessage(
        [property: JsonPropertyName("content")] string? Content);

    private sealed record ChatUsage(
        [property: JsonPropertyName("total_tokens")] int? TotalTokens);
}
