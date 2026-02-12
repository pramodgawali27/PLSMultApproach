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
    private readonly string _containerName;

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
        _containerName = containerName;
    }

    public async Task<string> GetLocalPathAsync(string storageUri, Guid documentId, CancellationToken cancellationToken)
    {
        var blobName = StoragePathHelper.ResolveBlobName(storageUri, _containerName);
        var blob = _container.GetBlobClient(blobName);
        var filePath = Path.Combine(_basePath, documentId.ToString());
        Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? _basePath);

        await using var stream = File.Open(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        await blob.DownloadToAsync(stream, cancellationToken);
        return filePath;
    }

}

public static class StoragePathHelper
{
    public static string ResolveBlobName(string storageUri, string containerName)
    {
        if (Uri.TryCreate(storageUri, UriKind.Absolute, out var uri))
        {
            var path = uri.AbsolutePath.TrimStart('/');
            var parts = path.Split('/', 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return string.Empty;
            }

            if (parts[0].Equals(containerName, StringComparison.OrdinalIgnoreCase))
            {
                return parts.Length > 1 ? parts[1] : string.Empty;
            }

            return path;
        }

        return storageUri.TrimStart('/');
    }

    public static string GetBlobDirectory(string blobName)
    {
        if (string.IsNullOrWhiteSpace(blobName))
        {
            return string.Empty;
        }

        var idx = blobName.LastIndexOf('/');
        return idx > 0 ? blobName.Substring(0, idx) : blobName;
    }
}
