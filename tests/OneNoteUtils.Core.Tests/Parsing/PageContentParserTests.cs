using FluentAssertions;
using OneNoteUtils.Core.Models;
using OneNoteUtils.Core.Parsing;

namespace OneNoteUtils.Core.Tests.Parsing;

public class PageContentParserTests
{
    private readonly string _pageXml = FixtureHelper.LoadFixture("page-content-sample.xml");

    [Fact]
    public void ParsePageContent_ReturnsPopulatedPage()
    {
        var stub = new Page("page-001", "Welcome Page", 1, null, []);

        var page = PageContentParser.ParsePageContent(stub, _pageXml);

        page.Elements.Should().NotBeEmpty();
        page.PageId.Should().Be("page-001");
        page.Title.Should().Be("Welcome Page");
    }

    [Fact]
    public void ParsePageContent_ParsesSimpleParagraph()
    {
        var stub = new Page("page-001", "Welcome Page", 1, null, []);
        var page = PageContentParser.ParsePageContent(stub, _pageXml);

        var paragraph = page.Elements.OfType<Paragraph>().FirstOrDefault();
        paragraph.Should().NotBeNull();

        var text = string.Concat(paragraph!.Runs.Select(r => r.Text));
        text.Should().Contain("simple paragraph");
    }

    [Fact]
    public void ParsePageContent_ParsesBoldAndItalic()
    {
        var stub = new Page("page-001", "Welcome Page", 1, null, []);
        var page = PageContentParser.ParsePageContent(stub, _pageXml);

        var paragraph = page.Elements.OfType<Paragraph>().FirstOrDefault();
        paragraph.Should().NotBeNull();

        var boldRun = paragraph!.Runs.FirstOrDefault(r => r.Bold);
        boldRun.Should().NotBeNull();
        boldRun!.Text.Should().Be("bold");

        var italicRun = paragraph.Runs.FirstOrDefault(r => r.Italic);
        italicRun.Should().NotBeNull();
        italicRun!.Text.Should().Be("italic");
    }

    [Fact]
    public void ParsePageContent_ParsesHeading()
    {
        var stub = new Page("page-001", "Welcome Page", 1, null, []);
        var page = PageContentParser.ParsePageContent(stub, _pageXml);

        var heading = page.Elements.OfType<Heading>().FirstOrDefault();
        heading.Should().NotBeNull();
        heading!.Text.Should().Be("Section Heading");
        heading.Level.Should().Be(3); // h2 style → level 3 (offset by 1)
    }

    [Fact]
    public void ParsePageContent_ParsesBulletList()
    {
        var stub = new Page("page-001", "Welcome Page", 1, null, []);
        var page = PageContentParser.ParsePageContent(stub, _pageXml);

        var bulletList = page.Elements.OfType<BulletList>().FirstOrDefault();
        bulletList.Should().NotBeNull();
        bulletList!.Items.Should().HaveCount(2);
    }

    [Fact]
    public void ParsePageContent_ParsesNumberedList()
    {
        var stub = new Page("page-001", "Welcome Page", 1, null, []);
        var page = PageContentParser.ParsePageContent(stub, _pageXml);

        var numberedList = page.Elements.OfType<NumberedList>().FirstOrDefault();
        numberedList.Should().NotBeNull();
        numberedList!.Items.Should().HaveCount(2);
        numberedList.Items[0].NumberText.Should().Be("1.");
        numberedList.Items[1].NumberText.Should().Be("2.");
    }

    [Fact]
    public void ParsePageContent_ParsesLink()
    {
        var stub = new Page("page-001", "Welcome Page", 1, null, []);
        var page = PageContentParser.ParsePageContent(stub, _pageXml);

        var allRuns = page.Elements.OfType<Paragraph>().SelectMany(p => p.Runs);
        var linkRun = allRuns.FirstOrDefault(r => r.HrefUrl != null);
        linkRun.Should().NotBeNull();
        linkRun!.HrefUrl.Should().Be("https://example.com");
        linkRun.Text.Should().Be("Example Site");
    }

    [Fact]
    public void ParsePageContent_ParsesStrikethrough()
    {
        var stub = new Page("page-001", "Welcome Page", 1, null, []);
        var page = PageContentParser.ParsePageContent(stub, _pageXml);

        var allRuns = page.Elements.OfType<Paragraph>().SelectMany(p => p.Runs);
        var strikeRun = allRuns.FirstOrDefault(r => r.Strikethrough);
        strikeRun.Should().NotBeNull();
        strikeRun!.Text.Should().Be("struck text");
    }

    [Fact]
    public void ParsePageContent_ParsesTable()
    {
        var stub = new Page("page-001", "Welcome Page", 1, null, []);
        var page = PageContentParser.ParsePageContent(stub, _pageXml);

        var table = page.Elements.OfType<Table>().FirstOrDefault();
        table.Should().NotBeNull();
        table!.Rows.Should().HaveCount(2);
        table.Rows[0].Cells.Should().HaveCount(2);
    }
}
