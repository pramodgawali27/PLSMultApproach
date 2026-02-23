using SummarizationService.Extraction;

namespace SummarizationService.LaneD.Skills;

/// <summary>
/// Wraps the Docling/Hybrid text extractor for Lane D's canonicalization stage (D0).
/// Produces canonical markdown text from a document storage URI.
/// </summary>
public sealed class DocklingSkill : IAgentSkill
{
    public string Name => "DocklingSkill";

    private readonly ITextExtractor _extractor;

    public DocklingSkill(ITextExtractor extractor)
    {
        _extractor = extractor;
    }

    /// <summary>Extract canonical markdown text from the document at the given storage URI.</summary>
    public async Task<ExtractionResult?> ExtractAsync(
        Guid documentId,
        string storageUri,
        string contentType,
        CancellationToken cancellationToken)
    {
        var result = await _extractor.ExtractAsync(documentId, storageUri, contentType);
        return result.IsSuccess ? result.Value : null;
    }
}
