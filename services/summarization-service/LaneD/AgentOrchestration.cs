using System.Text.Json;
using Microsoft.Extensions.Logging;
using SummarizationService.Diagnostics;
using SummarizationService.Images;
using SummarizationService.LaneD.Skills;
using SummarizationService.LaneD.Visuals;
using SummarizationService.Prompting;
using SummarizationService.Rag;

namespace SummarizationService.LaneD;

/// <summary>
/// Orchestrates the full Lane D agentic pipeline (D0–D9).
///
/// Stages:
///   D0  — Canonicalize + RAG index
///   D1  — PlannerAgent   → outline.json + questions
///   D2  — FactsAgent     → facts.json (strict schema + citations)
///   D3  — EvidenceAgent  → evidence.json (claim→citation map)
///   D4  — WriterAgent    → pls.md draft
///   D5  — ReviewAgent    → review.json (numeric claim verification)
///   D6  — FinalizeAgent  → final.md
///   D7  — VisualPlanner  → visualspec.json
///   D8  — Visual render  → SVG + Chart.js PNG (deterministic)
///   D9  — Image gen      → DALL·E concept images (optional)
/// </summary>
public sealed class LocalAgentOrchestrator
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    private readonly ILaneDAgentRuntime _runtime;
    private readonly SkillRegistry _skills;
    private readonly PromptTemplateStore _templates;
    private readonly RagIndexer _ragIndexer;
    private readonly IConfiguration _configuration;
    private readonly ILogger<LocalAgentOrchestrator> _logger;

    public LocalAgentOrchestrator(
        ILaneDAgentRuntime runtime,
        SkillRegistry skills,
        PromptTemplateStore templates,
        RagIndexer ragIndexer,
        IConfiguration configuration,
        ILogger<LocalAgentOrchestrator> logger)
    {
        _runtime = runtime;
        _skills = skills;
        _templates = templates;
        _ragIndexer = ragIndexer;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<LaneDResult> RunAsync(
        LaneDContext ctx,
        LaneDStageReporter stageReporter,
        CancellationToken cancellationToken)
    {
        var trace = RunTrace.Create(_configuration, ctx.DocumentId, "lane-d");
        var stages = new List<LaneDStageArtifact>();
        var metadata = new { title = ctx.Title, therapeutic_area = ctx.TherapeuticArea, audience = ctx.Audience };

        // ─── D0: Canonicalize + RAG index ────────────────────────────────────────
        await stageReporter.ReportAsync("D0_Canonicalize", "Extracting and indexing document.", cancellationToken);
        var d0Start = DateTimeOffset.UtcNow;

        var docklingSkill = _skills.Get<DocklingSkill>("DocklingSkill");
        var extraction = await docklingSkill.ExtractAsync(ctx.DocumentId, ctx.StorageUri, ctx.ContentType, cancellationToken);
        var canonicalText = extraction?.Text ?? string.Empty;
        trace?.WriteText("D0_canonical.txt", canonicalText);

        await _ragIndexer.IndexAsync(ctx.DocumentId, ctx.TenantId, ctx.StorageUri, ctx.ContentType, cancellationToken);

        stages.Add(new LaneDStageArtifact("D0", "Canonicalize", d0Start, DateTimeOffset.UtcNow, null));

        // ─── D1: Planner ─────────────────────────────────────────────────────────
        await stageReporter.ReportAsync("D1_Planner", "Building PLS outline and fact questions.", cancellationToken);
        var d1Start = DateTimeOffset.UtcNow;

        var retrieval = _skills.Get<RetrievalSkill>("RetrievalSkill");
        var planChunks = await retrieval.RetrieveMultiAsync(
            new[] { "study design overview", "primary endpoint", "patient population" },
            ctx.DocumentId, ctx.TenantId, topKPerQuery: 4, maxTotal: 8, cancellationToken);

        var plannerPrompt = _templates.Render(
            Path.Combine("lane-d", "planner.md.sbn"),
            new
            {
                metadata,
                context = BuildChunksText(planChunks)
            });

        var plannerResp = await _runtime.RunAsync(
            new AgentRequest("PlannerAgent", MedicalSummarizationSystemPrompt(), plannerPrompt, 0.2, 1200, null),
            cancellationToken);
        trace?.WriteText("D1_planner_response.txt", plannerResp.Text);

        var outlineJson = ExtractJson(plannerResp.Text);
        var plan = ParseJson<PlannerOutput>(outlineJson);
        trace?.WriteJson("D1_outline.json", plan);
        stages.Add(new LaneDStageArtifact("D1", "Planner", d1Start, DateTimeOffset.UtcNow, outlineJson));

        // ─── D2: Facts Extraction ─────────────────────────────────────────────────
        await stageReporter.ReportAsync("D2_Facts", "Extracting structured facts with citations.", cancellationToken);
        var d2Start = DateTimeOffset.UtcNow;

        var factQuestions = plan?.Questions?.Select(q => q.Question).ToList()
            ?? DefaultFactQuestions();

        var factChunks = await retrieval.RetrieveMultiAsync(
            factQuestions,
            ctx.DocumentId, ctx.TenantId, topKPerQuery: 5, maxTotal: 16, cancellationToken);

        var factsPrompt = _templates.Render(
            Path.Combine("lane-d", "facts.md.sbn"),
            new
            {
                document_id = ctx.DocumentId,
                metadata,
                questions_json = JsonSerializer.Serialize(factQuestions, JsonOpts),
                chunks_json = JsonSerializer.Serialize(ChunksToObject(factChunks), JsonOpts)
            });

        var factsResp = await _runtime.RunAsync(
            new AgentRequest("FactsAgent", FactsSystemPrompt(), factsPrompt, 0.1, 2000, null),
            cancellationToken);
        trace?.WriteText("D2_facts_response.txt", factsResp.Text);

        var factsJson = ExtractJson(factsResp.Text);
        var facts = ParseJson<object>(factsJson);
        trace?.WriteJson("D2_facts.json", facts);
        stages.Add(new LaneDStageArtifact("D2", "Facts", d2Start, DateTimeOffset.UtcNow, factsJson));

        // ─── D3: Evidence Map ─────────────────────────────────────────────────────
        await stageReporter.ReportAsync("D3_Evidence", "Building claim-to-citation evidence map.", cancellationToken);
        var d3Start = DateTimeOffset.UtcNow;

        var evidencePrompt = _templates.Render(
            Path.Combine("lane-d", "evidence.md.sbn"),
            new
            {
                facts_json = factsJson,
                chunks_json = JsonSerializer.Serialize(ChunksToObject(factChunks), JsonOpts)
            });

        var evidenceResp = await _runtime.RunAsync(
            new AgentRequest("EvidenceAgent", EvidenceSystemPrompt(), evidencePrompt, 0.1, 3000, null),
            cancellationToken);
        trace?.WriteText("D3_evidence_response.txt", evidenceResp.Text);

        var evidenceJson = ExtractJson(evidenceResp.Text);
        trace?.WriteJson("D3_evidence.json", ParseJson<object>(evidenceJson));
        stages.Add(new LaneDStageArtifact("D3", "Evidence", d3Start, DateTimeOffset.UtcNow, evidenceJson));

        // ─── D4: Writer ───────────────────────────────────────────────────────────
        await stageReporter.ReportAsync("D4_Writer", "Drafting plain-language summary.", cancellationToken);
        var d4Start = DateTimeOffset.UtcNow;

        var writerPrompt = _templates.Render(
            Path.Combine("lane-d", "writer.md.sbn"),
            new
            {
                metadata,
                facts_json = factsJson,
                outline_json = JsonSerializer.Serialize(plan?.Outline, JsonOpts)
            });

        var writerResp = await _runtime.RunAsync(
            new AgentRequest("WriterAgent", WriterSystemPrompt(), writerPrompt, 0.3, 3000, null),
            cancellationToken);
        trace?.WriteText("D4_draft.md", writerResp.Text);

        var draftParsed = ImagePromptParser.Parse(writerResp.Text);
        var draftMarkdown = draftParsed.SummaryText;
        stages.Add(new LaneDStageArtifact("D4", "Writer", d4Start, DateTimeOffset.UtcNow, null));

        // ─── D5: Review ───────────────────────────────────────────────────────────
        await stageReporter.ReportAsync("D5_Review", "Verifying numeric claims against facts.", cancellationToken);
        var d5Start = DateTimeOffset.UtcNow;

        var reviewPrompt = _templates.Render(
            Path.Combine("lane-d", "review.md.sbn"),
            new
            {
                draft_markdown = draftMarkdown,
                facts_json = factsJson,
                evidence_json = evidenceJson
            });

        var reviewResp = await _runtime.RunAsync(
            new AgentRequest("ReviewAgent", ReviewSystemPrompt(), reviewPrompt, 0.1, 1500, null),
            cancellationToken);
        trace?.WriteText("D5_review_response.txt", reviewResp.Text);

        var reviewJson = ExtractJson(reviewResp.Text);
        var review = ParseJson<ReviewOutput>(reviewJson);
        trace?.WriteJson("D5_review.json", review);
        stages.Add(new LaneDStageArtifact("D5", "Review", d5Start, DateTimeOffset.UtcNow, reviewJson));

        // ─── D6: Finalize ─────────────────────────────────────────────────────────
        await stageReporter.ReportAsync("D6_Finalize", "Applying review corrections and finalizing.", cancellationToken);
        var d6Start = DateTimeOffset.UtcNow;

        string finalMarkdown;
        if (review?.Approved == true && (review.Issues?.Count ?? 0) == 0)
        {
            finalMarkdown = draftMarkdown;
            _logger.LogInformation("[LaneD] Review approved with no issues — skipping finalize LLM call.");
        }
        else
        {
            var finalizePrompt = _templates.Render(
                Path.Combine("lane-d", "finalize.md.sbn"),
                new
                {
                    draft_markdown = draftMarkdown,
                    review_json = reviewJson,
                    facts_json = factsJson
                });

            var finalizeResp = await _runtime.RunAsync(
                new AgentRequest("FinalizeAgent", WriterSystemPrompt(), finalizePrompt, 0.2, 3000, null),
                cancellationToken);
            trace?.WriteText("D6_final.md", finalizeResp.Text);
            finalMarkdown = finalizeResp.Text;
        }

        stages.Add(new LaneDStageArtifact("D6", "Finalize", d6Start, DateTimeOffset.UtcNow, null));

        // ─── D7: Visual Planner ───────────────────────────────────────────────────
        await stageReporter.ReportAsync("D7_VisualPlan", "Planning deterministic visuals.", cancellationToken);
        var d7Start = DateTimeOffset.UtcNow;

        var visualPlanPrompt = _templates.Render(
            Path.Combine("lane-d", "visual-plan.md.sbn"),
            new
            {
                facts_json = factsJson,
                final_markdown = finalMarkdown
            });

        var visualPlanResp = await _runtime.RunAsync(
            new AgentRequest("VisualPlannerAgent", VisualPlannerSystemPrompt(), visualPlanPrompt, 0.1, 2000, null),
            cancellationToken);
        trace?.WriteText("D7_visualspec_response.txt", visualPlanResp.Text);

        var visualSpecJson = ExtractJson(visualPlanResp.Text);
        var visualSpec = ParseJson<VisualSpec>(visualSpecJson);
        trace?.WriteJson("D7_visualspec.json", visualSpec);
        stages.Add(new LaneDStageArtifact("D7", "VisualPlan", d7Start, DateTimeOffset.UtcNow, visualSpecJson));

        // ─── D8: Deterministic Visual Rendering ──────────────────────────────────
        await stageReporter.ReportAsync("D8_Visuals", "Rendering SVG diagrams and charts.", cancellationToken);
        var d8Start = DateTimeOffset.UtcNow;

        var renderedVisuals = new List<RenderedVisual>();
        if (visualSpec?.Visuals is { Count: > 0 })
        {
            foreach (var visual in visualSpec.Visuals)
            {
                try
                {
                    var rendered = await VisualRenderer.RenderAsync(visual, ctx, _configuration, cancellationToken);
                    if (rendered is not null)
                    {
                        renderedVisuals.Add(rendered);
                        finalMarkdown = InsertVisual(finalMarkdown, visual, rendered, visualSpec);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[LaneD] Failed to render visual {VisualId}.", visual.Id);
                }
            }
        }

        trace?.WriteText("D8_final_with_visuals.md", finalMarkdown);
        stages.Add(new LaneDStageArtifact("D8", "Visuals", d8Start, DateTimeOffset.UtcNow, null));

        // ─── D9: DALL·E Concept Images (optional) ────────────────────────────────
        await stageReporter.ReportAsync("D9_Images", "Generating concept illustrations.", cancellationToken);
        var d9Start = DateTimeOffset.UtcNow;

        if (draftParsed.ImagePrompts.Count > 0 && _configuration.GetValue("IMAGE_GENERATION_ENABLED", true))
        {
            var imageSkill = _skills.Get<ImageGenerationSkill>("ImageGenerationSkill");
            foreach (var imgPrompt in draftParsed.ImagePrompts.Take(2))
            {
                try
                {
                    var generated = await imageSkill.GenerateConceptImageAsync(
                        imgPrompt.Prompt, imgPrompt.FigureId, imgPrompt.Title, cancellationToken);

                    if (generated is not null)
                    {
                        var saved = await SaveGeneratedImageAsync(generated, ctx, cancellationToken);
                        if (saved is not null)
                        {
                            var placeholder = $"[[FIGURE:{imgPrompt.FigureId}]]";
                            finalMarkdown = finalMarkdown.Replace(
                                placeholder,
                                $"![{imgPrompt.Title}]({saved})",
                                StringComparison.OrdinalIgnoreCase);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[LaneD] Failed to generate image for {FigureId}.", imgPrompt.FigureId);
                }
            }
        }

        stages.Add(new LaneDStageArtifact("D9", "Images", d9Start, DateTimeOffset.UtcNow, null));
        await stageReporter.ReportAsync("D9_Complete", "Lane D pipeline complete.", cancellationToken);

        // ─── Build model info ─────────────────────────────────────────────────────
        var modelInfo = new
        {
            lane = "D",
            stages = stages.Select(s => new
            {
                s.StageId,
                s.StageName,
                durationMs = (s.CompletedAt - s.StartedAt).TotalMilliseconds
            }),
            factsWarnings = ExtractWarnings(factsJson),
            reviewApproved = review?.Approved,
            reviewIssueCount = review?.Issues?.Count ?? 0,
            visualsRendered = renderedVisuals.Count,
            // Rendered visual file paths — used by SummaryImageExtractor for PDF export
            renderedVisuals = renderedVisuals.Select(v => new
            {
                id = v.VisualId,
                type = v.Type,
                filePath = v.FilePath,
                contentType = "image/svg+xml",
                caption = v.VisualId,
                placement = visualSpec?.Placement?
                    .FirstOrDefault(p => string.Equals(p.VisualId, v.VisualId, StringComparison.OrdinalIgnoreCase))
                    ?.InsertAfterSection
            }).ToList()
        };

        return new LaneDResult(
            finalMarkdown,
            JsonSerializer.Serialize(modelInfo, JsonOpts),
            stages);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────────

    private static string BuildChunksText(IReadOnlyList<SummarizationService.Rag.RagChunk> chunks)
    {
        return string.Join("\n\n---\n\n", chunks.Select(c =>
            $"[chunkId: {c.ChunkId}] [section: {c.Section ?? "unknown"}] [page: {c.Page?.ToString() ?? "?"}]\n{c.Content}"));
    }

    private static IReadOnlyList<object> ChunksToObject(IReadOnlyList<SummarizationService.Rag.RagChunk> chunks)
    {
        return chunks.Select(c => (object)new
        {
            chunkId = c.ChunkId,
            section = c.Section,
            page = c.Page,
            text = c.Content
        }).ToList();
    }

    private static string ExtractJson(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "{}";

        // Try code-fenced JSON block first
        var fenceStart = text.IndexOf("```json", StringComparison.OrdinalIgnoreCase);
        if (fenceStart >= 0)
        {
            var contentStart = text.IndexOf('\n', fenceStart) + 1;
            var fenceEnd = text.IndexOf("```", contentStart, StringComparison.OrdinalIgnoreCase);
            if (fenceEnd > contentStart)
            {
                return text[contentStart..fenceEnd].Trim();
            }
        }

        // Try bare code fence
        var bareFenceStart = text.IndexOf("```", StringComparison.OrdinalIgnoreCase);
        if (bareFenceStart >= 0)
        {
            var contentStart = text.IndexOf('\n', bareFenceStart) + 1;
            var fenceEnd = text.IndexOf("```", contentStart, StringComparison.OrdinalIgnoreCase);
            if (fenceEnd > contentStart)
            {
                return text[contentStart..fenceEnd].Trim();
            }
        }

        // Try to find first { or [
        var braceIdx = text.IndexOfAny(new[] { '{', '[' });
        if (braceIdx >= 0)
        {
            return text[braceIdx..].Trim();
        }

        return text.Trim();
    }

    private static T? ParseJson<T>(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return default;
        try
        {
            return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch
        {
            return default;
        }
    }

    private static List<string> DefaultFactQuestions() =>
        new()
        {
            "What is the study design and type of trial?",
            "What is the total patient population and eligibility criteria?",
            "What are the treatment arms and interventions?",
            "What is the primary endpoint and its timepoint?",
            "What are the primary outcome results by treatment arm?",
            "What is the study duration and follow-up period?",
            "What were the most common adverse events and their incidence?",
            "What are the key secondary endpoints and results?"
        };

    private static List<string> ExtractWarnings(string factsJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(factsJson);
            if (doc.RootElement.TryGetProperty("trial", out var trial)
                && trial.TryGetProperty("warnings", out var warnings))
            {
                return warnings.EnumerateArray().Select(w => w.GetString() ?? string.Empty).ToList();
            }
        }
        catch { /* ignore */ }
        return new List<string>();
    }

    private static string InsertVisual(
        string markdown,
        VisualItem visual,
        RenderedVisual rendered,
        VisualSpec spec)
    {
        var placement = spec.Placement?.FirstOrDefault(p =>
            string.Equals(p.VisualId, visual.Id, StringComparison.OrdinalIgnoreCase));

        var imgTag = $"\n\n{rendered.Markdown}\n\n";

        if (placement is not null && !string.IsNullOrWhiteSpace(placement.InsertAfterSection))
        {
            var lines = markdown.Split('\n').ToList();
            for (var i = 0; i < lines.Count; i++)
            {
                var stripped = lines[i].TrimStart('#', ' ').Trim();
                if (stripped.Contains(placement.InsertAfterSection, StringComparison.OrdinalIgnoreCase))
                {
                    lines.Insert(i + 1, imgTag);
                    return string.Join('\n', lines);
                }
            }
        }

        // Replace placeholder if present
        var placeholder = $"[[FIGURE:{visual.Id}]]";
        if (markdown.Contains(placeholder, StringComparison.OrdinalIgnoreCase))
        {
            return markdown.Replace(placeholder, rendered.Markdown, StringComparison.OrdinalIgnoreCase);
        }

        var chartPlaceholder = $"[[CHART:{visual.Id}]]";
        if (markdown.Contains(chartPlaceholder, StringComparison.OrdinalIgnoreCase))
        {
            return markdown.Replace(chartPlaceholder, rendered.Markdown, StringComparison.OrdinalIgnoreCase);
        }

        return markdown + imgTag;
    }

    private async Task<string?> SaveGeneratedImageAsync(
        GeneratedImage image,
        LaneDContext ctx,
        CancellationToken cancellationToken)
    {
        var storageProvider = _configuration["Storage:Provider"] ?? "Local";
        var useBlob = storageProvider.Equals("Blob", StringComparison.OrdinalIgnoreCase);

        var ext = image.ContentType switch
        {
            "image/png" => ".png",
            "image/jpeg" => ".jpg",
            _ => ".png"
        };

        var fileName = $"{ctx.DocumentId}-concept-{image.FigureId ?? "img"}{ext}";

        if (useBlob)
        {
            var container = _configuration["Storage:Blob:Container"] ?? string.Empty;
            var connStr = _configuration["Storage:Blob:ConnectionString"] ?? string.Empty;
            if (string.IsNullOrWhiteSpace(connStr)) return null;

            var blobName = $"tenant/{ctx.TenantId}/documents/{ctx.DocumentId}/{fileName}";
            var client = new Azure.Storage.Blobs.BlobContainerClient(connStr, container);
            var blob = client.GetBlobClient(blobName);
            await blob.UploadAsync(new BinaryData(image.Content), overwrite: true, cancellationToken);
            return blob.Uri.ToString();
        }

        var dir = Path.GetDirectoryName(ctx.StorageUri) ?? Path.GetTempPath();
        var path = Path.Combine(dir, fileName);
        await File.WriteAllBytesAsync(path, image.Content, cancellationToken);
        return path;
    }

    // ─── System prompts ───────────────────────────────────────────────────────────

    private static string MedicalSummarizationSystemPrompt() =>
        "You are a medical plain-language summary architect. Output only valid JSON. Never invent clinical data.";

    private static string FactsSystemPrompt() =>
        "You are a clinical fact extractor. Output only valid JSON. Every numeric value must have a citation. Never guess.";

    private static string EvidenceSystemPrompt() =>
        "You are a medical evidence mapper. Output only valid JSON. Quote directly from retrieved chunks.";

    private static string WriterSystemPrompt() =>
        "You are a plain-language medical writer. Write at a 6th–8th grade level. Never invent statistics.";

    private static string ReviewSystemPrompt() =>
        "You are a medical accuracy reviewer. Any number without a citation is a fail. Output only valid JSON.";

    private static string VisualPlannerSystemPrompt() =>
        "You are a clinical data visualization planner. Output only valid VisualSpec JSON. Use only values from facts.";

    // ─── Internal parse types ─────────────────────────────────────────────────────

    private sealed class PlannerOutput
    {
        [System.Text.Json.Serialization.JsonPropertyName("outline")]
        public List<OutlineSection>? Outline { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("questions")]
        public List<PlanQuestion>? Questions { get; set; }
    }

    private sealed class OutlineSection
    {
        [System.Text.Json.Serialization.JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("heading")]
        public string Heading { get; set; } = string.Empty;
    }

    private sealed class PlanQuestion
    {
        [System.Text.Json.Serialization.JsonPropertyName("question")]
        public string Question { get; set; } = string.Empty;
    }

    private sealed class ReviewOutput
    {
        [System.Text.Json.Serialization.JsonPropertyName("approved")]
        public bool Approved { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("issues")]
        public List<object>? Issues { get; set; }
    }
}
