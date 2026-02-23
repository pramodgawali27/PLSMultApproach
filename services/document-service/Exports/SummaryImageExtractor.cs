using DocumentFormat.OpenXml.Packaging;

namespace DocumentService.Exports;

public sealed class SummaryImageExtractor
{
    private const int MaxImages = 3;

    public IReadOnlyList<SummaryImage> ExtractImages(string storageUri, string contentType, string? modelInfoJson)
    {
        if (!File.Exists(storageUri))
        {
            return Array.Empty<SummaryImage>();
        }

        var fromModel = ExtractFromModelInfo(modelInfoJson);
        if (fromModel.Count > 0)
        {
            return fromModel;
        }

        if (!contentType.Contains("word", StringComparison.OrdinalIgnoreCase)
            && !storageUri.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
        {
            return Array.Empty<SummaryImage>();
        }

        var images = new List<SummaryImage>();
        using var document = WordprocessingDocument.Open(storageUri, false);
        var parts = document.MainDocumentPart?.ImageParts ?? Enumerable.Empty<ImagePart>();
        foreach (var imagePart in parts)
        {
            using var stream = imagePart.GetStream();
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            var data = memory.ToArray();
            if (data.Length == 0)
            {
                continue;
            }

            images.Add(new SummaryImage(data, imagePart.ContentType, null, null));
            if (images.Count >= MaxImages)
            {
                break;
            }
        }

        return images;
    }

    private static IReadOnlyList<SummaryImage> ExtractFromModelInfo(string? modelInfoJson)
    {
        if (string.IsNullOrWhiteSpace(modelInfoJson))
        {
            return Array.Empty<SummaryImage>();
        }

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(modelInfoJson);
            var images = new List<SummaryImage>();

            // Lane D: rendered SVG/chart visuals with placement info
            if (doc.RootElement.TryGetProperty("renderedVisuals", out var renderedVisuals))
            {
                images.AddRange(ReadRenderedVisualsFromElement(renderedVisuals));
            }

            // Lane A/B/C: extracted images from docx
            if (doc.RootElement.TryGetProperty("extraction", out var extraction))
            {
                images.AddRange(ReadImagesFromElement(extraction, "images"));
            }

            // Generated DALL·E images
            if (doc.RootElement.TryGetProperty("generatedImages", out var generatedImages))
            {
                images.AddRange(ReadImagesFromElement(generatedImages, null));
            }

            return images;
        }
        catch (System.Text.Json.JsonException)
        {
            return Array.Empty<SummaryImage>();
        }
    }

    private static IEnumerable<SummaryImage> ReadRenderedVisualsFromElement(System.Text.Json.JsonElement element)
    {
        if (element.ValueKind != System.Text.Json.JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var item in element.EnumerateArray())
        {
            if (!item.TryGetProperty("filePath", out var filePathEl))
            {
                continue;
            }

            var filePath = filePathEl.GetString();
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                continue;
            }

            var contentType = item.TryGetProperty("contentType", out var ctEl)
                ? ctEl.GetString() ?? "image/svg+xml"
                : "image/svg+xml";
            var caption = item.TryGetProperty("caption", out var captEl)
                ? captEl.GetString()
                : null;
            var placement = item.TryGetProperty("placement", out var plEl)
                ? plEl.GetString()
                : null;

            yield return new SummaryImage(File.ReadAllBytes(filePath), contentType, caption, placement);
        }
    }

    private static IEnumerable<SummaryImage> ReadImagesFromElement(System.Text.Json.JsonElement element, string? propertyName)
    {
        System.Text.Json.JsonElement imagesElement;
        if (propertyName is null)
        {
            imagesElement = element;
        }
        else if (!element.TryGetProperty(propertyName, out imagesElement))
        {
            yield break;
        }
        if (imagesElement.ValueKind != System.Text.Json.JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var imageElement in imagesElement.EnumerateArray())
        {
            if (!imageElement.TryGetProperty("filePath", out var filePathElement))
            {
                continue;
            }

            var filePath = filePathElement.GetString();
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                continue;
            }

            var contentType = imageElement.TryGetProperty("contentType", out var ctElement)
                ? ctElement.GetString() ?? "application/octet-stream"
                : "application/octet-stream";
            var caption = imageElement.TryGetProperty("caption", out var captionElement)
                ? captionElement.GetString()
                : null;
            var placement = imageElement.TryGetProperty("placement", out var placementElement)
                ? placementElement.GetString()
                : null;

            yield return new SummaryImage(File.ReadAllBytes(filePath), contentType, caption, placement);
        }
    }
}

public sealed record SummaryImage(byte[] Content, string ContentType, string? Caption, string? Placement);
