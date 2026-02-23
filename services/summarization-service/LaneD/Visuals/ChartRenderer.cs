using System.Text;
using System.Text.Json;

namespace SummarizationService.LaneD.Visuals;

/// <summary>
/// Deterministic chart renderer using inline SVG bar/line charts.
/// Renders a Chart.js-spec-compatible bar or line chart as SVG without external dependencies.
///
/// Design choices:
///   - Pure SVG output (no Playwright / headless browser required)
///   - Deterministic layout: fixed canvas 600×300, axis margin 60px
///   - Teal (#0ea5e9) for primary dataset, purple (#7c3aed) for secondary
///   - Max 8 bars or data points to avoid label overflow
/// </summary>
public static class ChartRenderer
{
    private const int ChartWidth = 600;
    private const int ChartHeight = 300;
    private const int MarginLeft = 65;
    private const int MarginBottom = 55;
    private const int MarginTop = 40;
    private const int MarginRight = 20;
    private const string Background = "#f8fafc";
    private const string AxisColor = "#94a3b8";
    private const string TextColor = "#0f172a";
    private const string GridColor = "#e2e8f0";

    private static readonly string[] DatasetColors =
        { "#0ea5e9", "#7c3aed", "#10b981", "#f59e0b", "#ef4444" };

    public static string Render(VisualItem visual)
    {
        var labels = visual.Labels ?? new List<string>();
        var datasets = visual.Datasets ?? new List<ChartDataset>();

        if (labels.Count == 0 || datasets.Count == 0)
        {
            return RenderEmpty(visual.Title);
        }

        // Cap data to avoid overflow
        labels = labels.Take(8).ToList();

        var allValues = datasets.SelectMany(d => d.Data ?? new List<double>()).ToList();
        if (allValues.Count == 0) return RenderEmpty(visual.Title);

        var maxVal = allValues.Max();
        var yMax = Math.Ceiling(maxVal * 1.15 / 10) * 10; // round up to next 10
        if (yMax <= 0) yMax = 100;

        var plotWidth = ChartWidth - MarginLeft - MarginRight;
        var plotHeight = ChartHeight - MarginTop - MarginBottom;

        var sb = new StringBuilder();
        sb.AppendLine($"""<svg xmlns="http://www.w3.org/2000/svg" width="{ChartWidth}" height="{ChartHeight}" viewBox="0 0 {ChartWidth} {ChartHeight}" style="font-family:system-ui,sans-serif;background:{Background}">""");

        // Title
        if (!string.IsNullOrWhiteSpace(visual.Title))
        {
            sb.AppendLine($"""  <text x="{ChartWidth / 2}" y="22" text-anchor="middle" font-size="13" font-weight="600" fill="{TextColor}">{EscapeXml(visual.Title)}</text>""");
        }

        // Grid lines (5 horizontal)
        for (var i = 0; i <= 5; i++)
        {
            var gridY = MarginTop + plotHeight - (i * plotHeight / 5);
            var gridVal = (yMax * i / 5).ToString("G4");
            sb.AppendLine($"""  <line x1="{MarginLeft}" y1="{gridY}" x2="{ChartWidth - MarginRight}" y2="{gridY}" stroke="{GridColor}" stroke-width="1"/>""");
            sb.AppendLine($"""  <text x="{MarginLeft - 5}" y="{gridY + 4}" text-anchor="end" font-size="9" fill="{AxisColor}">{gridVal}</text>""");
        }

        // Axes
        sb.AppendLine($"""  <line x1="{MarginLeft}" y1="{MarginTop}" x2="{MarginLeft}" y2="{MarginTop + plotHeight}" stroke="{AxisColor}" stroke-width="1.5"/>""");
        sb.AppendLine($"""  <line x1="{MarginLeft}" y1="{MarginTop + plotHeight}" x2="{ChartWidth - MarginRight}" y2="{MarginTop + plotHeight}" stroke="{AxisColor}" stroke-width="1.5"/>""");

        // Y-axis label
        if (!string.IsNullOrWhiteSpace(visual.YLabel))
        {
            var yLabelText = visual.Units != null ? $"{visual.YLabel} ({visual.Units})" : visual.YLabel;
            sb.AppendLine($"""  <text transform="rotate(-90,14,{MarginTop + plotHeight / 2})" x="14" y="{MarginTop + plotHeight / 2}" text-anchor="middle" font-size="9" fill="{AxisColor}">{EscapeXml(yLabelText)}</text>""");
        }

        // X-axis label
        if (!string.IsNullOrWhiteSpace(visual.XLabel))
        {
            sb.AppendLine($"""  <text x="{MarginLeft + plotWidth / 2}" y="{ChartHeight - 4}" text-anchor="middle" font-size="9" fill="{AxisColor}">{EscapeXml(visual.XLabel)}</text>""");
        }

        // Bars
        var groupCount = labels.Count;
        var dsCount = datasets.Count;
        var groupWidth = plotWidth / Math.Max(1, groupCount);
        var barPadding = groupWidth * 0.1;
        var totalBarWidth = groupWidth - 2 * barPadding;
        var barWidth = totalBarWidth / Math.Max(1, dsCount);

        for (var dsIdx = 0; dsIdx < datasets.Count; dsIdx++)
        {
            var ds = datasets[dsIdx];
            var color = DatasetColors[dsIdx % DatasetColors.Length];
            var data = ds.Data ?? new List<double>();

            for (var lblIdx = 0; lblIdx < labels.Count; lblIdx++)
            {
                var val = lblIdx < data.Count ? data[lblIdx] : 0;
                var barX = MarginLeft + lblIdx * groupWidth + barPadding + dsIdx * barWidth;
                var barH = Math.Max(0, (val / yMax) * plotHeight);
                var barY = MarginTop + plotHeight - barH;

                sb.AppendLine($"""  <rect x="{barX:F0}" y="{barY:F0}" width="{barWidth:F0}" height="{barH:F0}" fill="{color}" rx="2"/>""");

                // Value label on top of bar
                if (val > 0)
                {
                    var valLabel = val % 1 == 0 ? ((int)val).ToString() : val.ToString("G3");
                    if (!string.IsNullOrWhiteSpace(visual.Units)) valLabel += visual.Units;
                    sb.AppendLine($"""  <text x="{barX + barWidth / 2:F0}" y="{barY - 3:F0}" text-anchor="middle" font-size="9" fill="{TextColor}">{EscapeXml(valLabel)}</text>""");
                }
            }
        }

        // X-axis labels
        for (var lblIdx = 0; lblIdx < labels.Count; lblIdx++)
        {
            var lx = MarginLeft + lblIdx * groupWidth + groupWidth / 2;
            var label = Truncate(labels[lblIdx], 14);
            sb.AppendLine($"""  <text x="{lx:F0}" y="{MarginTop + plotHeight + 14}" text-anchor="middle" font-size="9" fill="{TextColor}">{EscapeXml(label)}</text>""");
        }

        // Legend
        if (datasets.Count > 1)
        {
            var legendX = MarginLeft;
            var legendY = ChartHeight - 14;
            for (var dsIdx = 0; dsIdx < datasets.Count; dsIdx++)
            {
                var color = DatasetColors[dsIdx % DatasetColors.Length];
                var lx = legendX + dsIdx * 140;
                sb.AppendLine($"""  <rect x="{lx}" y="{legendY - 8}" width="10" height="10" fill="{color}" rx="2"/>""");
                sb.AppendLine($"""  <text x="{lx + 14}" y="{legendY}" font-size="9" fill="{TextColor}">{EscapeXml(Truncate(datasets[dsIdx].Label, 15))}</text>""");
            }
        }

        sb.AppendLine("</svg>");
        return sb.ToString();
    }

    private static string RenderEmpty(string title)
    {
        return $"""<svg xmlns="http://www.w3.org/2000/svg" width="400" height="80" style="font-family:system-ui,sans-serif;background:#f8fafc"><text x="200" y="44" text-anchor="middle" font-size="13" fill="#64748b">{EscapeXml(title)}: no chart data available</text></svg>""";
    }

    private static string EscapeXml(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        return text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
    }

    private static string Truncate(string text, int max)
        => text.Length <= max ? text : text[..max] + "…";
}
