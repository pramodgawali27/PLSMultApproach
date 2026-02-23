using SummarizationService.Images;

namespace SummarizationService.LaneD.Skills;

/// <summary>
/// Wraps the DALL·E image generator for Lane D concept illustrations.
/// Hard rule: never used for charts or study design diagrams (use SvgRenderer / ChartRenderer instead).
/// </summary>
public sealed class ImageGenerationSkill : IAgentSkill
{
    public string Name => "ImageGenerationSkill";

    private readonly IImageGenerator _generator;
    private readonly IConfiguration _configuration;

    public ImageGenerationSkill(IImageGenerator generator, IConfiguration configuration)
    {
        _generator = generator;
        _configuration = configuration;
    }

    /// <summary>
    /// Generate a concept illustration using DALL·E 3.
    /// Returns null if generation is disabled or fails.
    /// </summary>
    public async Task<GeneratedImage?> GenerateConceptImageAsync(
        string imagePrompt,
        string figureId,
        string title,
        CancellationToken cancellationToken)
    {
        var enabled = _configuration.GetValue("IMAGE_GENERATION_ENABLED", true);
        if (!enabled)
        {
            return null;
        }

        var prompt = new ImagePrompt(
            FigureId: figureId,
            Title: title,
            Placement: string.Empty,
            Rationale: "Concept illustration",
            Prompt: imagePrompt,
            Style: "diagram",
            AspectRatio: "wide",
            MustInclude: null,
            MustAvoid: new List<string> { "charts", "graphs", "text paragraphs", "logos" });

        return await _generator.GenerateAsync(prompt, cancellationToken);
    }
}
