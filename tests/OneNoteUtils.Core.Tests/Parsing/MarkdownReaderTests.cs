using FluentAssertions;
using OneNoteUtils.Core.Models;
using OneNoteUtils.Core.Parsing;

namespace OneNoteUtils.Core.Tests.Parsing;

public class MarkdownReaderTests
{
    [Fact]
    public void Parse_Heading()
    {
        var elements = MarkdownReader.Parse("## My Heading");

        elements.Should().HaveCount(1);
        elements[0].Should().BeOfType<Heading>()
            .Which.Level.Should().Be(2);
        ((Heading)elements[0]).Text.Should().Be("My Heading");
    }

    [Fact]
    public void Parse_MultipleLevelHeadings()
    {
        var elements = MarkdownReader.Parse("# H1\n## H2\n### H3");

        elements.Should().HaveCount(3);
        ((Heading)elements[0]).Level.Should().Be(1);
        ((Heading)elements[1]).Level.Should().Be(2);
        ((Heading)elements[2]).Level.Should().Be(3);
    }

    [Fact]
    public void Parse_Paragraph()
    {
        var elements = MarkdownReader.Parse("Hello world");

        elements.Should().HaveCount(1);
        elements[0].Should().BeOfType<Paragraph>();
        var runs = ((Paragraph)elements[0]).Runs;
        runs.Should().HaveCount(1);
        runs[0].Text.Should().Be("Hello world");
    }

    [Fact]
    public void Parse_BoldInline()
    {
        var elements = MarkdownReader.Parse("This is **bold** text");

        var para = elements[0].Should().BeOfType<Paragraph>().Subject;
        para.Runs.Should().HaveCountGreaterThanOrEqualTo(3);
        var boldRun = para.Runs.First(r => r.Bold);
        boldRun.Text.Should().Be("bold");
    }

    [Fact]
    public void Parse_ItalicInline()
    {
        var elements = MarkdownReader.Parse("This is *italic* text");

        var para = elements[0].Should().BeOfType<Paragraph>().Subject;
        para.Runs.Should().Contain(r => r.Italic && r.Text == "italic");
    }

    [Fact]
    public void Parse_StrikethroughInline()
    {
        var elements = MarkdownReader.Parse("This is ~~struck~~ text");

        var para = elements[0].Should().BeOfType<Paragraph>().Subject;
        para.Runs.Should().Contain(r => r.Strikethrough && r.Text == "struck");
    }

    [Fact]
    public void Parse_InlineCode()
    {
        var elements = MarkdownReader.Parse("Use `dotnet build` here");

        var para = elements[0].Should().BeOfType<Paragraph>().Subject;
        para.Runs.Should().Contain(r => r.Code && r.Text == "dotnet build");
    }

    [Fact]
    public void Parse_HighlightInline()
    {
        var elements = MarkdownReader.Parse("Please ==update your notes== now");

        var para = elements[0].Should().BeOfType<Paragraph>().Subject;
        para.Runs.Should().Contain(r => r.Highlight && !r.Bold && r.Text == "update your notes");
    }

    [Fact]
    public void Parse_BoldHighlightInline()
    {
        var elements = MarkdownReader.Parse("**==owners update==** and ==**mirror**==");

        var para = elements[0].Should().BeOfType<Paragraph>().Subject;
        para.Runs.Should().Contain(r => r.Highlight && r.Bold && r.Text == "owners update");
        para.Runs.Should().Contain(r => r.Highlight && r.Bold && r.Text == "mirror");
    }

    [Fact]
    public void Parse_Link()
    {
        var elements = MarkdownReader.Parse("Visit [GitHub](https://github.com)");

        var para = elements[0].Should().BeOfType<Paragraph>().Subject;
        var linkRun = para.Runs.First(r => r.HrefUrl != null);
        linkRun.Text.Should().Be("GitHub");
        linkRun.HrefUrl.Should().Be("https://github.com");
    }

    [Fact]
    public void Parse_BoldItalic()
    {
        var elements = MarkdownReader.Parse("***bold italic***");

        var para = elements[0].Should().BeOfType<Paragraph>().Subject;
        para.Runs.Should().Contain(r => r.Bold && r.Italic);
    }

    [Fact]
    public void Parse_BulletList()
    {
        var elements = MarkdownReader.Parse("- Alpha\n- Bravo\n- Charlie");

        elements.Should().HaveCount(1);
        var list = elements[0].Should().BeOfType<BulletList>().Subject;
        list.Items.Should().HaveCount(3);
    }

    [Fact]
    public void Parse_NestedBulletList()
    {
        var elements = MarkdownReader.Parse("- Parent\n    - Child");

        var list = elements[0].Should().BeOfType<BulletList>().Subject;
        list.Items.Should().HaveCount(1);
        list.Items[0].Children.Should().NotBeNull();
        list.Items[0].Children.Should().HaveCount(1);
    }

