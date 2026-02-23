using System.Text;

namespace SummarizationService.LaneD.Visuals;

/// <summary>
/// Dispatches VisualItem to the appropriate renderer (SVG or Chart) and persists the output.
/// Returns a RenderedVisual containing the file path and inline markdown.
/// </summary>
public static class VisualRenderer
{
    public static async Task<RenderedVisual?> RenderAsync(
        VisualItem visual,
        LaneDContext ctx,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        string svgContent;
        string mimeType = "image/svg+xml";

        switch (visual.Type?.ToLowerInvariant())
        {
            case "study_design_svg":
                svgContent = SvgRenderer.Render(visual);
                break;

            case "chartjs":
                svgContent = ChartRenderer.Render(visual);
                break;

            case "image":
                // DALL·E concept images are handled separately by ImageGenerationSkill in D9
                return null;

            default:
                return null;
        }

        var bytes = Encoding.UTF8.GetBytes(svgContent);
        var fileName = $"{ctx.DocumentId}-{visual.Id}.svg";
        var filePath = await PersistAsync(bytes, fileName, mimeType, ctx, configuration, cancellationToken);

        // Inline SVG in markdown (data URI for local; URL for blob)
        var markdown = BuildMarkdown(visual, filePath, svgContent, configuration);

        return new RenderedVisual(visual.Id, visual.Type, filePath, markdown);
    }

    private static async Task<string> PersistAsync(
        byte[] content,
        string fileName,
        string mimeType,
        LaneDContext ctx,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var storageProvider = configuration["Storage:Provider"] ?? "Local";
        var useBlob = storageProvider.Equals("Blob", StringComparison.OrdinalIgnoreCase);

        if (useBlob)
        {
            var connStr = configuration["Storage:Blob:ConnectionString"] ?? string.Empty;
            var container = configuration["Storage:Blob:Container"] ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(connStr) && !string.IsNullOrWhiteSpace(container))
            {
                var blobName = $"tenant/{ctx.TenantId}/documents/{ctx.DocumentId}/{fileName}";
                var client = new Azure.Storage.Blobs.BlobContainerClient(connStr, container);
                var blob = client.GetBlobClient(blobName);
                await blob.UploadAsync(new BinaryData(content), overwrite: true, cancellationToken);
                return blob.Uri.ToString();
            }
        }

        // Local storage: save next to the document
        var dir = Path.GetDirectoryName(ctx.StorageUri) ?? Path.GetTempPath();
        var localPath = Path.Combine(dir, fileName);
        await File.WriteAllBytesAsync(localPath, content, cancellationToken);
        return localPath;
    }

    private static string BuildMarkdown(
        VisualItem visual,
        string filePath,
        string svgContent,
        IConfiguration configuration)
    {
        var title = visual.Title ?? visual.Id;
        var storageProvider = configuration["Storage:Provider"] ?? "Local";
        var useBlob = storageProvider.Equals("Blob", StringComparison.OrdinalIgnoreCase);

        if (useBlob && (filePath.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                        || filePath.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
        {
            return $"![{title}]({filePath})";
        }

        // For local dev: embed as data URI so the frontend can render without a file server
        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(svgContent));
        return $"![{title}](data:image/svg+xml;base64,{base64})";
    }
}
