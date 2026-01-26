using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;

namespace SummarizationService.Llm;

public sealed class AzureOpenAiClient : ILLMClient
{
    private readonly OpenAIClient _client;

    public AzureOpenAiClient(IConfiguration configuration)
    {
        var endpoint = configuration["AZURE_OPENAI_ENDPOINT"] ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT is required.");
        var apiKey = configuration["AZURE_OPENAI_KEY"];
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            _client = new OpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
        }
        else
        {
            var clientId = configuration["AZURE_CLIENT_ID"];
            var credential = string.IsNullOrWhiteSpace(clientId)
                ? new DefaultAzureCredential()
                : new DefaultAzureCredential(new DefaultAzureCredentialOptions { ManagedIdentityClientId = clientId });
            _client = new OpenAIClient(new Uri(endpoint), credential);
        }
    }

    public async Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default)
    {
        var options = new CompletionsOptions
        {
            DeploymentName = request.Deployment,
            Temperature = (float)request.Temperature,
            MaxTokens = request.MaxTokens ?? 1200
        };
        options.Prompts.Add($"{request.SystemPrompt}\n{request.UserPrompt}");

        var response = await _client.GetCompletionsAsync(options);
        var completion = response.Value.Choices.FirstOrDefault()?.Text ?? string.Empty;

        var tokens = response.Value.Usage?.TotalTokens ?? completion.Length / 4;
        return new LlmResponse(completion, tokens, request.Deployment);
    }
}
