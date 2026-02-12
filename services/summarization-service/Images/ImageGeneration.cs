using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Azure.Core;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using SummarizationService.Storage;

namespace SummarizationService.Images;

public interface IImageGenerator
{
    Task<GeneratedImage?> GenerateAsync(ImagePrompt prompt, CancellationToken cancellationToken);
}

public sealed class AzureOpenAiImageGenerator : IImageGenerator
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AzureOpenAiImageGenerator> _logger;

    public AzureOpenAiImageGenerator(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<AzureOpenAiImageGenerator> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<GeneratedImage?> GenerateAsync(ImagePrompt prompt, CancellationToken cancellationToken)
    {
        var endpoint = _configuration["AZURE_OPENAI_ENDPOINT"];
        var deployment = _configuration["AZURE_OPENAI_DEPLOYMENT_IMAGE"];
        //Image generation
        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(deployment))
        {
            _logger.LogWarning("Image generation skipped: AZURE_OPENAI_ENDPOINT or AZURE_OPENAI_DEPLOYMENT_IMAGE missing.");
            return null;
        }

        var baseEndpoint = endpoint.TrimEnd('/');
        if (!baseEndpoint.EndsWith("/openai/v1", StringComparison.OrdinalIgnoreCase))
        {
            baseEndpoint = $"{baseEndpoint}/openai/v1";
        }
        var requestUri = $"{baseEndpoint}/images/generations";

        var body = new
        {
            prompt = prompt.Prompt,
            model = deployment,
            n = 1,
            size = "1024x1024",
            response_format = "b64_json"
        };

        var client = _httpClientFactory.CreateClient("azureopenai");
        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = JsonContent.Create(body)
        };

        var apiKey = _configuration["AZURE_OPENAI_KEY"];
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            request.Headers.Add("api-key", apiKey);
        }
        else
        {
            var credential = new DefaultAzureCredential();
            var token = await credential.GetTokenAsync(
                new TokenRequestContext(new[] { "https://cognitiveservices.azure.com/.default" }),
                cancellationToken);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        }

        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Image generation failed with status {StatusCode}", response.StatusCode);
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var payload = await JsonSerializer.DeserializeAsync<ImageResponse>(stream, cancellationToken: cancellationToken);
        var data = payload?.Data?.FirstOrDefault()?.Base64Json;
        if (string.IsNullOrWhiteSpace(data))
        {
            return null;
        }

        var content = Convert.FromBase64String(data);
        return new GeneratedImage(content, "image/png", prompt.Title, prompt.FigureId);
    }

    private sealed record ImageResponse([property: JsonPropertyName("data")] List<ImageItem>? Data);

    private sealed record ImageItem([property: JsonPropertyName("b64_json")] string? Base64Json);
}

public sealed record GeneratedImage(byte[] Content, string ContentType, string? Caption, string? FigureId);

public sealed class ImagePromptParser
{
    public static ParsedOutput Parse(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return new ParsedOutput(string.Empty, Array.Empty<ImagePrompt>());
        }

        var summary = output;
        var jsonBlock = string.Empty;

        var blockBIndex = output.IndexOf("BLOCK B", StringComparison.OrdinalIgnoreCase);
        if (blockBIndex >= 0)
        {
            jsonBlock = output.Substring(blockBIndex);
            summary = output.Substring(0, blockBIndex);
        }

        summary = Regex.Replace(summary, @"^.*BLOCK A.*$", string.Empty, RegexOptions.Multiline);
        summary = Regex.Replace(summary, @"^\s*\[\[FIGURE:.*?\]\]\s*$", string.Empty, RegexOptions.Multiline);
        summary = summary.Trim();

        var jsonMatch = Regex.Match(jsonBlock, @"\[[\s\S]*\]");
        if (!jsonMatch.Success)
        {
            return new ParsedOutput(summary, Array.Empty<ImagePrompt>());
        }

        try
        {
            var prompts = JsonSerializer.Deserialize<List<ImagePrompt>>(jsonMatch.Value) ?? new List<ImagePrompt>();
            return new ParsedOutput(summary, prompts.Where(p => !string.IsNullOrWhiteSpace(p.Prompt)).ToList());
        }
        catch (JsonException)
        {
            return new ParsedOutput(summary, Array.Empty<ImagePrompt>());
        }
    }
}

public sealed record ParsedOutput(string SummaryText, IReadOnlyList<ImagePrompt> ImagePrompts);

public sealed record ImagePrompt(
    [property: JsonPropertyName("figure_id")] string FigureId,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("placement")] string Placement,
    [property: JsonPropertyName("rationale")] string Rationale,
    [property: JsonPropertyName("prompt")] string Prompt,
    [property: JsonPropertyName("style")] string Style,
    [property: JsonPropertyName("aspect_ratio")] string AspectRatio,
    [property: JsonPropertyName("must_include")] List<string>? MustInclude,
    [property: JsonPropertyName("must_avoid")] List<string>? MustAvoid);

public static class ImageGenerationPipeline
{
    private const int MaxImages = 3;
    private const string MinimalPngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR4nGNgYAAAAAMAASsJTYQAAAAASUVORK5CYII=";
    private static readonly string[] PlaceholderImages =
    {
        "iVBORw0KGgoAAAANSUhEUgAAAeAAAAEsCAIAAACUnPcNAAACSklEQVR4nO3DMQ0AAAgDsElDF+aHDY42aWYbVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVXVRw9EAjdIg3h+GQAAAABJRU5ErkJggg==",
        "iVBORw0KGgoAAAANSUhEUgAAAeAAAAEsCAIAAACUnPcNAAACS0lEQVR4nO3DMQ0AAAgDsMlBInImFRscbdJMN6qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqo8eJvBJMuQNT6AAAAAASUVORK5CYII=",
        "iVBORw0KGgoAAAANSUhEUgAAAeAAAAEsCAIAAACUnPcNAAACS0lEQVR4nO3DoQ0AAAgDsHn+t3w5zxuINmm6E1VVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV1UcPQuXgRFg/BUsAAAAASUVORK5CYII="
    };

