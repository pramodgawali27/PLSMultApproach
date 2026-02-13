using System.Security.Cryptography;
using System.Text;

namespace SummarizationService.Rag;

public sealed class TextChunker
{
    private readonly int _chunkSize;
    private readonly int _overlap;

    public TextChunker(IConfiguration configuration)
    {
        _chunkSize = configuration.GetValue("RAG_CHUNK_SIZE", 1500);
        _overlap = configuration.GetValue("RAG_CHUNK_OVERLAP", 200);
    }

    public IReadOnlyList<TextChunk> Chunk(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<TextChunk>();
        }

        var normalized = text.Replace("\r", string.Empty);
        var sections = SplitByHeadings(normalized);
        var chunks = new List<TextChunk>();
        var index = 0;

        foreach (var section in sections)
        {
            var content = section.Content;
            var start = 0;
            while (start < content.Length)
            {
                var length = Math.Min(_chunkSize, content.Length - start);
                var slice = content.Substring(start, length).Trim();
                if (!string.IsNullOrWhiteSpace(slice))
                {
                    chunks.Add(new TextChunk(index++, slice, section.Heading));
                }

                if (start + length >= content.Length)
                {
                    break;
                }

                start = Math.Max(0, start + length - _overlap);
            }
        }

        return chunks;
    }

    public static string BuildChunkId(Guid documentId, int index, string content)
    {
        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes($"{documentId}:{index}:{content}");
        var hash = sha.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static string HashContent(string content)
    {
        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(content);
        var hash = sha.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static IReadOnlyList<SectionBlock> SplitByHeadings(string text)
    {
        var lines = text.Split('\n');
        var blocks = new List<SectionBlock>();
        var currentHeading = "Context";
        var currentContent = new StringBuilder();

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("#"))
            {
                if (currentContent.Length > 0)
                {
                    blocks.Add(new SectionBlock(currentHeading, currentContent.ToString()));
                    currentContent.Clear();
                }

                currentHeading = trimmed.TrimStart('#', ' ').Trim();
                continue;
            }

            currentContent.AppendLine(line);
        }

        if (currentContent.Length > 0)
        {
            blocks.Add(new SectionBlock(currentHeading, currentContent.ToString()));
        }

        return blocks.Count > 0 ? blocks : new[] { new SectionBlock("Context", text) };
    }

    private sealed record SectionBlock(string Heading, string Content);
}

public sealed record TextChunk(int Index, string Content, string Section);
