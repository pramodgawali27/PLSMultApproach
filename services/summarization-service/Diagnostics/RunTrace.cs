using System.Text.Json;

namespace SummarizationService.Diagnostics;

public sealed class RunTrace
{
    private readonly string _directory;

    private RunTrace(string directory)
    {
        _directory = directory;
        Directory.CreateDirectory(_directory);
    }

    public static RunTrace? Create(IConfiguration configuration, Guid documentId, string stage)
    {
        var enabled = configuration.GetValue("TRACE_ENABLED", false);
        var root = configuration["TRACE_OUTPUT_DIR"];
        if (!enabled && string.IsNullOrWhiteSpace(root))
        {
            return null;
        }

        var baseDir = string.IsNullOrWhiteSpace(root)
            ? Path.Combine(AppContext.BaseDirectory, "trace")
            : root;
        var safeStage = string.IsNullOrWhiteSpace(stage) ? "run" : stage.Trim();
        var stamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmssfff");
        var directory = Path.Combine(baseDir, documentId.ToString(), $"{stamp}-{safeStage}");
        return new RunTrace(directory);
    }

    public void WriteText(string fileName, string content)
    {
        var path = Path.Combine(_directory, fileName);
        File.WriteAllText(path, content ?? string.Empty);
    }

    public void WriteJson(string fileName, object payload)
    {
        var path = Path.Combine(_directory, fileName);
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        File.WriteAllText(path, json);
    }
}
