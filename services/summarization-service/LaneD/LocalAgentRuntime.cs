using SummarizationService.Llm;

namespace SummarizationService.LaneD;

/// <summary>
/// Local agent runtime: routes AgentRequest directly to the configured ILLMClient.
/// Used when LANE_D_RUNTIME=local (default). No external agent service required.
/// </summary>
public sealed class LocalAgentRuntime : ILaneDAgentRuntime
{
    private readonly ILLMClient _llmClient;
    private readonly IConfiguration _configuration;

    public LocalAgentRuntime(ILLMClient llmClient, IConfiguration configuration)
    {
        _llmClient = llmClient;
        _configuration = configuration;
    }

    public async Task<AgentResponse> RunAsync(AgentRequest request, CancellationToken cancellationToken)
    {
        var deployment = _configuration["AZURE_OPENAI_DEPLOYMENT_PROMPT"]
            ?? throw new InvalidOperationException("AZURE_OPENAI_DEPLOYMENT_PROMPT is required.");

        var llmRequest = new LlmRequest(
            request.SystemPrompt,
            request.UserPrompt,
            deployment,
            request.Temperature,
            request.MaxTokens);

        var response = await _llmClient.CompleteAsync(llmRequest, cancellationToken);

        return new AgentResponse(response.Text, response.TokensEstimated, request.AgentName);
    }
}
