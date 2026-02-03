using System.Net.Http.Json;

namespace SummarizationService.Llm;

public sealed class OllamaLlmClient : ILLMClient
{
    private readonly HttpClient _httpClient;
    private readonly string _model;

    public OllamaLlmClient(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClient = httpClientFactory.CreateClient("ollama");
        var baseUrl = configuration["OLLAMA_BASE_URL"] ?? "http://host.docker.internal:11434";
        _httpClient.BaseAddress = new Uri(baseUrl);
        _model = configuration["OLLAMA_MODEL"] ?? "llama3.1";
    }

    public async Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default)
    {
        var payload = new OllamaChatRequest(
            _model,
            new[]
            {
                new OllamaMessage("system", request.SystemPrompt),
                new OllamaMessage("user", request.UserPrompt)
            },
            false,
            new OllamaOptions(request.Temperature, request.MaxTokens));

        using var response = await _httpClient.PostAsJsonAsync("/api/chat", payload, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(cancellationToken: cancellationToken);
        var text = result?.Message?.Content ?? string.Empty;
        var tokens = Math.Max(50, text.Length / 4);

        return new LlmResponse(text, tokens, _model);
    }

    private sealed record OllamaChatRequest(
        string Model,
        IReadOnlyList<OllamaMessage> Messages,
        bool Stream,
        OllamaOptions Options);

    private sealed record OllamaMessage(string Role, string Content);

    private sealed record OllamaOptions(double Temperature, int? NumPredict);

    private sealed record OllamaChatResponse(OllamaMessage Message);
}
