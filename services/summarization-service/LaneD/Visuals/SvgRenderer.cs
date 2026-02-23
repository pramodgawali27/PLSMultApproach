using System.Text;

namespace SummarizationService.LaneD.Visuals;

/// <summary>
/// Deterministic SVG renderer for clinical study design diagrams.
///
/// Design rules (Taylor &amp; Francis PLSP reference):
///   - Fixed canvas: 900×450 for LR layout
///   - Fixed node width: 160px, height: 70px
///   - Max 12 nodes to avoid clutter
///   - Automatic text wrapping (max 2 lines per label)
///   - LR grid layout with column lanes for arms
///   - PLSP colors: Purple (#6b4fa0) for treatment arm, Teal (#47b5a0) for control arm
/// </summary>
public static class SvgRenderer
{
    private const int CanvasWidth = 900;
    private const int CanvasHeight = 450;
    private const int NodeWidth = 160;
    private const int NodeHeight = 70;
    private const int NodeRx = 10;
    private const int HorizontalGap = 40;
    private const int VerticalGap = 30;
    // PLSP reference colors: purple = treatment arm, teal = comparator/control arm
    private const string TreatmentColor = "#6b4fa0";   // PLSP purple
    private const string ControlColor = "#47b5a0";      // PLSP teal
    private const string NeutralColor = "#64748b";
    private const string BackgroundColor = "#ffffff";
    private const string NodeFill = "#ffffff";
    private const string NodeStroke = "#d1c4e9";        // light purple border
    private const string TextColor = "#1f2937";

    /// <summary>Render a study design SVG from the visual spec nodes/edges.</summary>
    public static string Render(VisualItem visual)
    {
        var nodes = visual.Nodes ?? new List<SvgNode>();
        var edges = visual.Edges ?? new List<SvgEdge>();

        if (nodes.Count == 0)
        {
            return RenderEmpty(visual.Title);
        }

        // Cap at 12 nodes
        nodes = nodes.Take(12).ToList();

        var positions = ComputePositions(nodes, edges);
        var sb = new StringBuilder();

        sb.AppendLine($"""<svg xmlns="http://www.w3.org/2000/svg" width="{CanvasWidth}" height="{CanvasHeight}" viewBox="0 0 {CanvasWidth} {CanvasHeight}" style="font-family:system-ui,sans-serif;background:{BackgroundColor}">""");

        // Title
        if (!string.IsNullOrWhiteSpace(visual.Title))
        {
            sb.AppendLine($"""  <text x="{CanvasWidth / 2}" y="24" text-anchor="middle" font-size="14" font-weight="600" fill="{TextColor}">{EscapeXml(visual.Title)}</text>""");
        }

        // Arrow marker definition
        sb.AppendLine("""
  <defs>
    <marker id="arrow" markerWidth="10" markerHeight="7" refX="9" refY="3.5" orient="auto">
      <polygon points="0 0, 10 3.5, 0 7" fill="#94a3b8"/>
    </marker>
  </defs>
  """);

        // Edges (drawn before nodes so nodes appear on top)
        foreach (var edge in edges)
        {
            if (positions.TryGetValue(edge.From, out var fromPos) && positions.TryGetValue(edge.To, out var toPos))
            {
                var x1 = fromPos.X + NodeWidth;
                var y1 = fromPos.Y + NodeHeight / 2;
                var x2 = toPos.X;
                var y2 = toPos.Y + NodeHeight / 2;

                sb.AppendLine($"""  <line x1="{x1}" y1="{y1}" x2="{x2 - 8}" y2="{y2}" stroke="#94a3b8" stroke-width="1.5" marker-end="url(#arrow)"/>""");

                if (!string.IsNullOrWhiteSpace(edge.Label))
                {
                    var mx = (x1 + x2) / 2;
                    var my = (y1 + y2) / 2 - 6;
                    sb.AppendLine($"""  <text x="{mx}" y="{my}" text-anchor="middle" font-size="10" fill="{NeutralColor}">{EscapeXml(edge.Label)}</text>""");
                }
            }
        }

        // Nodes
        for (var i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            if (!positions.TryGetValue(node.Id, out var pos)) continue;

            // PLSP color assignment:
            // i=0 → root/enrollment node: neutral gray
            // i=1 → first child (treatment arm): purple
            // i=2 → second child (control arm): teal
            // i≥3 → alternating purple/teal
            var accent = i == 0 ? NeutralColor : (i % 2 == 1 ? TreatmentColor : ControlColor);
            var headerHeight = 8;

            // Node rectangle
            sb.AppendLine($"""  <rect x="{pos.X}" y="{pos.Y}" width="{NodeWidth}" height="{NodeHeight}" rx="{NodeRx}" fill="{NodeFill}" stroke="{NodeStroke}" stroke-width="1"/>""");
            // Accent top bar
            sb.AppendLine($"""  <rect x="{pos.X}" y="{pos.Y}" width="{NodeWidth}" height="{headerHeight}" rx="{NodeRx}" fill="{accent}"/>""");
            sb.AppendLine($"""  <rect x="{pos.X}" y="{pos.Y + 4}" width="{NodeWidth}" height="{headerHeight - 4}" fill="{accent}"/>""");

            // Label (truncate to 20 chars)
            var label = Truncate(node.Label, 20);
            sb.AppendLine($"""  <text x="{pos.X + NodeWidth / 2}" y="{pos.Y + NodeHeight / 2 - 4}" text-anchor="middle" font-size="12" font-weight="600" fill="{TextColor}">{EscapeXml(label)}</text>""");

            // Sublabel
            if (!string.IsNullOrWhiteSpace(node.Sublabel))
            {
                var sub = Truncate(node.Sublabel, 22);
                sb.AppendLine($"""  <text x="{pos.X + NodeWidth / 2}" y="{pos.Y + NodeHeight / 2 + 12}" text-anchor="middle" font-size="10" fill="{NeutralColor}">{EscapeXml(sub)}</text>""");
            }

            // n= count
            if (node.N.HasValue)
            {
                sb.AppendLine($"""  <text x="{pos.X + NodeWidth / 2}" y="{pos.Y + NodeHeight - 6}" text-anchor="middle" font-size="9" fill="{NeutralColor}">n={node.N}</text>""");
            }
        }

        sb.AppendLine("</svg>");
        return sb.ToString();
    }

