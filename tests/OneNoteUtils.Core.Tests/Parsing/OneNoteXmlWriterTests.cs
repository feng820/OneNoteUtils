using FluentAssertions;
using OneNoteUtils.Core.Models;
using OneNoteUtils.Core.Parsing;

namespace OneNoteUtils.Core.Tests.Parsing;

public class OneNoteXmlWriterTests
{
    [Fact]
    public void BuildPageXml_ContainsPageAndTitle()
    {
        var xml = OneNoteXmlWriter.BuildPageXml("page-1", "Test Page", []);

        xml.Should().Contain("one:Page");
        xml.Should().Contain("ID=\"page-1\"");
        xml.Should().Contain("Test Page");
        xml.Should().Contain("one:Title");
    }

    [Fact]
    public void BuildPageXml_Heading()
    {
        var xml = OneNoteXmlWriter.BuildPageXml("p1", "T", [new Heading(2, "My Heading")]);

        xml.Should().Contain("My Heading");
        xml.Should().Contain("font-weight:bold");
        xml.Should().Contain("font-size:17.0pt"); // h2 = 17pt
    }

    [Fact]
    public void BuildPageXml_Paragraph()
    {
        var xml = OneNoteXmlWriter.BuildPageXml("p1", "T",
            [new Paragraph([new Run("Hello world")])]);

        xml.Should().Contain("Hello world");
        xml.Should().Contain("one:T");
    }

    [Fact]
    public void BuildPageXml_BoldRun()
    {
        var xml = OneNoteXmlWriter.BuildPageXml("p1", "T",
            [new Paragraph([new Run("bold text", Bold: true)])]);

        xml.Should().Contain("font-weight:bold");
        xml.Should().Contain("bold text");
    }

    [Fact]
    public void BuildPageXml_ItalicRun()
    {
        var xml = OneNoteXmlWriter.BuildPageXml("p1", "T",
            [new Paragraph([new Run("italic", Italic: true)])]);

        xml.Should().Contain("font-style:italic");
    }

    [Fact]
    public void BuildPageXml_LinkRun()
    {
        var xml = OneNoteXmlWriter.BuildPageXml("p1", "T",
            [new Paragraph([new Run("click", HrefUrl: "https://example.com")])]);

        xml.Should().Contain("href=");
        xml.Should().Contain("https://example.com");
        xml.Should().Contain("click");
    }

    [Fact]
    public void BuildPageXml_InlineCodeRun()
    {
        var xml = OneNoteXmlWriter.BuildPageXml("p1", "T",
            [new Paragraph([new Run("code", Code: true)])]);

        xml.Should().Contain("Consolas");
        xml.Should().Contain("code");
    }

    [Fact]
    public void BuildPageXml_InlineCodeRun_IsConsolasRed()
    {
        var xml = OneNoteXmlWriter.BuildPageXml("p1", "T",
            [new Paragraph([new Run("MDSFormatS360Output", Code: true)])]);

        xml.Should().Contain("font-family:Consolas");
        xml.Should().Contain("color:#DA3900");
    }

    [Fact]
    public void BuildPageXml_HighlightRun_YellowBackground()
    {
        var xml = OneNoteXmlWriter.BuildPageXml("p1", "T",
            [new Paragraph([new Run("update your notes", Bold: true, Highlight: true)])]);

        xml.Should().Contain("background:yellow");
        xml.Should().Contain("font-weight:bold");
    }

    [Fact]
    public void BuildPageXml_BulletList()
    {
        var xml = OneNoteXmlWriter.BuildPageXml("p1", "T",
            [new BulletList([
                new ListItem([new Paragraph([new Run("Item 1")])]),
                new ListItem([new Paragraph([new Run("Item 2")])])
            ])]);

        xml.Should().Contain("one:Bullet");
        xml.Should().Contain("Item 1");
        xml.Should().Contain("Item 2");
    }

    [Fact]
    public void BuildPageXml_NumberedList()
    {
        var xml = OneNoteXmlWriter.BuildPageXml("p1", "T",
            [new NumberedList([
                new ListItem([new Paragraph([new Run("Step 1")])], NumberText: "1."),
                new ListItem([new Paragraph([new Run("Step 2")])], NumberText: "2.")
            ])]);

        xml.Should().Contain("one:Number");
        xml.Should().Contain("Step 1");
        xml.Should().Contain("Step 2");
    }

    [Fact]
    public void BuildPageXml_Table()
    {
        var xml = OneNoteXmlWriter.BuildPageXml("p1", "T",
            [new Table([
                new TableRow([
                    new TableCell([new Paragraph([new Run("A")])]),
                    new TableCell([new Paragraph([new Run("B")])])
                ])
            ])]);

        xml.Should().Contain("one:Table");
        xml.Should().Contain("one:Row");
        xml.Should().Contain("one:Cell");
        xml.Should().Contain("one:Column");
    }

    [Fact]
    public void BuildPageXml_TableCellWithImage()
    {
        var bytes = new byte[] { 0x89, 0x50, 0x4E, 0x47 };
        var xml = OneNoteXmlWriter.BuildPageXml("p1", "T",
            [new Table([
                new TableRow([
                    new TableCell([new Paragraph([new Run("KPI")])]),
                    new TableCell([
                        new Paragraph([new Run("caption")]),
                        new Image("shot.png", "png", () => bytes)
                    ])
                ])
            ])]);

        xml.Should().Contain("caption");
        xml.Should().Contain("one:Image");
        xml.Should().Contain("one:Data");

        // the image must be emitted inside a table cell
        var cellStart = xml.IndexOf("<one:Cell>");
        var imageStart = xml.IndexOf("<one:Image");
        cellStart.Should().BeGreaterThan(-1);
        imageStart.Should().BeGreaterThan(cellStart);
    }

