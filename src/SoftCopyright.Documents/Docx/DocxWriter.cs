using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace SoftCopyright.Documents.Docx;

/// <summary>DOCX 构建辅助：统一中文字体、字号与段落格式。</summary>
public static class DocxWriter
{
    public const string BodyFont = "宋体";
    public const string CodeFont = "Consolas";

    public static DocxDocument Create() => new();

    public static void AddParagraph(
        DocxDocument document,
        string text,
        string font = BodyFont,
        int halfPointSize = 24,
        bool bold = false,
        JustificationValues? justification = null,
        bool pageBreakBefore = false)
    {
        var runProperties = new RunProperties(
            new RunFonts { Ascii = font, HighAnsi = font, EastAsia = font },
            new FontSize { Val = halfPointSize.ToString() });

        if (bold)
        {
            runProperties.PrependChild(new Bold());
        }

        var paragraphProperties = new ParagraphProperties(
            new SpacingBetweenLines { After = "120", Line = "312", LineRule = LineSpacingRuleValues.Auto });

        if (justification is not null)
        {
            paragraphProperties.Append(new Justification { Val = justification.Value });
        }

        var paragraph = new Paragraph(paragraphProperties);

        if (pageBreakBefore)
        {
            paragraph.Append(new Run(new Break { Type = BreakValues.Page }));
        }

        paragraph.Append(new Run(runProperties, new Text(text) { Space = SpaceProcessingModeValues.Preserve }));
        document.Body.Append(paragraph);
    }

    public static void AddHeading(DocxDocument document, string text, int level = 1, bool pageBreakBefore = false)
    {
        var size = level switch
        {
            1 => 32,
            2 => 28,
            _ => 24
        };

        AddParagraph(document, text, BodyFont, size, bold: true, pageBreakBefore: pageBreakBefore);
    }

    public static void AddCover(DocxDocument document, string title, string subtitle, IReadOnlyList<string> lines)
    {
        AddParagraph(document, string.Empty, BodyFont, 24);
        AddParagraph(document, title, BodyFont, 44, bold: true, justification: JustificationValues.Center);
        AddParagraph(document, subtitle, BodyFont, 28, justification: JustificationValues.Center);
        AddParagraph(document, string.Empty, BodyFont, 24);

        foreach (var line in lines)
        {
            AddParagraph(document, line, BodyFont, 24, justification: JustificationValues.Center);
        }

        AddParagraph(document, string.Empty, BodyFont, 24);
    }
}

/// <summary>封装 DOCX 文档与内存流。</summary>
public sealed class DocxDocument : IDisposable
{
    private readonly MemoryStream _stream;

    public DocxDocument()
    {
        _stream = new MemoryStream();
        Document = WordprocessingDocument.Create(_stream, WordprocessingDocumentType.Document, true);
        var mainPart = Document.AddMainDocumentPart();
        mainPart.Document = new Document(new Body());
        Body = mainPart.Document.Body!;
    }

    public WordprocessingDocument Document { get; }

    public Body Body { get; }

    public byte[] ToBytes()
    {
        Document.Save();
        return _stream.ToArray();
    }

    public void Dispose() => Document.Dispose();
}
