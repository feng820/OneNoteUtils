using FluentAssertions;
using OneNoteUtils.Core.Models;
using OneNoteUtils.Core.Parsing;

namespace OneNoteUtils.Core.Tests.Parsing;

public class HtmlFragmentParserTests
{
    [Fact]
    public void ParseHtmlToRuns_PlainText()
    {
        var runs = HtmlFragmentParser.ParseHtmlToRuns("Hello world");

        runs.Should().HaveCount(1);
        runs[0].Text.Should().Be("Hello world");
        runs[0].Bold.Should().BeFalse();
    }

    [Fact]
    public void ParseHtmlToRuns_BoldTag()
    {
        var runs = HtmlFragmentParser.ParseHtmlToRuns("Hello <b>world</b>");

        runs.Should().HaveCountGreaterThanOrEqualTo(2);
        var boldRun = runs.First(r => r.Bold);
        boldRun.Text.Should().Be("world");
    }

    [Fact]
    public void ParseHtmlToRuns_ItalicTag()
    {
        var runs = HtmlFragmentParser.ParseHtmlToRuns("<em>emphasis</em>");

        runs.Should().ContainSingle();
        runs[0].Italic.Should().BeTrue();
        runs[0].Text.Should().Be("emphasis");
    }

    [Fact]
    public void ParseHtmlToRuns_StrikethroughTag()
    {
        var runs = HtmlFragmentParser.ParseHtmlToRuns("<s>deleted</s>");

        runs.Should().ContainSingle();
        runs[0].Strikethrough.Should().BeTrue();
        runs[0].Text.Should().Be("deleted");
    }

    [Fact]
    public void ParseHtmlToRuns_Anchor()
    {
        var runs = HtmlFragmentParser.ParseHtmlToRuns("<a href=\"https://example.com\">link</a>");

        runs.Should().ContainSingle();
        runs[0].HrefUrl.Should().Be("https://example.com");
        runs[0].Text.Should().Be("link");
    }

    [Fact]
    public void ParseHtmlToRuns_NestedFormatting()
    {
        var runs = HtmlFragmentParser.ParseHtmlToRuns("<b><i>bold italic</i></b>");

        runs.Should().ContainSingle();
        runs[0].Bold.Should().BeTrue();
        runs[0].Italic.Should().BeTrue();
        runs[0].Text.Should().Be("bold italic");
    }

    [Fact]
    public void ParseHtmlToRuns_SpanWithStyle()
    {
        var runs = HtmlFragmentParser.ParseHtmlToRuns(
            "<span style=\"font-weight: bold; font-style: italic\">styled</span>");

        runs.Should().ContainSingle();
        runs[0].Bold.Should().BeTrue();
        runs[0].Italic.Should().BeTrue();
    }

    [Fact]
    public void ParseHtmlToRuns_BrTag()
    {
        var runs = HtmlFragmentParser.ParseHtmlToRuns("line1<br/>line2");

        runs.Should().HaveCount(3);
        runs[1].Text.Should().Be("\n");
    }

    [Fact]
    public void ParseHtmlToRuns_EmptyReturnsEmpty()
    {
        var runs = HtmlFragmentParser.ParseHtmlToRuns("");
        runs.Should().BeEmpty();

        var runsNull = HtmlFragmentParser.ParseHtmlToRuns("   ");
        runsNull.Should().BeEmpty();
    }

    [Fact]
    public void ParseHtmlToRuns_MalformedHtmlFallsBack()
    {
        // Unclosed tags should fall back to plain text stripping
        var runs = HtmlFragmentParser.ParseHtmlToRuns("<b>unclosed");

        runs.Should().NotBeEmpty();
    }

    [Fact]
    public void ParseHtmlToRuns_UnderlineTag()
    {
        var runs = HtmlFragmentParser.ParseHtmlToRuns("<u>underlined</u>");

        runs.Should().ContainSingle();
        runs[0].Underline.Should().BeTrue();
        runs[0].Text.Should().Be("underlined");
    }
}
