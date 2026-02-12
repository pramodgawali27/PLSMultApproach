using Azure.Storage.Blobs;

namespace SummarizationService.Storage;

public interface IStorageReader
{
    Task<string> GetLocalPathAsync(string storageUri, Guid documentId, CancellationToken cancellationToken);
}

public sealed class LocalStorageReader : IStorageReader
{
    public Task<string> GetLocalPathAsync(string storageUri, Guid documentId, CancellationToken cancellationToken)
        => Task.FromResult(storageUri);
}

public sealed class AzureBlobStorageReader : IStorageReader
{
    private readonly BlobContainerClient _container;
    private readonly string _basePath;

    public AzureBlobStorageReader(IConfiguration configuration)
    {
        var connectionString = configuration["Storage:Blob:ConnectionString"];
        var containerName = configuration["Storage:Blob:Container"];
        if (string.IsNullOrWhiteSpace(connectionString) || string.IsNullOrWhiteSpace(containerName))
        {
            throw new InvalidOperationException("Storage:Blob:ConnectionString and Storage:Blob:Container are required.");
        }

        _container = new BlobContainerClient(connectionString, containerName);
        _basePath = Path.Combine(Path.GetTempPath(), "pls", "blob-cache");
    }

    public async Task<string> GetLocalPathAsync(string storageUri, Guid documentId, CancellationToken cancellationToken)
    {
        var blobName = ResolveBlobName(storageUri);
        var blob = _container.GetBlobClient(blobName);
        var filePath = Path.Combine(_basePath, documentId.ToString());
        Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? _basePath);

        await using var stream = File.Open(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        await blob.DownloadToAsync(stream, cancellationToken);
        return filePath;
    }

    private static string ResolveBlobName(string storageUri)
    {
        if (Uri.TryCreate(storageUri, UriKind.Absolute, out var uri))
        {
            return uri.AbsolutePath.TrimStart('/');
        }

        return storageUri.TrimStart('/');
    }
}
