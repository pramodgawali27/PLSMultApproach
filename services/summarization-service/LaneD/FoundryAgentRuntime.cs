using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Logging;

namespace SummarizationService.LaneD;

/// <summary>
/// Foundry Agent Service runtime: uses Azure AI Foundry threads/runs/tool-loop pattern.
/// Activated when LANE_D_RUNTIME=foundry.
///
/// Flow per stage:
///   1. CreateThread + AddMessage + CreateRun (one call via createThreadAndRun)
///   2. Poll until terminal (completed | failed | expired)
///   3. If requires_action → execute tools → SubmitToolOutputs → continue polling
///   4. List messages → return assistant text
/// </summary>
public sealed class FoundryAgentRuntime : ILaneDAgentRuntime
{
    private readonly HttpClient _http;
    private readonly IConfiguration _configuration;
    private readonly ILogger<FoundryAgentRuntime> _logger;

    private string ProjectEndpoint => _configuration["FOUNDRY_PROJECT_ENDPOINT"]
        ?? throw new InvalidOperationException("FOUNDRY_PROJECT_ENDPOINT is required.");

    private string AgentId => _configuration["FOUNDRY_AGENT_ID"]
        ?? throw new InvalidOperationException("FOUNDRY_AGENT_ID is required.");

    private string ApiVersion => _configuration["FOUNDRY_API_VERSION"] ?? "v1";

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
        await SetAuthHeaderAsync(cancellationToken);

        var endpoint = ProjectEndpoint.TrimEnd('/');

        // Step 1: Create thread and run in one call
        var createBody = new
        {
            assistant_id = AgentId,
            thread = new
            {
                messages = new[]
                {
                    new { role = "user", content = $"[SYSTEM]\n{request.SystemPrompt}\n\n[USER]\n{request.UserPrompt}" }
                }
            },
            temperature = request.Temperature,
            max_completion_tokens = request.MaxTokens
        };

        using var createResp = await _http.PostAsJsonAsync(
            $"{endpoint}/threads/runs?api-version={ApiVersion}",
            createBody,
            cancellationToken);
        createResp.EnsureSuccessStatusCode();

        var createPayload = await createResp.Content.ReadFromJsonAsync<FoundryRunResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Foundry createThreadAndRun returned null.");

        var threadId = createPayload.ThreadId;
        var runId = createPayload.Id;

        _logger.LogInformation("[LaneD/Foundry] Agent={Agent} ThreadId={ThreadId} RunId={RunId} created.",
            request.AgentName, threadId, runId);

        // Step 2: Poll until terminal state
        var text = await PollUntilCompleteAsync(endpoint, threadId, runId, cancellationToken);

