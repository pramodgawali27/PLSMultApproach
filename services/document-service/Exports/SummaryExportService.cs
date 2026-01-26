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
        var palette = new[]
        {
            new SectionStyle("#EAF2FF", "#1B2A4E"),
            new SectionStyle("#F5F0FF", "#3B2A5F"),
            new SectionStyle("#EAF7F1", "#1F4A3A"),
            new SectionStyle("#FFF4E5", "#6B3A1B")
        };

        var document = QDocument.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(40);
                page.DefaultTextStyle(style => style.FontFamily("DejaVu Sans").FontSize(11).FontColor("#1F2937"));
                page.Header().Element(header => BuildPdfHeader(header, data));
                page.Content().Column(column =>
                {
                    if (images.Count > 0)
                    {
                        column.Item().Element(content => BuildImageRow(content, images));
                    }

                    foreach (var (section, index) in sections.Select((section, index) => (section, index)))
                    {
                        var style = palette[index % palette.Length];
                        column.Item().Element(content => BuildSectionCard(content, section, style));
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
        body.Append(BuildDocxSummary(data));
        body.Append(sectionProperties);
        mainPart.Document.Body = body;
        mainPart.Document.Save();

        return stream.ToArray();
    }

    private static void BuildPdfHeader(IContainer container, SummaryExportData data)
    {
        container.Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                col.Item().Text("Plain Language Summary").FontSize(18).SemiBold().FontColor("#0F172A");
                col.Item().Text(data.DocumentName).FontSize(10).FontColor("#64748B");
            });
            row.ConstantItem(160).AlignRight().Text(text =>
            {
                text.DefaultTextStyle(style => style.FontSize(10).FontColor("#334155"));
                text.Span("Lane ");
                text.Span(data.LaneLabel.Replace("Lane", string.Empty, StringComparison.OrdinalIgnoreCase))
                    .SemiBold();
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
        var lines = NormalizeSummaryText(data.SummaryText)
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

    private static string NormalizeSummaryText(string text)
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

    private static void BuildSectionCard(IContainer container, SummarySection section, SectionStyle style)
    {
        container.PaddingVertical(6).PaddingHorizontal(2).Background(style.Background).Border(1).BorderColor("#E2E8F0").Padding(14).Column(col =>
        {
            col.Item().Text(section.Title).FontSize(13).SemiBold().FontColor(style.TitleColor);
            col.Item().PaddingTop(6).Text(section.Body).FontSize(11).LineHeight(1.4f).FontColor("#1F2937");
        });
    }

    private static void BuildImageRow(IContainer container, IReadOnlyList<SummaryImage> images)
    {
        container.PaddingBottom(12).Column(column =>
        {
            column.Item().Text("Key figures").FontSize(12).SemiBold().FontColor("#0F172A");
            column.Item().Row(row =>
            {
                foreach (var image in images)
                {
                    row.RelativeItem().PaddingRight(6).Border(1).BorderColor("#E2E8F0").Padding(6)
                        .Background("#FFFFFF")
                        .Column(col =>
                        {
                            col.Item().AlignMiddle().Image(image.Content).FitArea();
                            if (!string.IsNullOrWhiteSpace(image.Caption))
                            {
                                col.Item().PaddingTop(4).Text(image.Caption!).FontSize(9).FontColor("#475569");
                            }
                        });
                }
            });
        });
    }
}

public sealed record SummaryExportData(
    string DocumentName,
    SummaryLane Lane,
    string SummaryText,
    DateTimeOffset GeneratedAt)
{
    public string LaneLabel => Lane.ToString();
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

public sealed record SectionStyle(string Background, string TitleColor);
