using System.Text.Json.Serialization;

namespace SummarizationService.LaneD.Visuals;

/// <summary>Root VisualSpec contract produced by VisualPlannerAgent (D7).</summary>
public sealed class VisualSpec
{
    [JsonPropertyName("visuals")]
    public List<VisualItem>? Visuals { get; set; }

    [JsonPropertyName("placement")]
    public List<VisualPlacement>? Placement { get; set; }
}

public sealed class VisualItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>study_design_svg | chartjs | image</summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    // SVG-only fields
    [JsonPropertyName("layout")]
    public string? Layout { get; set; }   // "LR" | "TB"

    [JsonPropertyName("nodes")]
    public List<SvgNode>? Nodes { get; set; }

    [JsonPropertyName("edges")]
    public List<SvgEdge>? Edges { get; set; }

    // Chart.js-only fields
    [JsonPropertyName("chartType")]
    public string? ChartType { get; set; }  // "bar" | "line" | "stackedBar"

    [JsonPropertyName("labels")]
    public List<string>? Labels { get; set; }

    [JsonPropertyName("datasets")]
    public List<ChartDataset>? Datasets { get; set; }

    [JsonPropertyName("xLabel")]
    public string? XLabel { get; set; }

    [JsonPropertyName("yLabel")]
    public string? YLabel { get; set; }

    [JsonPropertyName("units")]
    public string? Units { get; set; }

    // DALL·E image-only
    [JsonPropertyName("imagePrompt")]
    public string? ImagePrompt { get; set; }
}

public sealed class SvgNode
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("sublabel")]
    public string? Sublabel { get; set; }

    [JsonPropertyName("n")]
    public int? N { get; set; }
}

public sealed class SvgEdge
{
    [JsonPropertyName("from")]
    public string From { get; set; } = string.Empty;

    [JsonPropertyName("to")]
    public string To { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string? Label { get; set; }
}

public sealed class ChartDataset
{
    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public List<double>? Data { get; set; }
}

public sealed class VisualPlacement
{
    [JsonPropertyName("visualId")]
    public string VisualId { get; set; } = string.Empty;

    [JsonPropertyName("insertAfterSection")]
    public string InsertAfterSection { get; set; } = string.Empty;
}

/// <summary>Output from the visual renderer — contains both file path and inline Markdown.</summary>
public sealed record RenderedVisual(
    string VisualId,
    string Type,
    string FilePath,
    string Markdown);