    private static Dictionary<string, (int X, int Y)> ComputePositions(
        List<SvgNode> nodes,
        List<SvgEdge> edges)
    {
        // Build a depth map: BFS from roots
        var childrenOf = new Dictionary<string, List<string>>();
        var parentCount = new Dictionary<string, int>();

        foreach (var node in nodes)
        {
            childrenOf[node.Id] = new List<string>();
            parentCount[node.Id] = 0;
        }

        foreach (var edge in edges)
        {
            if (childrenOf.ContainsKey(edge.From))
                childrenOf[edge.From].Add(edge.To);
            if (parentCount.ContainsKey(edge.To))
                parentCount[edge.To]++;
        }

        var roots = nodes.Where(n => parentCount[n.Id] == 0).Select(n => n.Id).ToList();
        if (roots.Count == 0)
            roots = new List<string> { nodes[0].Id };

        // BFS to assign column (depth) and row (sibling index)
        var col = new Dictionary<string, int>();
        var siblingIndex = new Dictionary<string, int>();
        var colSiblingCount = new Dictionary<int, int>();
        var queue = new Queue<string>(roots);
        var visited = new HashSet<string>();

        foreach (var r in roots) { col[r] = 0; visited.Add(r); }

        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            var curCol = col[cur];

            if (!colSiblingCount.ContainsKey(curCol)) colSiblingCount[curCol] = 0;
            siblingIndex[cur] = colSiblingCount[curCol]++;

            foreach (var child in childrenOf[cur])
            {
                if (!visited.Contains(child))
                {
                    visited.Add(child);
                    col[child] = curCol + 1;
                    queue.Enqueue(child);
                }
            }
        }

        // Unvisited nodes go to last column
        var maxCol = col.Values.DefaultIfEmpty(0).Max() + 1;
        foreach (var node in nodes)
        {
            if (!col.ContainsKey(node.Id))
            {
                col[node.Id] = maxCol;
                if (!colSiblingCount.ContainsKey(maxCol)) colSiblingCount[maxCol] = 0;
                siblingIndex[node.Id] = colSiblingCount[maxCol]++;
            }
        }

        // Compute actual pixel positions
        var totalCols = (colSiblingCount.Keys.DefaultIfEmpty(0).Max()) + 1;
        var colXStep = Math.Max(NodeWidth + HorizontalGap,
            (CanvasWidth - NodeWidth) / Math.Max(1, totalCols));

        var positions = new Dictionary<string, (int X, int Y)>();
        foreach (var node in nodes)
        {
            var c = col.GetValueOrDefault(node.Id, 0);
            var s = siblingIndex.GetValueOrDefault(node.Id, 0);
            var maxSiblings = colSiblingCount.GetValueOrDefault(c, 1);

            var x = 20 + c * colXStep;
            var totalHeight = maxSiblings * (NodeHeight + VerticalGap) - VerticalGap;
            var startY = (CanvasHeight - totalHeight) / 2;
            var y = startY + s * (NodeHeight + VerticalGap);

            positions[node.Id] = (x, Math.Max(40, y));
        }

        return positions;
    }

    private static string RenderEmpty(string title)
    {
        return $"""<svg xmlns="http://www.w3.org/2000/svg" width="400" height="80" style="font-family:system-ui,sans-serif;background:#f8fafc"><text x="200" y="44" text-anchor="middle" font-size="13" fill="#64748b">{EscapeXml(title)}: no diagram data available</text></svg>""";
    }

    private static string EscapeXml(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;");
    }

    private static string Truncate(string text, int max)
        => text.Length <= max ? text : text[..max] + "…";
}
