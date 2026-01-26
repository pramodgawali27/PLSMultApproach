using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace SummarizationService.KernelRuntime;

public sealed class SemanticKernelFactory
{
    private readonly IConfiguration _configuration;

    public SemanticKernelFactory(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public Microsoft.SemanticKernel.Kernel CreateKernel()
    {
        var builder = Microsoft.SemanticKernel.Kernel.CreateBuilder();
        var endpoint = _configuration["AZURE_OPENAI_ENDPOINT"] ?? string.Empty;
        var deployment = _configuration["AZURE_OPENAI_DEPLOYMENT_AGENT"] ?? string.Empty;
        var apiKey = _configuration["AZURE_OPENAI_KEY"];

        if (!string.IsNullOrWhiteSpace(endpoint) && !string.IsNullOrWhiteSpace(deployment) && !string.IsNullOrWhiteSpace(apiKey))
        {
            builder.AddAzureOpenAIChatCompletion(deployment, endpoint, apiKey);
        }

        return builder.Build();
    }
}
