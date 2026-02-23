using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PLS.BuildingBlocks.Correlation;
using PLS.BuildingBlocks.Data;
using PLS.BuildingBlocks.Results;
using PLS.Contracts;
using SummarizationService.Diagnostics;
using SummarizationService.Extraction;
using SummarizationService.Guardrails;
using SummarizationService.Images;
using SummarizationService.LaneD;
using SummarizationService.Llm;
using SummarizationService.Prompting;
using SummarizationService.Rag;
using SummarizationService.Storage;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient("docling", client =>
{
    var timeoutSeconds = builder.Configuration.GetValue("DOCLING_TIMEOUT_SECONDS", 120);
    client.Timeout = TimeSpan.FromSeconds(Math.Clamp(timeoutSeconds, 5, 600));
});
builder.Services.AddHttpClient("azureopenai");
builder.Services.AddHttpClient("ollama");
builder.Services.AddDbContext<PlsDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("Postgres")
        ?? "Host=localhost;Port=5432;Database=pls;Username=pls;Password=pls";
    options.UseNpgsql(connectionString);
});
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
builder.Services.AddSingleton<TextChunker>();
var ragVectorStore = builder.Configuration["RAG_VECTORSTORE"] ?? "memory";
var ragEmbeddings = builder.Configuration["RAG_EMBEDDINGS"] ?? "azure";
var useAzureVectorStore = ragVectorStore.Equals("azure", StringComparison.OrdinalIgnoreCase);
var useLocalEmbeddings = ragEmbeddings.Equals("local", StringComparison.OrdinalIgnoreCase);
if (!useLocalEmbeddings)
{
    var endpoint = builder.Configuration["AZURE_OPENAI_ENDPOINT"];
    var deployment = builder.Configuration["AZURE_OPENAI_EMBEDDING_DEPLOYMENT"];
    if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(deployment))
    {
        if (useAzureVectorStore)
        {
            throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT and AZURE_OPENAI_EMBEDDING_DEPLOYMENT are required for Azure vector store.");
        }

        useLocalEmbeddings = true;
    }
}

if (useLocalEmbeddings)
{
    builder.Services.AddSingleton<IEmbeddingClient, FakeEmbeddingClient>();
}
else
{
    builder.Services.AddSingleton<IEmbeddingClient, AzureOpenAiEmbeddingClient>();
}

builder.Services.AddSingleton<IVectorStore>(sp => useAzureVectorStore
    ? new AzureAiSearchVectorStore(sp.GetRequiredService<IConfiguration>())
    : new InMemoryVectorStore());
builder.Services.AddScoped<IRagRetriever, RagRetriever>();
builder.Services.AddScoped<RagIndexer>();
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

