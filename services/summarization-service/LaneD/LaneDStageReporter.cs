using System.Text.Json;
using Microsoft.Extensions.Logging;
using SummarizationService.Storage;

namespace SummarizationService.LaneD;

/// <summary>
/// Writes stage.json progress files so the frontend can display live Lane D stage status.
/// Path convention: {storageDir}/stage.json — overwritten on every stage transition.
/// </summary>
public sealed class LaneDStageReporter
{
    private readonly string _storageDir;
    private readonly ILogger _logger;
    private readonly bool _useBlob;
    private readonly string? _blobConnectionString;
    private readonly string? _blobContainer;

    public LaneDStageReporter(
        Guid documentId,
        string storageUri,
        IConfiguration configuration,
        ILogger logger)
    {
        _logger = logger;
        var storageProvider = configuration["Storage:Provider"] ?? "Local";
        _useBlob = storageProvider.Equals("Blob", StringComparison.OrdinalIgnoreCase);
        _blobConnectionString = configuration["Storage:Blob:ConnectionString"];
        _blobContainer = configuration["Storage:Blob:Container"];

        if (_useBlob)
        {
            _storageDir = StoragePathHelper.GetBlobDirectory(
                StoragePathHelper.ResolveBlobName(storageUri, _blobContainer ?? string.Empty));
        }
        else
        {
            _storageDir = Path.GetDirectoryName(storageUri) ?? Path.GetTempPath();
        }
    }

    public async Task ReportAsync(string stageId, string message, CancellationToken cancellationToken)
    {
        var payload = new
        {
            stageId,
            message,
            timestamp = DateTimeOffset.UtcNow
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = false });

        try
        {
            if (_useBlob && !string.IsNullOrWhiteSpace(_blobConnectionString) && !string.IsNullOrWhiteSpace(_blobContainer))
            {
                var container = new Azure.Storage.Blobs.BlobContainerClient(_blobConnectionString, _blobContainer);
                var blobName = $"{_storageDir}/stage.json";
                var blob = container.GetBlobClient(blobName);
                await blob.UploadAsync(new BinaryData(System.Text.Encoding.UTF8.GetBytes(json)), overwrite: true, cancellationToken);
            }
            else
            {
                var filePath = Path.Combine(_storageDir, "stage.json");
                await File.WriteAllTextAsync(filePath, json, cancellationToken);
            }

            _logger.LogInformation("[LaneD] Stage {StageId}: {Message}", stageId, message);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[LaneD] Failed to write stage report for {StageId}.", stageId);
        }
    }
}