    [Fact]
    public void Parse_NumberedList()
    {
        var elements = MarkdownReader.Parse("1. First\n1. Second\n1. Third");

        elements.Should().HaveCount(1);
        var list = elements[0].Should().BeOfType<NumberedList>().Subject;
        list.Items.Should().HaveCount(3);
    }

    [Fact]
    public void Parse_Table()
    {
        var md = "| A | B |\n| --- | --- |\n| 1 | 2 |\n| 3 | 4 |";
        var elements = MarkdownReader.Parse(md);

        elements.Should().HaveCount(1);
        var table = elements[0].Should().BeOfType<Table>().Subject;
        table.Rows.Should().HaveCount(3); // header + 2 data rows
        table.Rows[0].Cells.Should().HaveCount(2);
    }

    [Fact]
    public void Parse_CodeBlock()
    {
        var md = "```kql\nStormEvents\n| where State == 'TX'\n```";
        var elements = MarkdownReader.Parse(md);

        elements.Should().HaveCount(1);
        var code = elements[0].Should().BeOfType<CodeBlock>().Subject;
        code.Language.Should().Be("kql");
        code.Code.Should().Contain("StormEvents");
        code.Code.Should().Contain("| where");
    }

    [Fact]
    public void Parse_CodeBlockNoLanguage()
    {
        var md = "```\nsome code\n```";
        var elements = MarkdownReader.Parse(md);

        var code = elements[0].Should().BeOfType<CodeBlock>().Subject;
        code.Language.Should().BeNull();
        code.Code.Should().Be("some code");
    }

    [Fact]
    public void Parse_HorizontalRule()
    {
        var elements = MarkdownReader.Parse("text\n\n***\n\nmore text");

        elements.Should().HaveCount(3);
        elements[1].Should().BeOfType<HorizontalRule>();
    }

    [Fact]
    public void Parse_Blockquote()
    {
        var elements = MarkdownReader.Parse("> This is quoted");

        elements.Should().HaveCount(1);
        var bq = elements[0].Should().BeOfType<Blockquote>().Subject;
        bq.Elements.Should().HaveCount(1);
    }

    [Fact]
    public void Parse_StripsFrontmatter()
    {
        var md = "---\ntitle: Test\n---\n\n## Content";
        var elements = MarkdownReader.Parse(md);

        elements.Should().HaveCount(1);
        elements[0].Should().BeOfType<Heading>();
        ((Heading)elements[0]).Text.Should().Be("Content");
    }

    [Fact]
    public void Parse_ExtractTitle_FromH1()
    {
        var title = MarkdownReader.ExtractTitle("# My Page Title\n\nSome content");
        title.Should().Be("My Page Title");
    }

    [Fact]
    public void Parse_ExtractTitle_NullWhenNoH1()
    {
        var title = MarkdownReader.ExtractTitle("## Not h1\n\nSome content");
        title.Should().BeNull();
    }

    [Fact]
    public void Parse_ExtractTitle_SkipsFrontmatter()
    {
        var title = MarkdownReader.ExtractTitle("---\ntitle: Meta\n---\n\n# Real Title");
        title.Should().Be("Real Title");
    }

    [Fact]
    public void Parse_Image_WikiLink()
    {
        var elements = MarkdownReader.Parse("![[image.png]]");

        elements.Should().HaveCount(1);
        elements[0].Should().BeOfType<Image>();
        ((Image)elements[0]).FileName.Should().Be("image.png");
    }

    [Fact]
    public void Parse_Image_StandardMarkdown()
    {
        var elements = MarkdownReader.Parse("![alt text](photo.jpg)");

        elements.Should().HaveCount(1);
        elements[0].Should().BeOfType<Image>();
        ((Image)elements[0]).Format.Should().Be("jpg");
    }

    [Fact]
    public void Parse_Table_CellWithInlineImage()
    {
        var md = "| KPI | Notes |\n| --- | --- |\n| Foo | before ![[shot.png]] after |";
        var elements = MarkdownReader.Parse(md);

        elements.Should().HaveCount(1);
        var table = (Table)elements[0];
        var noteCell = table.Rows[1].Cells[1];

        noteCell.Elements.OfType<Image>().Should().ContainSingle()
            .Which.FileName.Should().Be("shot.png");
        noteCell.Elements.OfType<Paragraph>().Should().HaveCount(2);
    }

    [Fact]
    public void Parse_MixedContent()
    {
        var md = "# Title\n\nA paragraph.\n\n- Bullet\n\n---\n\n> Quote";
        var elements = MarkdownReader.Parse(md);

        elements.Should().HaveCount(5);
        elements[0].Should().BeOfType<Heading>();
        elements[1].Should().BeOfType<Paragraph>();
        elements[2].Should().BeOfType<BulletList>();
        elements[3].Should().BeOfType<HorizontalRule>();
        elements[4].Should().BeOfType<Blockquote>();
    }
}