// ── Lane D: Agentic runtime ──────────────────────────────────────────────────
builder.Services.AddHttpClient("foundry");
var laneDRuntime = builder.Configuration["LANE_D_RUNTIME"] ?? "local";
if (laneDRuntime.Equals("foundry", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<ILaneDAgentRuntime, FoundryAgentRuntime>();
}
else
{
    builder.Services.AddSingleton<ILaneDAgentRuntime, LocalAgentRuntime>();
}
builder.Services.AddScoped<LaneDRouter>();

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
    PlsDbContext db,
    IRagRetriever ragRetriever,
    RagIndexer indexer,
    LaneDRouter laneDRouter,
    IConfiguration configuration,
    CancellationToken cancellationToken) =>
{
    // ── Lane D: Agentic multi-step ───────────────────────────────────────────
    if (request.Lane == SummaryLane.LaneD)
    {
        var laneDDoc = await db.Documents.FirstOrDefaultAsync(
            x => x.Id == request.DocumentId, cancellationToken);
        if (laneDDoc is null)
            return Results.BadRequest("Document not found.");

        var laneDCtx = new LaneDContext(
            JobId: Guid.NewGuid(), // orchestrator populates real jobId; ok as correlation here
            DocumentId: request.DocumentId,
            TenantId: laneDDoc.TenantId,
            UserId: laneDDoc.UserId,
            StorageUri: request.StorageUri,
            ContentType: request.ContentType,
            Title: laneDDoc.Title,
            TherapeuticArea: laneDDoc.TherapeuticArea,
            Audience: laneDDoc.Audience,
            PromptOverride: request.PromptOverride);

        var laneDResult = await laneDRouter.HandleAsync(laneDCtx, cancellationToken);
        return Results.Ok(new SummarizeResponse(laneDResult.FinalMarkdown, laneDResult.ModelInfoJson));
    }

    if (request.Lane != SummaryLane.LaneA && request.Lane != SummaryLane.LaneC)
    {
        return Results.BadRequest("Lane not yet implemented.");
    }

    var trace = RunTrace.Create(configuration, request.DocumentId, "summarize");
    trace?.WriteJson("00_request.json", new
    {
        request.DocumentId,
        request.Lane,
        request.StorageUri,
        request.ContentType,
        request.PromptOverride
    });

    var extraction = await extractor.ExtractAsync(request.DocumentId, request.StorageUri, request.ContentType);
    if (!extraction.IsSuccess || extraction.Value is null)
    {
        return Results.BadRequest(extraction.Error ?? "Failed to extract text.");
    }
    trace?.WriteText("01_extraction.txt", extraction.Value.Text);

    var savedImages = ImagePersistence.Save(extraction.Value.Images, request.DocumentId, request.StorageUri, configuration);

    var provider = configuration["LLM_PROVIDER"] ?? "azure";
    var guardrailResult = guardrails.Evaluate(request.Lane, extraction.Value.Text);
    if (!provider.Equals("ollama", StringComparison.OrdinalIgnoreCase)
        && guardrailResult.FinalDecision == "block")
    {
        return Results.BadRequest("Guardrail policy blocked summarization.");
    }

    var usedPromptOverride = !string.IsNullOrWhiteSpace(request.PromptOverride);
    string template;
    string systemPrompt;
    RagContext? ragContext = null;
    Document? document = null;

    if (request.Lane == SummaryLane.LaneC)
    {
        document = await db.Documents.FirstOrDefaultAsync(x => x.Id == request.DocumentId, cancellationToken);
        if (document is null)
        {
            return Results.BadRequest("Document not found for retrieval.");
        }

        ragContext = await ragRetriever.RetrieveForPlsAsync(request.DocumentId, document.TenantId, cancellationToken);
        if (ragContext.Chunks.Count == 0)
        {
            var autoIndex = configuration.GetValue("RAG_AUTO_INDEX", true);
            if (autoIndex)
            {
                await indexer.IndexAsync(request.DocumentId, document.TenantId, request.StorageUri, request.ContentType, cancellationToken);
                ragContext = await ragRetriever.RetrieveForPlsAsync(request.DocumentId, document.TenantId, cancellationToken);
            }

            if (ragContext.Chunks.Count == 0)
            {
                return Results.BadRequest("No context available for retrieval.");
            }
        }
        trace?.WriteJson("02_rag_context.json", new
        {
            queries = ragContext.Queries,
            chunks = ragContext.Chunks.Select(chunk => new
            {
                chunk.ChunkId,
                chunk.Score,
                chunk.Page,
                chunk.Section,
                chunk.Content
            })
        });

        var context = RagPromptBuilder.BuildContext(ragContext);
        var metadata = new
        {
            title = document.Title,
            therapeuticArea = document.TherapeuticArea,
            audience = document.Audience
        };
        template = usedPromptOverride
            ? templates.RenderContent(request.PromptOverride!, new { context, metadata })
            : templates.Render(Path.Combine("lane-c", "summary.md.sbn"), new { context, metadata });
        systemPrompt = RagPromptBuilder.BuildSystemPrompt();
    }
    else
    {
        document ??= await db.Documents.FirstOrDefaultAsync(x => x.Id == request.DocumentId, cancellationToken);
        var promptModel = new
        {
            text = extraction.Value.Text,
            metadata = new
            {
                title = document?.Title,
                therapeuticArea = document?.TherapeuticArea,
                audience = document?.Audience
            }
        };
        template = usedPromptOverride
            ? templates.RenderContent(request.PromptOverride!, promptModel)
            : templates.Render(Path.Combine("lane-a", "summary.md.sbn"), promptModel);
        systemPrompt = "You are a helpful medical summarizer. Do not provide medical advice.";
    }

    var deployment = configuration["AZURE_OPENAI_DEPLOYMENT_PROMPT"]
        ?? throw new InvalidOperationException("AZURE_OPENAI_DEPLOYMENT_PROMPT is required.");

    trace?.WriteText("03_system_prompt.txt", systemPrompt);
    trace?.WriteText("04_prompt.txt", template);

    var response = await llm.CompleteAsync(
        new LlmRequest(systemPrompt, template, deployment, 0.2, 1400),
        cancellationToken);
    trace?.WriteText("05_llm_response.txt", response.Text);

    var parsed = ImagePromptParser.Parse(response.Text);
    if (parsed.ImagePrompts.Count == 0 && configuration.GetValue("IMAGE_PROMPT_FALLBACK", true))
    {
        var imageContext = SummaryPostProcessor.BuildImagePromptContext(ragContext, extraction.Value.Text, configuration);
        if (!string.IsNullOrWhiteSpace(imageContext))
        {
            var imagePromptTemplate = $$"""
You are a medical visualization assistant.
Create 1-2 image prompts grounded in the provided context.
Return ONLY a JSON array matching this schema:
[
  {
    "figure_id": "fig_01",
    "title": "Short title",
    "placement": "Section heading from the summary",
    "rationale": "1-2 lines why it helps",
    "prompt": "A complete DALL·E prompt",
    "style": "diagram",
    "aspect_ratio": "wide",
    "must_include": ["item1", "item2"],
    "must_avoid": ["itemA", "itemB"]
  }
]
Use only facts present in the context. If study design or results are described, prioritize those.
Style requirements:
- choose infographic, diagram, or chart to best match the context
- keep a clean flat style with white rounded cards on a light beige background
- use two accent colors consistently (teal and deep purple) for group labels
- bold numbers and short labels; no logos; no photos; high contrast

Context:
{{imageContext}}
""";

            var imageResponse = await llm.CompleteAsync(
                new LlmRequest("You generate grounded image prompts only.", imagePromptTemplate, deployment, 0.2, 600),
                cancellationToken);
            trace?.WriteText("05b_image_prompt_response.txt", imageResponse.Text);
            var fallbackParsed = ImagePromptParser.Parse(imageResponse.Text);
            if (fallbackParsed.ImagePrompts.Count > 0)
            {
                parsed = new ParsedOutput(parsed.SummaryText, fallbackParsed.ImagePrompts);
            }
        }
    }
    trace?.WriteText("06_parsed_summary.md", parsed.SummaryText);
    trace?.WriteJson("07_image_prompts.json", parsed.ImagePrompts);
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
    if (request.Lane == SummaryLane.LaneC)
    {
        enrichedSummary = SummaryPostProcessor.LinkCitations(enrichedSummary);
    }
    trace?.WriteText("08_enriched_summary.md", enrichedSummary);

    var modelInfo = new
    {
        modelDeployment = response.ModelDeployment,
        tokensEstimated = response.TokensEstimated,
        promptTemplateId = request.Lane == SummaryLane.LaneC ? "lane-c/summary" : "lane-a/summary",
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
            figureId = image.FigureId,
            placement = parsed.ImagePrompts.FirstOrDefault(p => string.Equals(p.FigureId, image.FigureId, StringComparison.OrdinalIgnoreCase))?.Placement
        }),
        imagePrompts = parsed.ImagePrompts.Select(prompt => new
        {
            id = prompt.FigureId,
            title = prompt.Title,
            placement = prompt.Placement,
            style = prompt.Style,
            aspectRatio = prompt.AspectRatio
        }),
        retrieval = ragContext is null ? null : new
        {
            queries = ragContext.Queries,
            chunks = ragContext.Chunks.Select(chunk => new
            {
                chunkId = chunk.ChunkId,
                score = chunk.Score,
                section = chunk.Section,
                page = chunk.Page
            })
        }
    };
    trace?.WriteJson("09_model_info.json", modelInfo);

    return Results.Ok(new SummarizeResponse(enrichedSummary, System.Text.Json.JsonSerializer.Serialize(modelInfo)));
});