        return new AgentResponse(text, 0, request.AgentName);
    }

    private async Task<string> PollUntilCompleteAsync(
        string endpoint,
        string threadId,
        string runId,
        CancellationToken cancellationToken)
    {
        const int maxPollAttempts = 120;
        const int pollDelayMs = 2000;

        for (var attempt = 0; attempt < maxPollAttempts; attempt++)
        {
            await Task.Delay(pollDelayMs, cancellationToken);

            using var pollResp = await _http.GetAsync(
                $"{endpoint}/threads/{threadId}/runs/{runId}?api-version={ApiVersion}",
                cancellationToken);
            pollResp.EnsureSuccessStatusCode();

            var run = await pollResp.Content.ReadFromJsonAsync<FoundryRunResponse>(cancellationToken: cancellationToken);
            if (run is null) continue;

            _logger.LogDebug("[LaneD/Foundry] RunId={RunId} Status={Status}", runId, run.Status);

            switch (run.Status)
            {
                case "completed":
                    return await GetLastAssistantMessageAsync(endpoint, threadId, cancellationToken);

                case "requires_action":
                    // Tool loop: execute tools and submit outputs
                    await HandleToolCallsAsync(endpoint, threadId, runId, run, cancellationToken);
                    break;

                case "failed":
                case "expired":
                case "cancelled":
                    var errMsg = run.LastError?.Message ?? run.Status;
                    throw new InvalidOperationException($"Foundry run {runId} ended with status '{run.Status}': {errMsg}");
            }
        }

        throw new TimeoutException($"Foundry run {runId} did not complete within the polling window.");
    }

    private async Task HandleToolCallsAsync(
        string endpoint,
        string threadId,
        string runId,
        FoundryRunResponse run,
        CancellationToken cancellationToken)
    {
        var toolCalls = run.RequiredAction?.SubmitToolOutputs?.ToolCalls ?? Array.Empty<FoundryToolCall>();
        var outputs = new List<object>();

        foreach (var call in toolCalls)
        {
            _logger.LogInformation("[LaneD/Foundry] Tool call: {ToolName} ({CallId})", call.Function?.Name, call.Id);
            // Tool execution is handled inline in the orchestrator via AgentTool delegation.
            // For now, return an empty output to unblock the run.
            outputs.Add(new { tool_call_id = call.Id, output = "{}" });
        }

        var submitBody = new { tool_outputs = outputs };
        using var submitResp = await _http.PostAsJsonAsync(
            $"{endpoint}/threads/{threadId}/runs/{runId}/submit_tool_outputs?api-version={ApiVersion}",
            submitBody,
            cancellationToken);
        submitResp.EnsureSuccessStatusCode();
    }

    private async Task<string> GetLastAssistantMessageAsync(
        string endpoint,
        string threadId,
        CancellationToken cancellationToken)
    {
        using var listResp = await _http.GetAsync(
            $"{endpoint}/threads/{threadId}/messages?api-version={ApiVersion}&order=desc&limit=10",
            cancellationToken);
        listResp.EnsureSuccessStatusCode();

        var messages = await listResp.Content.ReadFromJsonAsync<FoundryMessageList>(cancellationToken: cancellationToken);
        var assistant = messages?.Data?.FirstOrDefault(m =>
            string.Equals(m.Role, "assistant", StringComparison.OrdinalIgnoreCase));

        return assistant?.Content?.FirstOrDefault()?.Text?.Value ?? string.Empty;
    }

    private async Task SetAuthHeaderAsync(CancellationToken cancellationToken)
    {
        var apiKey = _configuration["FOUNDRY_API_KEY"];
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            _http.DefaultRequestHeaders.Remove("api-key");
            _http.DefaultRequestHeaders.Add("api-key", apiKey);
            return;
        }

        var credential = new DefaultAzureCredential();
        var token = await credential.GetTokenAsync(
            new TokenRequestContext(new[] { "https://ai.azure.com/.default" }),
            cancellationToken);
        _http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.Token);
    }

    // ─── Foundry REST response shapes ───────────────────────────────────────────

    private sealed record FoundryRunResponse(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("thread_id")] string ThreadId,
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("required_action")] FoundryRequiredAction? RequiredAction,
        [property: JsonPropertyName("last_error")] FoundryLastError? LastError);

    private sealed record FoundryRequiredAction(
        [property: JsonPropertyName("submit_tool_outputs")] FoundrySubmitToolOutputs? SubmitToolOutputs);

    private sealed record FoundrySubmitToolOutputs(
        [property: JsonPropertyName("tool_calls")] FoundryToolCall[] ToolCalls);

    private sealed record FoundryToolCall(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("function")] FoundryFunction? Function);

    private sealed record FoundryFunction(
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("arguments")] string? Arguments);

    private sealed record FoundryLastError(
        [property: JsonPropertyName("message")] string? Message);

    private sealed record FoundryMessageList(
        [property: JsonPropertyName("data")] List<FoundryMessage>? Data);

    private sealed record FoundryMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] List<FoundryMessageContent>? Content);

    private sealed record FoundryMessageContent(
        [property: JsonPropertyName("text")] FoundryTextValue? Text);

    private sealed record FoundryTextValue(
        [property: JsonPropertyName("value")] string? Value);
}
