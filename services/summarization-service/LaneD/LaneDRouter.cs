using Microsoft.Extensions.Logging;
using PLS.BuildingBlocks.Data;
using SummarizationService.Extraction;
using SummarizationService.Images;
using SummarizationService.LaneD.Skills;
using SummarizationService.Prompting;
using SummarizationService.Rag;

namespace SummarizationService.LaneD;

/// <summary>
/// Entry point for Lane D requests from the /internal/summarize endpoint.
/// Constructs a per-request SkillRegistry + LaneDStageReporter,
/// then delegates to LocalAgentOrchestrator.
/// </summary>
public sealed class LaneDRouter
{
    private readonly ILaneDAgentRuntime _runtime;
    private readonly ITextExtractor _textExtractor;
    private readonly IEmbeddingClient _embeddingClient;
    private readonly IVectorStore _vectorStore;
    private readonly IImageGenerator _imageGenerator;
    private readonly PromptTemplateStore _templates;
    private readonly RagIndexer _ragIndexer;
    private readonly IConfiguration _configuration;
    private readonly ILogger<LaneDRouter> _logger;

    public LaneDRouter(
        ILaneDAgentRuntime runtime,
        ITextExtractor textExtractor,
        IEmbeddingClient embeddingClient,
        IVectorStore vectorStore,
        IImageGenerator imageGenerator,
        PromptTemplateStore templates,
        RagIndexer ragIndexer,
        IConfiguration configuration,
        ILogger<LaneDRouter> logger)
    {
        _runtime = runtime;
        _textExtractor = textExtractor;
        _embeddingClient = embeddingClient;
        _vectorStore = vectorStore;
        _imageGenerator = imageGenerator;
        _templates = templates;
        _ragIndexer = ragIndexer;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<LaneDResult> HandleAsync(
        LaneDContext ctx,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("[LaneD] Starting agentic pipeline for document {DocumentId}.", ctx.DocumentId);

        // Build per-request stage reporter
        var stageReporter = new LaneDStageReporter(
            ctx.DocumentId,
            ctx.StorageUri,
            _configuration,
            _logger);

        // Build per-request skill registry
        var skills = new SkillRegistry();
        skills.RegisterSkill(new DocklingSkill(_textExtractor));
        skills.RegisterSkill(new RetrievalSkill(_embeddingClient, _vectorStore));
        skills.RegisterSkill(new ImageGenerationSkill(_imageGenerator, _configuration));
        skills.RegisterSkill(new StageReportSkill(stageReporter));

        var orchestrator = new LocalAgentOrchestrator(
            _runtime,
            skills,
            _templates,
            _ragIndexer,
            _configuration,
            _logger as ILogger<LocalAgentOrchestrator>
                ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<LocalAgentOrchestrator>.Instance);

        return await orchestrator.RunAsync(ctx, stageReporter, cancellationToken);
    }
}