app.MapPost("/internal/index", async (
    [FromBody] IndexRequest request,
    RagIndexer indexer,
    CancellationToken cancellationToken) =>
{
    await indexer.IndexAsync(request.DocumentId, request.TenantId, request.StorageUri, request.ContentType, cancellationToken);
    return Results.Ok();
});

app.MapGet("/health", () => Results.Ok("ok"));

app.Run();

sealed record SummarizeRequest(Guid DocumentId, SummaryLane Lane, string StorageUri, string ContentType, string? PromptOverride);

sealed record SummarizeResponse(string SummaryText, string? ModelInfoJson);

sealed record IndexRequest(Guid DocumentId, Guid TenantId, string StorageUri, string ContentType);

static class SummaryPostProcessor
{
    public static string BuildImagePromptContext(RagContext? ragContext, string extractionText, IConfiguration configuration)
    {
        var maxChars = Math.Clamp(configuration.GetValue("IMAGE_PROMPT_CONTEXT_CHARS", 3000), 500, 12000);
        if (ragContext is not null && ragContext.Chunks.Count > 0)
        {
            var chunkText = string.Join("\n\n", ragContext.Chunks
                .Take(4)
                .Select(chunk => $"{chunk.Section}\n{chunk.Content}"));
            return chunkText.Length <= maxChars ? chunkText : chunkText[..maxChars];
        }

        if (string.IsNullOrWhiteSpace(extractionText))
        {
            return string.Empty;
        }

        var trimmed = extractionText.Trim();
        return trimmed.Length <= maxChars ? trimmed : trimmed[..maxChars];
    }

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

