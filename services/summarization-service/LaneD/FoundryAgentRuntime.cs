using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace SummarizationService.LaneD;

/// <summary>
/// Foundry Agent Service runtime using the Azure OpenAI Responses API.
/// Activated when LANE_D_RUNTIME=foundry.
///
/// Uses the base Azure OpenAI endpoint (confirmed working):
///   POST {AZURE_OPENAI_ENDPOINT}/openai/responses?api-version=2025-03-01-preview
///
/// NOTE: The published application endpoint
///   ({FOUNDRY_PROJECT_ENDPOINT}/applications/{FOUNDRY_AGENT_ID}/protocols/openai/responses)
///   requires Azure RBAC (Microsoft.MachineLearningServices/workspaces/agents/action) and
///   does NOT accept API key auth alone. To use it, configure AZURE_CLIENT_ID with a service
///   principal that has the "Azure AI Inference Deployment Operator" role.
///   Until then, the base endpoint with gpt-4o is used.
///
/// This is a single synchronous call — no threads, no polling.
/// </summary>
public sealed class FoundryAgentRuntime : ILaneDAgentRuntime
{
    private readonly HttpClient _http;
    private readonly IConfiguration _configuration;
    private readonly ILogger<FoundryAgentRuntime> _logger;

    // Base Azure OpenAI endpoint — accepts api-key auth and confirmed working
    private string BaseEndpoint => _configuration["AZURE_OPENAI_ENDPOINT"]
        ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT is required.");

    // gpt-4o deployment — used until Azure AD auth is configured for the published PLS-Agent app
    private string ModelDeployment => _configuration["AZURE_OPENAI_DEPLOYMENT_PROMPT"]
        ?? throw new InvalidOperationException("AZURE_OPENAI_DEPLOYMENT_PROMPT is required.");

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
        // Retry up to 3 times for transient network errors (ResponseEnded, ServiceUnavailable, etc.)
        const int MaxAttempts = 3;
        var delay = TimeSpan.FromSeconds(3);

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                return await CallOnceAsync(request, cancellationToken);
            }
            catch (HttpRequestException ex) when (attempt < MaxAttempts && IsTransient(ex))
            {
                _logger.LogWarning(
                    "[LaneD/Foundry] Agent={Agent} attempt {Attempt}/{Max} failed ({Msg}). Retrying in {Delay}s…",
                    request.AgentName, attempt, MaxAttempts, ex.Message, delay.TotalSeconds);
                await Task.Delay(delay, cancellationToken);
                delay = TimeSpan.FromSeconds(delay.TotalSeconds * 2); // exponential back-off
            }
        }

        // Final attempt — let any exception propagate
        return await CallOnceAsync(request, cancellationToken);
    }

    private async Task<AgentResponse> CallOnceAsync(AgentRequest request, CancellationToken cancellationToken)
    {
        SetAuthHeader();

        var endpoint = BaseEndpoint.TrimEnd('/');
        var url = $"{endpoint}/openai/responses?api-version={ApiVersion}";

        var requestBody = new
        {
            model             = ModelDeployment,
            input             = new[]
            {
                new { role = "system", content = request.SystemPrompt },
                new { role = "user",   content = request.UserPrompt   }
            },
            temperature       = request.Temperature,
            max_output_tokens = request.MaxTokens
        };

        _logger.LogInformation("[LaneD/Foundry] Model={Model} → POST {Url}",
            ModelDeployment, url);

        using var resp = await _http.PostAsJsonAsync(
            url,
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

    /// <summary>Returns true for transient HTTP errors that are safe to retry.</summary>
    private static bool IsTransient(HttpRequestException ex)
    {
        // ResponseEnded / connection reset / premature EOF
        if (ex.InnerException is System.Net.Http.HttpIOException)
            return true;

        return ex.StatusCode is
            System.Net.HttpStatusCode.ServiceUnavailable or
            System.Net.HttpStatusCode.TooManyRequests   or
            System.Net.HttpStatusCode.GatewayTimeout    or
            System.Net.HttpStatusCode.BadGateway;
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
