using System.Net.Http.Json;
using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using PLS.BuildingBlocks.Results;
using UglyToad.PdfPig;

namespace SummarizationService.Extraction;

public interface ITextExtractor
{
    Task<Result<ExtractionResult>> ExtractAsync(Guid documentId, string storageUri, string contentType);
}

public sealed class ExtractionResult
{
    public ExtractionResult(string text, IReadOnlyList<ExtractedImage> images, string source)
    {
        Text = text;
        Images = images;
        Source = source;
    }

    public string Text { get; }
    public IReadOnlyList<ExtractedImage> Images { get; }
    public string Source { get; }
}

public sealed record ExtractedImage(byte[] Content, string ContentType, string? Caption);

public sealed class HybridTextExtractor : ITextExtractor
{
    private readonly DoclingTextExtractor _docling;
    private readonly CompositeTextExtractor _fallback;
    private readonly IConfiguration _configuration;
    private readonly ILogger<HybridTextExtractor> _logger;

    public HybridTextExtractor(
        DoclingTextExtractor docling,
        CompositeTextExtractor fallback,
        IConfiguration configuration,
        ILogger<HybridTextExtractor> logger)
    {
        _docling = docling;
        _fallback = fallback;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<Result<ExtractionResult>> ExtractAsync(Guid documentId, string storageUri, string contentType)
    {
        var useDocling = _configuration.GetValue("USE_DOCLING", true);
        if (!useDocling)
        {
            return await _fallback.ExtractAsync(documentId, storageUri, contentType);
        }

        var doclingResult = await _docling.ExtractAsync(documentId, storageUri, contentType);
        if (doclingResult.IsSuccess && doclingResult.Value is not null && !string.IsNullOrWhiteSpace(doclingResult.Value.Text))
        {
            return doclingResult;
        }

        _logger.LogWarning("Docling extraction failed; falling back to built-in extractor. Error: {Error}", doclingResult.Error);
        return await _fallback.ExtractAsync(documentId, storageUri, contentType);
    }
}

public sealed class DoclingTextExtractor : ITextExtractor
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public DoclingTextExtractor(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    public async Task<Result<ExtractionResult>> ExtractAsync(Guid documentId, string storageUri, string contentType)
    {
        if (!File.Exists(storageUri))
        {
            return Result<ExtractionResult>.Fail("File not found in storage.");
        }

        var baseUrl = _configuration["DOCLING_URL"];
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return Result<ExtractionResult>.Fail("DOCLING_URL is not configured.");
        }

        using var content = new MultipartFormDataContent();
        await using var stream = File.OpenRead(storageUri);
        var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        content.Add(fileContent, "file", Path.GetFileName(storageUri));

        var client = _httpClientFactory.CreateClient("docling");
        client.BaseAddress = new Uri(baseUrl);
        var response = await client.PostAsync("/extract", content);
        if (!response.IsSuccessStatusCode)
        {
            return Result<ExtractionResult>.Fail($"Docling returned {response.StatusCode}");
        }

        var result = await response.Content.ReadFromJsonAsync<DoclingResponse>();
        if (result is null || string.IsNullOrWhiteSpace(result.Markdown))
        {
            return Result<ExtractionResult>.Fail("Docling response was empty.");
        }

        var images = result.Images?
            .Select(image => new ExtractedImage(
                Convert.FromBase64String(image.DataBase64 ?? string.Empty),
                image.ContentType ?? "application/octet-stream",
                string.IsNullOrWhiteSpace(image.Caption) ? null : image.Caption))
            .Where(image => image.Content.Length > 0)
            .ToList()
            ?? new List<ExtractedImage>();

        return Result<ExtractionResult>.Ok(new ExtractionResult(result.Markdown, images, "docling"));
    }

    private sealed record DoclingResponse(string Markdown, IReadOnlyList<DoclingImage>? Images);

    private sealed record DoclingImage(string? FileName, string? ContentType, string? Caption, string? DataBase64);
}

public sealed class CompositeTextExtractor : ITextExtractor
{
    public async Task<Result<ExtractionResult>> ExtractAsync(Guid documentId, string storageUri, string contentType)
    {
        if (!File.Exists(storageUri))
        {
            return Result<ExtractionResult>.Fail("File not found in storage.");
        }

        if (contentType.Contains("text", StringComparison.OrdinalIgnoreCase))
        {
            var text = await File.ReadAllTextAsync(storageUri);
            return Result<ExtractionResult>.Ok(new ExtractionResult(text, Array.Empty<ExtractedImage>(), "local"));
        }

        if (contentType.Contains("pdf", StringComparison.OrdinalIgnoreCase))
        {
            using var document = PdfDocument.Open(storageUri);
            var text = new StringBuilder();
            foreach (var page in document.GetPages())
            {
                text.AppendLine(page.Text);
            }
            return Result<ExtractionResult>.Ok(new ExtractionResult(text.ToString(), Array.Empty<ExtractedImage>(), "local"));
        }

        if (contentType.Contains("word", StringComparison.OrdinalIgnoreCase) || storageUri.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
        {
            using var doc = WordprocessingDocument.Open(storageUri, false);
            var body = doc.MainDocumentPart?.Document?.Body;
            var text = body?.Descendants<Text>().Select(t => t.Text) ?? Enumerable.Empty<string>();
            return Result<ExtractionResult>.Ok(new ExtractionResult(string.Join(" ", text), Array.Empty<ExtractedImage>(), "local"));
        }

        return Result<ExtractionResult>.Fail("File type not supported for extraction.");
    }
}
