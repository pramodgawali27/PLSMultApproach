using System.Security.Cryptography;
using System.Text;

namespace SummarizationService.Rag;

public sealed class FakeEmbeddingClient : IEmbeddingClient
{
    private const int Dimension = 8;

    public Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken)
    {
        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(text ?? string.Empty);
        var hash = sha.ComputeHash(bytes);
        var vector = new float[Dimension];
        for (var i = 0; i < Dimension; i++)
        {
            vector[i] = hash[i] / 255f;
        }

        return Task.FromResult(vector);
    }
}