            var heading = NormalizePlacement(prompt.Placement);
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
                if (lineHeading.Equals(headingText, StringComparison.OrdinalIgnoreCase)
                    || lineHeading.Contains(headingText, StringComparison.OrdinalIgnoreCase)
                    || headingText.Contains(lineHeading, StringComparison.OrdinalIgnoreCase))
                {
                    lines.Insert(i + 1, placeholder);
                    return string.Join('\n', lines);
                }
            }
        }

        return $"{summary}\n\n{placeholder}";
    }

    private static string? NormalizePlacement(string? placement)
    {
        if (string.IsNullOrWhiteSpace(placement))
        {
            return null;
        }

        var normalized = placement.Trim();
        var lower = normalized.ToLowerInvariant();

        if (lower.Contains("adverse") || lower.Contains("safety") || lower.Contains("side effect"))
        {
            return "What side effects or safety findings were reported?";
        }

        if (lower.Contains("result") || lower.Contains("outcome"))
        {
            return "What were the main results?";
        }

        if (lower.Contains("method") || lower.Contains("design") || lower.Contains("how was"))
        {
            return "How was the study done?";
        }

        if (lower.Contains("summary") || lower.Contains("overview") || lower.Contains("about"))
        {
            return "What is this summary about?";
        }

        return normalized;
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

    public static string LinkCitations(string summary)
    {
        if (string.IsNullOrWhiteSpace(summary))
        {
            return summary;
        }

        var normalized = System.Text.RegularExpressions.Regex.Replace(
            summary,
            @"\[\s*chunk\s*(?<id>[0-9a-f]{64})\s*\]",
            match => $"[{match.Groups["id"].Value}]",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);

        var orderedIds = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(
                     normalized,
                     @"\[(?<id>[0-9a-f]{64})\]",
                     System.Text.RegularExpressions.RegexOptions.IgnoreCase))
        {
            var id = match.Groups["id"].Value;
            if (seen.Add(id))
            {
                orderedIds.Add(id);
            }
        }

        if (orderedIds.Count == 0)
        {
            return normalized;
        }

        var citationIndex = orderedIds
            .Select((id, index) => new { id, index })
            .ToDictionary(item => item.id, item => item.index + 1, StringComparer.OrdinalIgnoreCase);

        var linked = System.Text.RegularExpressions.Regex.Replace(
            normalized,
            @"\[(?<id>[0-9a-f]{64})\]",
            match =>
            {
                var id = match.Groups["id"].Value;
                if (!citationIndex.TryGetValue(id, out var index))
                {
                    return match.Value;
                }
                return $"[\\[{index}\\]](#evidence-{index})";
            },
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (linked.Contains("### Citations", StringComparison.OrdinalIgnoreCase))
        {
            return linked;
        }

        var builder = new System.Text.StringBuilder(linked);
        builder.AppendLine();
        builder.AppendLine();
        builder.AppendLine("### Citations");
        var citationLine = string.Join(string.Empty, citationIndex.Values.Select(index => $"[\\[{index}\\]](#evidence-{index})"));
        builder.AppendLine(citationLine);

        return builder.ToString();
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
