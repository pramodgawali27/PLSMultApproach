using Microsoft.AspNetCore.Mvc;
using PLS.BuildingBlocks.Correlation;
using PLS.BuildingBlocks.Results;
using PLS.Contracts;
using SummarizationService.Guardrails;
using SummarizationService.Llm;
using SummarizationService.Extraction;
using SummarizationService.Images;
using SummarizationService.Prompting;
using SummarizationService.Storage;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient("docling");
builder.Services.AddHttpClient("azureopenai");
builder.Services.AddHttpClient("ollama");
var storageProvider = builder.Configuration["Storage:Provider"] ?? "Local";
if (storageProvider.Equals("Blob", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<IStorageReader, AzureBlobStorageReader>();
}
else
{
    builder.Services.AddSingleton<IStorageReader, LocalStorageReader>();
}
builder.Services.AddSingleton<CompositeTextExtractor>();
builder.Services.AddSingleton<DoclingTextExtractor>();
builder.Services.AddSingleton<ITextExtractor, HybridTextExtractor>();
builder.Services.AddSingleton<IImageGenerator, AzureOpenAiImageGenerator>();
builder.Services.AddSingleton<PromptTemplateStore>();
var llmProvider = builder.Configuration["LLM_PROVIDER"] ?? "azure";
if (llmProvider.Equals("ollama", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<ILLMClient, OllamaLlmClient>();
}
else
{
    builder.Services.AddSingleton<ILLMClient, AzureOpenAiV1ChatClient>();
}
builder.Services.AddSingleton(GuardrailPolicyLoader.Load(builder.Configuration));
builder.Services.AddSingleton<GuardrailEvaluator>();

var app = builder.Build();

app.UseCorrelationId();

if (app.Environment.IsDevelopment())
{
    // OpenAPI is optional in this starter.
}

app.MapPost("/internal/summarize", async (
    [FromBody] SummarizeRequest request,
    ITextExtractor extractor,
    PromptTemplateStore templates,
    GuardrailEvaluator guardrails,
    ILLMClient llm,
    IImageGenerator imageGenerator,
    IConfiguration configuration,
    CancellationToken cancellationToken) =>
{
    if (request.Lane != SummaryLane.LaneA)
    {
        return Results.BadRequest("Lane not yet implemented.");
    }

    var extraction = await extractor.ExtractAsync(request.DocumentId, request.StorageUri, request.ContentType);
    if (!extraction.IsSuccess || extraction.Value is null)
    {
        return Results.BadRequest(extraction.Error ?? "Failed to extract text.");
    }

    var savedImages = ImagePersistence.Save(extraction.Value.Images, request.DocumentId, request.StorageUri, configuration);

    var provider = configuration["LLM_PROVIDER"] ?? "azure";
    var guardrailResult = guardrails.Evaluate(request.Lane, extraction.Value.Text);
    if (!provider.Equals("ollama", StringComparison.OrdinalIgnoreCase)
        && guardrailResult.FinalDecision == "block")
    {
        return Results.BadRequest("Guardrail policy blocked summarization.");
    }

    var promptModel = new { text = extraction.Value.Text };
    var usedPromptOverride = !string.IsNullOrWhiteSpace(request.PromptOverride);
    var template = usedPromptOverride
        ? templates.RenderContent(request.PromptOverride!, promptModel)
        : templates.Render(Path.Combine("lane-a", "summary.md.sbn"), promptModel);
    var systemPrompt = "You are a helpful medical summarizer. Do not provide medical advice.";

    var deployment = configuration["AZURE_OPENAI_DEPLOYMENT_PROMPT"]
        ?? throw new InvalidOperationException("AZURE_OPENAI_DEPLOYMENT_PROMPT is required.");

    var response = await llm.CompleteAsync(
        new LlmRequest(systemPrompt, template, deployment, 0.2, 1400),
        cancellationToken);

    var parsed = ImagePromptParser.Parse(response.Text);
    var generatedImages = await ImageGenerationPipeline.GenerateAsync(
        parsed.ImagePrompts,
        request.DocumentId,
        request.StorageUri,
        imageGenerator,
        configuration,
        cancellationToken);

    var summaryWithPlaceholders = SummaryPostProcessor.EnsureFigurePlaceholders(parsed.SummaryText, parsed.ImagePrompts);
    var enrichedSummary = SummaryPostProcessor.ReplaceFigurePlaceholders(summaryWithPlaceholders, generatedImages);
    enrichedSummary = SummaryPostProcessor.NormalizeImageMarkdown(enrichedSummary);

    var modelInfo = new
    {
        modelDeployment = response.ModelDeployment,
        tokensEstimated = response.TokensEstimated,
        promptTemplateId = "lane-a/summary",
        promptOverrideUsed = usedPromptOverride,
        guardrails = new
        {
            finalDecision = guardrailResult.FinalDecision,
            findings = guardrailResult.Findings
        },
        extraction = new
        {
            source = extraction.Value.Source,
            images = savedImages.Select(image => new
            {
                filePath = image.FilePath,
                contentType = image.ContentType,
                caption = image.Caption
            })
        },
        generatedImages = generatedImages.Select(image => new
        {
            filePath = image.FilePath,
            contentType = image.ContentType,
            caption = image.Caption,
            figureId = image.FigureId
        }),
        imagePrompts = parsed.ImagePrompts.Select(prompt => new
        {
            id = prompt.FigureId,
            title = prompt.Title,
            placement = prompt.Placement,
            style = prompt.Style,
            aspectRatio = prompt.AspectRatio
        })
    };

    return Results.Ok(new SummarizeResponse(enrichedSummary, System.Text.Json.JsonSerializer.Serialize(modelInfo)));
});

app.MapGet("/health", () => Results.Ok("ok"));

app.Run();

sealed record SummarizeRequest(Guid DocumentId, SummaryLane Lane, string StorageUri, string ContentType, string? PromptOverride);

sealed record SummarizeResponse(string SummaryText, string? ModelInfoJson);

static class SummaryPostProcessor
{
    public static string EnsureFigurePlaceholders(string summary, IReadOnlyList<ImagePrompt> prompts)
    {
        if (string.IsNullOrWhiteSpace(summary) || prompts.Count == 0)
        {
            return summary;
        }

        var output = summary;
        foreach (var prompt in prompts)
        {
            if (string.IsNullOrWhiteSpace(prompt.FigureId))
            {
                continue;
            }

            var placeholder = $"[[FIGURE:{prompt.FigureId}]]";
            if (output.Contains(placeholder, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var heading = prompt.Placement?.Trim();
            if (!string.IsNullOrWhiteSpace(heading))
            {
                output = InsertPlaceholderAfterHeading(output, heading, placeholder);
            }
            else
            {
                output = $"{output}\n\n{placeholder}";
            }
        }

        return output;
    }

    public static string ReplaceFigurePlaceholders(string summary, IReadOnlyList<GeneratedImageFile> images)
    {
        if (string.IsNullOrWhiteSpace(summary) || images.Count == 0)
        {
            return summary;
        }

        var output = summary;
        foreach (var image in images)
        {
            if (string.IsNullOrWhiteSpace(image.FigureId))
            {
                continue;
            }

            var placeholder = $"[[FIGURE:{image.FigureId}]]";
            var alt = string.IsNullOrWhiteSpace(image.Caption) ? "Figure" : image.Caption;
            var replacement = $"![{alt}]({image.FilePath})";
            output = output.Replace(placeholder, replacement, StringComparison.OrdinalIgnoreCase);
        }

        return output;
    }

    private static string InsertPlaceholderAfterHeading(string summary, string headingText, string placeholder)
    {
        var lines = summary.Split('\n').ToList();
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i].Trim();
            if (line.StartsWith("## ", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("### ", StringComparison.OrdinalIgnoreCase))
            {
                var lineHeading = line.TrimStart('#', ' ').Trim();
                if (lineHeading.Equals(headingText, StringComparison.OrdinalIgnoreCase))
                {
                    lines.Insert(i + 1, placeholder);
                    return string.Join('\n', lines);
                }
            }
        }

        return $"{summary}\n\n{placeholder}";
    }

    public static string NormalizeImageMarkdown(string summary)
    {
        if (string.IsNullOrWhiteSpace(summary))
        {
            return summary;
        }

        // Collapse newlines inside markdown image URLs: ![alt](url)
        return System.Text.RegularExpressions.Regex.Replace(
            summary,
            @"!\[(?<alt>[^\]]*)\]\((?<url>[\s\S]*?)\)",
            match =>
            {
                var alt = match.Groups["alt"].Value;
                var url = match.Groups["url"].Value.Replace("\r", string.Empty).Replace("\n", string.Empty).Trim();
                return $"![{alt}]({url})";
            });
    }
}

static class ImagePersistence
{
    public static IReadOnlyList<SavedImage> Save(
        IReadOnlyList<ExtractedImage> images,
        Guid documentId,
        string storageUri,
        IConfiguration configuration)
    {
        if (images.Count == 0)
        {
            return Array.Empty<SavedImage>();
        }

        var storageProvider = configuration["Storage:Provider"] ?? "Local";
        var useBlob = storageProvider.Equals("Blob", StringComparison.OrdinalIgnoreCase);
        var containerName = configuration["Storage:Blob:Container"];
        var connectionString = configuration["Storage:Blob:ConnectionString"];
        var directory = useBlob
            ? SummarizationService.Storage.StoragePathHelper.GetBlobDirectory(
                SummarizationService.Storage.StoragePathHelper.ResolveBlobName(storageUri, containerName ?? string.Empty))
            : Path.GetDirectoryName(storageUri);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return Array.Empty<SavedImage>();
        }

        var container = useBlob && !string.IsNullOrWhiteSpace(connectionString) && !string.IsNullOrWhiteSpace(containerName)
            ? new Azure.Storage.Blobs.BlobContainerClient(connectionString, containerName)
            : null;
        var saved = new List<SavedImage>();
        for (var i = 0; i < images.Count; i++)
        {
            var image = images[i];
            var extension = ExtensionFromContentType(image.ContentType);
            string filePath;
            if (useBlob && container is not null)
            {
                var blobName = $"{directory}/{documentId}-image-{i + 1}{extension}";
                var blob = container.GetBlobClient(blobName);
                blob.Upload(new BinaryData(image.Content), overwrite: true);
                filePath = blob.Uri.ToString();
            }
            else
            {
                filePath = Path.Combine(directory, $"{documentId}-image-{i + 1}{extension}");
                File.WriteAllBytes(filePath, image.Content);
            }
            saved.Add(new SavedImage(filePath, image.ContentType, image.Caption));
        }

        return saved;
    }

    private static string ExtensionFromContentType(string contentType)
    {
        return contentType switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            "image/gif" => ".gif",
            _ => ".bin"
        };
    }
}

sealed record SavedImage(string FilePath, string ContentType, string? Caption);