    public static async Task<IReadOnlyList<GeneratedImageFile>> GenerateAsync(
        IReadOnlyList<ImagePrompt> prompts,
        Guid documentId,
        string storageUri,
        IImageGenerator generator,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        if (prompts.Count == 0)
        {
            return Array.Empty<GeneratedImageFile>();
        }

        var storageProvider = configuration["Storage:Provider"] ?? "Local";
        var containerName = configuration["Storage:Blob:Container"];
        var connectionString = configuration["Storage:Blob:ConnectionString"];
        var useBlob = storageProvider.Equals("Blob", StringComparison.OrdinalIgnoreCase);
        if (!useBlob && LooksLikeBlobUrl(storageUri) && !string.IsNullOrWhiteSpace(connectionString) && !string.IsNullOrWhiteSpace(containerName))
        {
            useBlob = true;
        }
        var directory = useBlob
            ? StoragePathHelper.GetBlobDirectory(StoragePathHelper.ResolveBlobName(storageUri, containerName ?? string.Empty))
            : Path.GetDirectoryName(storageUri);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return Array.Empty<GeneratedImageFile>();
        }

        var results = new List<GeneratedImageFile>();
        var container = useBlob && !string.IsNullOrWhiteSpace(connectionString) && !string.IsNullOrWhiteSpace(containerName)
            ? new BlobContainerClient(connectionString, containerName)
            : null;
        foreach (var prompt in prompts.Take(MaxImages))
        {
            var generated = await generator.GenerateAsync(prompt, cancellationToken);
            if (generated is null || generated.Content.Length == 0)
            {
                var allowPlaceholders = configuration.GetValue("ENABLE_IMAGE_PLACEHOLDERS", true);
                if (!allowPlaceholders)
                {
                    continue;
                }

                generated = CreatePlaceholder(prompt);
            }

            var safeId = string.IsNullOrWhiteSpace(generated.FigureId)
                ? Guid.NewGuid().ToString("n")
                : SanitizeId(generated.FigureId);
            string filePath;
            if (useBlob && container is not null)
            {
                var blobName = $"{directory}/{documentId}-ai-{safeId}.png";
                var blob = container.GetBlobClient(blobName);
                await blob.UploadAsync(new BinaryData(generated.Content), overwrite: true, cancellationToken);
                filePath = BuildBlobUrl(blob, configuration);
            }
            else
            {
                filePath = Path.Combine(directory, $"{documentId}-ai-{safeId}.png");
                await File.WriteAllBytesAsync(filePath, generated.Content, cancellationToken);
            }

            results.Add(new GeneratedImageFile(filePath, generated.ContentType, generated.Caption, generated.FigureId));
        }

        return results;
    }

    private static string SanitizeId(string value)
    {
        var cleaned = Regex.Replace(value, @"[^a-zA-Z0-9_-]", string.Empty);
        return string.IsNullOrWhiteSpace(cleaned) ? Guid.NewGuid().ToString("n") : cleaned;
    }

    private static GeneratedImage CreatePlaceholder(ImagePrompt prompt)
    {
        var base64 = PlaceholderImages[Random.Shared.Next(PlaceholderImages.Length)];
        var sanitized = Regex.Replace(base64, @"\s+", string.Empty);
        byte[] content;
        try
        {
            content = Convert.FromBase64String(sanitized);
        }
        catch (FormatException)
        {
            content = Convert.FromBase64String(MinimalPngBase64);
        }
        var caption = string.IsNullOrWhiteSpace(prompt.Title) ? "Placeholder illustration" : prompt.Title;
        return new GeneratedImage(content, "image/png", caption, prompt.FigureId);
    }

    private static bool LooksLikeBlobUrl(string storageUri)
    {
        if (!Uri.TryCreate(storageUri, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return uri.Host.Contains("blob.core.windows.net", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildBlobUrl(BlobClient blob, IConfiguration configuration)
    {
        var sasToken = configuration["Storage:Blob:SasToken"];
        if (string.IsNullOrWhiteSpace(sasToken))
        {
            var generateSas = configuration.GetValue("Storage:Blob:GenerateSas", true);
            if (!generateSas || !blob.CanGenerateSasUri)
            {
                return blob.Uri.ToString();
            }

            var expiresInMinutes = configuration.GetValue("Storage:Blob:SasExpiryMinutes", 120);
            var sasBuilder = new BlobSasBuilder
            {
                BlobContainerName = blob.BlobContainerName,
                BlobName = blob.Name,
                Resource = "b",
                ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(expiresInMinutes)
            };
            sasBuilder.SetPermissions(BlobSasPermissions.Read);
            return blob.GenerateSasUri(sasBuilder).ToString();
        }

        return sasToken.StartsWith("?", StringComparison.Ordinal)
            ? $"{blob.Uri}{sasToken}"
            : $"{blob.Uri}?{sasToken}";
    }
}

public sealed record GeneratedImageFile(string FilePath, string ContentType, string? Caption, string? FigureId);
