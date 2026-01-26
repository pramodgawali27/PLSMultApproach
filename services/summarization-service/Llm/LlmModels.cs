namespace SummarizationService.Llm;

public sealed record LlmRequest(
    string SystemPrompt,
    string UserPrompt,
    string Deployment,
    double Temperature,
    int? MaxTokens = null);

public sealed record LlmResponse(string Text, int TokensEstimated, string ModelDeployment);

public interface ILLMClient
{
    Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default);
}
