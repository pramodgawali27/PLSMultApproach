using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using PLS.Contracts;
using QDocument = QuestPDF.Fluent.Document;
using OpenXmlDocument = DocumentFormat.OpenXml.Wordprocessing.Document;
using System.Text.RegularExpressions;

namespace DocumentService.Exports;

public sealed class SummaryExportService
{
    public byte[] CreatePdf(SummaryExportData data, IReadOnlyList<SummaryImage> images)
    {
        var sections = ParseSections(data.SummaryText);
        // PLSP Taylor & Francis color scheme: purple (#6b4fa0) headers, white/light backgrounds
        var palette = new[]
        {
            new SectionStyle("#FFFFFF", "#6b4fa0", "#6b4fa0"),
            new SectionStyle("#F9F7FC", "#6b4fa0", "#6b4fa0"),
            new SectionStyle("#FFFFFF", "#6b4fa0", "#6b4fa0"),
            new SectionStyle("#F9F7FC", "#6b4fa0", "#6b4fa0")
        };

        var document = QDocument.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(32);
                page.DefaultTextStyle(style => style.FontFamily("DejaVu Sans").FontSize(13).FontColor("#1F2937"));
                page.Header().Element(header => BuildPdfHeader(header, data));
                page.Content().Column(column =>
                {
                    column.Item().Element(meta => BuildPdfMetadata(meta, data));
                    var remainingImages = images.ToList();
                    foreach (var (section, index) in sections.Select((section, index) => (section, index)))
                    {
                        var style = palette[index % palette.Length];
                        var matched = MatchImagesForSection(section.Title, remainingImages);
                        column.Item().Element(content => BuildSectionCard(content, section, style, matched));
                    }

                    if (remainingImages.Count > 0)
                    {
                        var style = palette[0];
                        var figureSection = new SummarySection("Figures", string.Empty);
                        column.Item().Element(content => BuildSectionCard(content, figureSection, style, remainingImages));
                        remainingImages.Clear();
                    }
                });
                page.Footer().Element(footer => BuildPdfFooter(footer, data));
            });
        });

        return document.GeneratePdf();
    }

    public byte[] CreateDocx(SummaryExportData data)
    {
        using var stream = new MemoryStream();
        using var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true);

        var mainPart = document.AddMainDocumentPart();
        mainPart.Document = new OpenXmlDocument(new Body());

        var headerPart = mainPart.AddNewPart<HeaderPart>();
        headerPart.Header = new Header(BuildDocxHeader(data));

        var footerPart = mainPart.AddNewPart<FooterPart>();
        footerPart.Footer = new Footer(BuildDocxFooter(data));

        var sectionProperties = new SectionProperties(
            new HeaderReference { Type = HeaderFooterValues.Default, Id = mainPart.GetIdOfPart(headerPart) },
            new FooterReference { Type = HeaderFooterValues.Default, Id = mainPart.GetIdOfPart(footerPart) },
            new PageMargin { Top = 720, Bottom = 720, Left = 720, Right = 720 });

        var body = mainPart.Document.Body ?? new Body();
        body.Append(BuildDocxSummaryHeader());
        foreach (var line in BuildDocxMetadata(data))
        {
            body.Append(line);
        }
        body.Append(BuildDocxSummary(data));
        body.Append(sectionProperties);
        mainPart.Document.Body = body;
        mainPart.Document.Save();

        return stream.ToArray();
    }

    private static void BuildPdfHeader(IContainer container, SummaryExportData data)
    {
        // PLSP Taylor & Francis header: purple banner with teal PLS badge
        container.Column(col =>
        {
            col.Item().PaddingBottom(6).Text("Plain Language Summary of Publication")
                .FontSize(9)
                .FontColor("#6b4fa0");
            col.Item().Background("#6b4fa0").PaddingVertical(14).PaddingHorizontal(16).Row(row =>
            {
                row.RelativeItem().Column(text =>
                {
                    text.Item().Text(data.DocumentName).FontSize(15).FontColor("#FFFFFF").SemiBold();
                    text.Item().PaddingTop(2).Text($"Plain Language Summary · {data.LaneLabel}")
                        .FontSize(9)
                        .FontColor("#E8DEFF");
                });
                row.ConstantItem(64).AlignRight().AlignMiddle().Element(card =>
                {
                    card.Width(52).Height(52).Background("#47b5a0").Border(2).BorderColor("#FFFFFF")
                        .AlignCenter().AlignMiddle()
                        .Text("PLS").FontColor("#FFFFFF").FontSize(12).SemiBold();
                });
            });
        });
    }

    private static void BuildPdfFooter(IContainer container, SummaryExportData data)
    {
        container.Row(row =>
        {
            row.RelativeItem()
                .Text($"Generated {data.GeneratedAt:yyyy-MM-dd HH:mm} UTC")
                .FontSize(9)
                .FontColor("#666666");
            row.ConstantItem(60).AlignRight().Text(text =>
            {
                text.DefaultTextStyle(style => style.FontSize(9).FontColor("#666666"));
                text.CurrentPageNumber();
                text.Span(" / ");
                text.TotalPages();
            });
        });
    }

    private static Paragraph BuildDocxHeader(SummaryExportData data)
    {
        var run = new Run(
            new RunProperties(new Bold()),
            new Text($"Plain Language Summary - {data.DocumentName} ({data.LaneLabel})"));
        return new Paragraph(run);
    }

    private static Paragraph BuildDocxFooter(SummaryExportData data)
    {
        return new Paragraph(
            new Run(new Text($"Generated {data.GeneratedAt:yyyy-MM-dd HH:mm} UTC")));
    }

    private static Paragraph BuildDocxSummary(SummaryExportData data)
    {
        var paragraph = new Paragraph();
        var run = new Run();
        var lines = StripBoldMarkers(NormalizeSummaryText(data.SummaryText))
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        for (var i = 0; i < lines.Length; i++)
        {
            run.AppendChild(new Text(lines[i]));
            if (i < lines.Length - 1)
            {
                run.AppendChild(new Break());
            }
        }
        paragraph.Append(run);
        return paragraph;
    }

    private static Paragraph BuildDocxSummaryHeader()
    {
        var run = new Run(
            new RunProperties(new Bold()),
            new Text("Summary in Plain Language"));
        return new Paragraph(run);
    }

    private static IEnumerable<Paragraph> BuildDocxMetadata(SummaryExportData data)
    {
        var lines = new List<string>
        {
            $"Title: {data.DisplayTitle}",
            $"Therapeutic area: {data.TherapeuticArea ?? "-"}",
            $"Audience: {data.Audience ?? "-"}",
            $"Generated: {data.GeneratedAt:yyyy-MM-dd HH:mm} UTC",
            $"Lane: {data.LaneLabel}"
        };

        foreach (var line in lines)
        {
            yield return new Paragraph(new Run(new Text(line)));
        }
    }

    private static void BuildPdfMetadata(IContainer container, SummaryExportData data)
    {
        container.PaddingVertical(10).PaddingHorizontal(4).Column(col =>
        {
            col.Item().Text("Document details").FontSize(10).FontColor("#6b4fa0").SemiBold();
            col.Item().PaddingTop(6).Background("#FFFFFF").Border(1).BorderColor("#d1c4e9").Padding(10).Column(meta =>
            {
                meta.Spacing(3);
                meta.Item().Text(text =>
                {
                    text.Span("Title: ").SemiBold();
                    text.Span(data.DisplayTitle);
                });
                meta.Item().Text(text =>
                {
                    text.Span("Therapeutic area: ").SemiBold();
                    text.Span(data.TherapeuticArea ?? "-");
                });
                meta.Item().Text(text =>
                {
                    text.Span("Audience: ").SemiBold();
                    text.Span(data.Audience ?? "-");
                });
                meta.Item().Text(text =>
                {
                    text.Span("Generated: ").SemiBold();
                    text.Span($"{data.GeneratedAt:yyyy-MM-dd HH:mm} UTC");
                });
                meta.Item().Text(text =>
                {
                    text.Span("Lane: ").SemiBold();
                    text.Span(data.LaneLabel);
                });
            });
        });
    }

    private static string NormalizeSummaryText(string text)
        => Regex.Replace(
            text.Replace("\r", string.Empty, StringComparison.Ordinal),
            @"\[(?<label>[^\]]+)\]\([^)]+\)",
            "[$1]")
            .Replace(@"\[", "[", StringComparison.Ordinal)
            .Replace(@"\]", "]", StringComparison.Ordinal)
            .Replace("[[", "[", StringComparison.Ordinal)
            .Replace("]]", "]", StringComparison.Ordinal);

    private static string StripBoldMarkers(string text)
        => text.Replace("**", string.Empty, StringComparison.Ordinal);

    private static IReadOnlyList<SummarySection> ParseSections(string text)
    {
        var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        var sections = new List<SummarySection>();
        SummarySection? current = null;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (IsHeading(line))
            {
                var title = NormalizeHeading(line);
                current = new SummarySection(title, string.Empty);
                sections.Add(current);
                continue;
            }

            current ??= new SummarySection("Summary in Plain Language", string.Empty);
            current.Body = string.IsNullOrEmpty(current.Body)
                ? NormalizeSummaryText(line)
                : $"{current.Body}\n{NormalizeSummaryText(line)}";
        }

        return sections.Count > 0 ? sections : new[] { new SummarySection("Summary in Plain Language", NormalizeSummaryText(text)) };
    }

    private static bool IsHeading(string line)
        => line.StartsWith("#", StringComparison.Ordinal)
           || Regex.IsMatch(line, @"^\d+[\).]");

    private static string NormalizeHeading(string line)
    {
        var trimmed = line.TrimStart('#', ' ').Trim();
        trimmed = Regex.Replace(trimmed, @"^\d+[\).]\s*", string.Empty);
        return string.IsNullOrWhiteSpace(trimmed) ? "Section" : trimmed;
    }

    private static void BuildSectionCard(
        IContainer container,
        SummarySection section,
        SectionStyle style,
        IReadOnlyList<SummaryImage> images)
    {
        // PLSP layout: purple section header, clean white/near-white body, teal image border
        container.PaddingVertical(5).PaddingHorizontal(2).Background(style.Background)
            .Border(1).BorderColor("#e5e7eb").Column(col =>
        {
            // Purple section header bar
            col.Item().Background(style.AccentColor).PaddingVertical(8).PaddingHorizontal(14)
                .Text(section.Title).FontSize(12).SemiBold().FontColor("#FFFFFF");

            col.Item().PaddingHorizontal(14).PaddingVertical(8).Column(bodyCol =>
            {
                bodyCol.Spacing(5);
                var lines = section.Body.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    // Bullet points
                    var isBullet = line.TrimStart().StartsWith("- ", StringComparison.Ordinal)
                                || line.TrimStart().StartsWith("• ", StringComparison.Ordinal)
                                || line.TrimStart().StartsWith("* ", StringComparison.Ordinal);
                    var lineText = isBullet
                        ? "• " + line.TrimStart().TrimStart('-', '•', '*', ' ')
                        : line;

                    bodyCol.Item().Text(text =>
                    {
                        text.DefaultTextStyle(ts => ts.FontSize(12).LineHeight(1.6f).FontColor("#1F2937"));
                        foreach (var segment in ParseBoldSegments(lineText))
                        {
                            var span = text.Span(segment.Text);
                            if (segment.IsBold)
                            {
                                span.SemiBold().FontColor("#6b4fa0");
                            }
                        }
                    });
                }
            });

            // Images placed in this section — teal border (PLSP style)
            if (images.Count > 0)
            {
                col.Item().PaddingHorizontal(14).PaddingBottom(10).Column(imageCol =>
                {
                    foreach (var image in images)
                    {
                        imageCol.Item().Background("#FFFFFF").Border(2).BorderColor("#47b5a0").Padding(6)
                            .Column(img =>
                            {
                                img.Item().AlignCenter().Image(image.Content).FitWidth();
                                if (!string.IsNullOrWhiteSpace(image.Caption))
                                {
                                    img.Item().PaddingTop(4).AlignCenter()
                                        .Text(image.Caption!).FontSize(9).FontColor("#6b4fa0").Italic();
                                }
                            });
                    }
                });
            }
        });
    }

    private static IReadOnlyList<SummaryImage> MatchImagesForSection(string title, List<SummaryImage> remaining)
    {
        if (remaining.Count == 0)
        {
            return Array.Empty<SummaryImage>();
        }

        var matches = remaining
            .Where(image => PlacementMatchesSection(image.Placement, title)
                || CaptionMatchesSection(image.Caption ?? string.Empty, title))
            .ToList();

        if (matches.Count == 0)
        {
            return Array.Empty<SummaryImage>();
        }

        foreach (var match in matches)
        {
            remaining.Remove(match);
        }

        return matches;
    }

    private static bool CaptionMatchesSection(string caption, string title)
    {
        if (string.IsNullOrWhiteSpace(caption) || string.IsNullOrWhiteSpace(title))
        {
            return false;
        }

        var normalizedCaption = caption.ToLowerInvariant();
        var normalizedTitle = title.ToLowerInvariant();
        if (normalizedTitle.Contains("how was") || normalizedTitle.Contains("study done"))
        {
            return normalizedCaption.Contains("design");
        }

        if (normalizedTitle.Contains("results"))
        {
            return normalizedCaption.Contains("result") || normalizedCaption.Contains("outcome");
        }

        if (normalizedTitle.Contains("side effects") || normalizedTitle.Contains("safety"))
        {
            return normalizedCaption.Contains("safety") || normalizedCaption.Contains("adverse");
        }

        return normalizedCaption.Contains(normalizedTitle);
    }

    private static bool PlacementMatchesSection(string? placement, string title)
    {
        if (string.IsNullOrWhiteSpace(placement) || string.IsNullOrWhiteSpace(title))
        {
            return false;
        }

        return placement.Trim().Equals(title.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<(string Text, bool IsBold)> ParseBoldSegments(string line)
    {
        if (string.IsNullOrEmpty(line))
        {
            yield break;
        }

        var pattern = new Regex(@"\*\*(?<bold>.+?)\*\*");
        var lastIndex = 0;
        foreach (Match match in pattern.Matches(line))
        {
            if (match.Index > lastIndex)
            {
                yield return (line.Substring(lastIndex, match.Index - lastIndex), false);
            }

            var boldText = match.Groups["bold"].Value;
            if (!string.IsNullOrEmpty(boldText))
            {
                yield return (boldText, true);
            }

            lastIndex = match.Index + match.Length;
        }

        if (lastIndex < line.Length)
        {
            yield return (line[lastIndex..], false);
        }
    }
}

public sealed record SummaryExportData(
    string DocumentName,
    string? Title,
    string? TherapeuticArea,
    string? Audience,
    SummaryLane Lane,
    string SummaryText,
    DateTimeOffset GeneratedAt)
{
    public string LaneLabel => Lane.ToString();
    public string DisplayTitle => string.IsNullOrWhiteSpace(Title) ? DocumentName : Title!;
}

public sealed class SummarySection
{
    public SummarySection(string title, string body)
    {
        Title = title;
        Body = body;
    }

    public string Title { get; set; }
    public string Body { get; set; }
}

public sealed record SectionStyle(string Background, string TitleColor, string AccentColor);