    [Fact]
    public void Push_TableCellImage_WithSpacedAbsolutePath_LoadsBytes()
    {
        // Reproduces the real S360 case: an absolute wikilink whose path
        // contains spaces (e.g. "Test VMs Az Sec pack") inside a table cell.
        string dir = Path.Combine(Path.GetTempPath(), "one note cell img " + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            byte[] bytes = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A };
            string png = Path.Combine(dir, "shot 06.png");
            File.WriteAllBytes(png, bytes);

            string md = "| KPI | Notes |\n| --- | --- |\n| Foo | before ![[" + png + "]] after |";
            var elements = MarkdownReader.Parse(md, dir);
            string xml = OneNoteXmlWriter.BuildPageXml("p1", "T", elements);

            xml.Should().Contain("one:Image");
            xml.Should().Contain(Convert.ToBase64String(bytes));

            int cellStart = xml.IndexOf("<one:Cell>");
            int imageStart = xml.IndexOf("<one:Image");
            imageStart.Should().BeGreaterThan(cellStart);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void BuildPageXml_TableColumnWidths_NotesWiderThanSubItems()
    {
        var table = new Table([
            new TableRow([
                new TableCell([new Paragraph([new Run("KPIName")])]),
                new TableCell([new Paragraph([new Run("Top10SubItems")])]),
                new TableCell([new Paragraph([new Run("Notes")])])
            ]),
            new TableRow([
                new TableCell([new Paragraph([new Run("k")])]),
                new TableCell([new Paragraph([new Run("s")])]),
                new TableCell([new Paragraph([new Run("n")])])
            ])
        ]);

        var xml = OneNoteXmlWriter.BuildPageXml("p1", "T", [table]);

        var widths = System.Text.RegularExpressions.Regex
            .Matches(xml, "<one:Column index=\"(\\d+)\" width=\"(\\d+)\" isLocked=\"true\"/>")
            .Select(m => int.Parse(m.Groups[2].Value))
            .ToList();

        widths.Should().HaveCount(3);
        widths[2].Should().BeGreaterThan(widths[1]); // Notes wider than Top10SubItems
        widths[2].Should().BeGreaterThan(widths[0]); // Notes wider than KPIName
    }

    [Fact]
    public void BuildPageXml_AppliesTemplateFonts_CalibriBody_SegoeCells()
    {
        var elements = new List<ContentElement>
        {
            new Paragraph([new Run("S360 Kusto Query + Data:") { Bold = true }]),
            new Table([
                new TableRow([new TableCell([new Paragraph([new Run("KPIName")])])]),
                new TableRow([new TableCell([new Paragraph([new Run("k")])])])
            ])
        };

        var xml = OneNoteXmlWriter.BuildPageXml("p1", "T", elements);

        // Body paragraphs render as Calibri 11pt (the S360 "p" quick style)
        xml.Should().Contain("<one:T style=\"font-family:Calibri;font-size:11.0pt\">");
        // Table cells render as Segoe UI 9pt (matches the authored S360 table)
        xml.Should().Contain("<one:T style=\"font-family:'Segoe UI';font-size:9.0pt\">");
    }

    [Fact]
    public void BuildPageXml_Image()
    {
        var bytes = new byte[] { 0x89, 0x50, 0x4E, 0x47 };
        var xml = OneNoteXmlWriter.BuildPageXml("p1", "T",
            [new Image("test.png", "png", () => bytes)]);

        xml.Should().Contain("one:Image");
        xml.Should().Contain("one:Data");
        xml.Should().Contain("format=\"png\"");
    }

    [Fact]
    public void BuildPageXml_CodeBlock()
    {
        var xml = OneNoteXmlWriter.BuildPageXml("p1", "T",
            [new CodeBlock("let x = 1;\nlet y = 2;")]);

        xml.Should().Contain("one:Table"); // code blocks are in bordered boxes
        xml.Should().Contain("Consolas");
        xml.Should().Contain("let x = 1;");
        xml.Should().Contain("let y = 2;");
    }

    [Fact]
    public void BuildPageXml_HorizontalRule()
    {
        var xml = OneNoteXmlWriter.BuildPageXml("p1", "T", [new HorizontalRule()]);

        xml.Should().Contain("━"); // horizontal rule character
    }

    [Fact]
    public void BuildPageXml_Blockquote()
    {
        var xml = OneNoteXmlWriter.BuildPageXml("p1", "T",
            [new Blockquote([new Paragraph([new Run("quoted text")])])]);

        xml.Should().Contain("quoted text");
        xml.Should().Contain("font-style:italic");
    }

    [Fact]
    public void BuildPageXml_SpacingBetweenHeadings()
    {
        var xml = OneNoteXmlWriter.BuildPageXml("p1", "T", [
            new Heading(2, "First"),
            new Paragraph([new Run("text")]),
            new Heading(2, "Second")
        ]);

        // Blank lines should separate headings
        var blankLineCount = xml.Split("CDATA[ ]]").Length - 1;
        blankLineCount.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void BuildPageXml_HtmlEncodesProperly()
    {
        var xml = OneNoteXmlWriter.BuildPageXml("p1", "T",
            [new Paragraph([new Run("A < B & C > D")])]);

        xml.Should().Contain("&lt;");
        xml.Should().Contain("&amp;");
        xml.Should().Contain("&gt;");
        xml.Should().NotContain("< B");
    }
}
