using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace SummarizationService.LaneD;

/// <summary>
/// Foundry Agent Service runtime using the OpenAI Responses API.
/// Activated when LANE_D_RUNTIME=foundry.
///
/// Uses the Azure OpenAI base endpoint Responses API (confirmed working):
///   POST {AZURE_OPENAI_ENDPOINT}/openai/responses?api-version=2025-03-01-preview
///
/// The project-scoped endpoint (/api/projects/{name}) rejects all api-version values.
/// The base endpoint with api-version=2025-03-01-preview and model=gpt-4o returns HTTP 200.
///
/// This is a single synchronous call — no threads, no polling.
/// Mirrors what AIProjectClient.OpenAI.GetProjectResponsesClientForAgent().CreateResponse() does.
/// </summary>
public sealed class FoundryAgentRuntime : ILaneDAgentRuntime
{
    private readonly HttpClient _http;
    private readonly IConfiguration _configuration;
    private readonly ILogger<FoundryAgentRuntime> _logger;

    // Use the base Azure OpenAI endpoint — project endpoint rejects all api-versions
    private string BaseEndpoint => _configuration["AZURE_OPENAI_ENDPOINT"]
        ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT is required.");

    // Use the standard deployment (gpt-4o) — Foundry agents are not accessible at the base endpoint level
    private string ModelDeployment => _configuration["AZURE_OPENAI_DEPLOYMENT_PROMPT"]
        ?? throw new InvalidOperationException("AZURE_OPENAI_DEPLOYMENT_PROMPT is required.");

    // 2025-03-01-preview is confirmed working with the Responses API on the base endpoint
    private string ApiVersion => _configuration["FOUNDRY_API_VERSION"] ?? "2025-03-01-preview";

    public FoundryAgentRuntime(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<FoundryAgentRuntime> logger)
    {
        _http = httpClientFactory.CreateClient("foundry");
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<AgentResponse> RunAsync(AgentRequest request, CancellationToken cancellationToken)
    {
        SetAuthHeader();

        var endpoint = BaseEndpoint.TrimEnd('/');

        // OpenAI Responses API — single call, no polling required
        // Confirmed working: base endpoint + gpt-4o + api-version=2025-03-01-preview → HTTP 200
        var requestBody = new
        {
            model = ModelDeployment,
            input = new[]
            {
                new { role = "system", content = request.SystemPrompt },
                new { role = "user",   content = request.UserPrompt   }
            },
            temperature       = request.Temperature,
            max_output_tokens = request.MaxTokens
        };

        _logger.LogInformation("[LaneD/Foundry] Model={Model} → POST {Endpoint}/openai/responses?api-version={ApiVersion}",
            ModelDeployment, endpoint, ApiVersion);

        using var resp = await _http.PostAsJsonAsync(
            $"{endpoint}/openai/responses?api-version={ApiVersion}",
            requestBody,
            cancellationToken);

        if (!resp.IsSuccessStatusCode)
        {
            var errorBody = await resp.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("[LaneD/Foundry] CreateResponse failed {Status}: {Body}",
                (int)resp.StatusCode, errorBody);
            resp.EnsureSuccessStatusCode();
        }

        var result = await resp.Content.ReadFromJsonAsync<FoundryResponseResult>(
                         cancellationToken: cancellationToken)
                     ?? throw new InvalidOperationException("Foundry Responses API returned null.");

        _logger.LogInformation("[LaneD/Foundry] Agent={Agent} ResponseId={Id} Status={Status}",
            request.AgentName, result.Id, result.Status);

        // Extract text from output[].content[type=output_text].text
        var text = result.Output?
            .Where(o => string.Equals(o.Type, "message", StringComparison.OrdinalIgnoreCase))
            .SelectMany(o => o.Content ?? [])
            .Where(c => string.Equals(c.Type, "output_text", StringComparison.OrdinalIgnoreCase))
            .Select(c => c.Text)
            .FirstOrDefault(t => !string.IsNullOrWhiteSpace(t))
            ?? string.Empty;

        return new AgentResponse(text, 0, request.AgentName);
    }

    private void SetAuthHeader()
    {
        // Prefer FOUNDRY_API_KEY if set, fall back to AZURE_OPENAI_KEY (same key in practice)
        var apiKey = _configuration["FOUNDRY_API_KEY"]
                  ?? _configuration["AZURE_OPENAI_KEY"];
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            _http.DefaultRequestHeaders.Remove("api-key");
            _http.DefaultRequestHeaders.Add("api-key", apiKey);
        }
    }

    // ─── Response shapes ─────────────────────────────────────────────────────

    private sealed record FoundryResponseResult(
        [property: JsonPropertyName("id")]     string Id,
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("output")] List<FoundryOutputItem>? Output);

    private sealed record FoundryOutputItem(
        [property: JsonPropertyName("type")]    string Type,
        [property: JsonPropertyName("content")] List<FoundryContentItem>? Content);

    private sealed record FoundryContentItem(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("text")] string? Text);
}
