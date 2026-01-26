using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace SummarizationService.Llm;

public sealed class SemanticKernelLlmClient : ILLMClient
{
    private readonly Kernel _kernel;

    public SemanticKernelLlmClient(Kernel kernel)
    {
        _kernel = kernel;
    }

    public async Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default)
    {
        var chat = _kernel.GetRequiredService<IChatCompletionService>();
        var history = new ChatHistory();
        history.AddSystemMessage(request.SystemPrompt);
        history.AddUserMessage(request.UserPrompt);

        var settings = new OpenAIPromptExecutionSettings
        {
            Temperature = request.Temperature,
            MaxTokens = request.MaxTokens ?? 1200
        };

        var result = await chat.GetChatMessageContentsAsync(history, settings, _kernel, cancellationToken);
        var text = result.FirstOrDefault()?.Content ?? string.Empty;
        var tokens = Math.Max(50, text.Length / 4);

        return new LlmResponse(text, tokens, request.Deployment);
    }
}
