using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OneNoteUtils.Core;
using OneNoteUtils.Core.Models;
using OneNoteUtils.Writers.Obsidian;

namespace OneNoteUtils.Writers.Obsidian.Tests;

public class ObsidianMarkdownWriterTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ExportOptions _options;
    private readonly ObsidianMarkdownWriter _writer;

    public ObsidianMarkdownWriterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "OneNoteUtilsTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);

        _options = new ExportOptions
        {
            UseObsidianWikilinks = true,
            EmbedImages = true,
            IncludeFrontmatter = true,
            UseAliasLinks = true
        };

        _writer = new ObsidianMarkdownWriter(_options, NullLogger<ObsidianMarkdownWriter>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void Write_CreatesNotebookFolder()
    {
        var notebook = CreateSimpleNotebook();

        _writer.Write(notebook, _tempDir);

        Directory.Exists(Path.Combine(_tempDir, "Test Notebook")).Should().BeTrue();
    }

    [Fact]
    public void Write_CreatesSectionFolders()
    {
        var notebook = CreateSimpleNotebook();

        _writer.Write(notebook, _tempDir);

        Directory.Exists(Path.Combine(_tempDir, "Test Notebook", "My Section")).Should().BeTrue();
    }

    [Fact]
    public void Write_CreatesMarkdownFiles()
    {
        var notebook = CreateSimpleNotebook();

        _writer.Write(notebook, _tempDir);

        var mdFile = Path.Combine(_tempDir, "Test Notebook", "My Section", "Hello World.md");
        File.Exists(mdFile).Should().BeTrue();
    }

    [Fact]
    public void Write_IncludesFrontmatter()
    {
        var notebook = CreateSimpleNotebook();

        _writer.Write(notebook, _tempDir);

        var content = ReadOutputFile("Test Notebook", "My Section", "Hello World.md");
        content.Should().Contain("---");
        content.Should().Contain("title: \"Hello World\"");
        content.Should().Contain("source: OneNote COM");
    }

    [Fact]
    public void Write_IncludesPageTitle()
    {
        var notebook = CreateSimpleNotebook();

        _writer.Write(notebook, _tempDir);

        var content = ReadOutputFile("Test Notebook", "My Section", "Hello World.md");
        content.Should().Contain("# Hello World");
    }

    [Fact]
    public void Write_WritesHeading()
    {
        var notebook = CreateNotebookWithElements(
            new Heading(2, "My Heading"));

        _writer.Write(notebook, _tempDir);

        var content = ReadOutputFile("Test Notebook", "My Section", "Test Page.md");
        content.Should().Contain("## My Heading");
    }

    [Fact]
    public void Write_WritesParagraphWithFormatting()
    {
        var notebook = CreateNotebookWithElements(
            new Paragraph([
                new Run("Normal text "),
                new Run("bold", Bold: true),
                new Run(" and "),
                new Run("italic", Italic: true)
            ]));

        _writer.Write(notebook, _tempDir);

        var content = ReadOutputFile("Test Notebook", "My Section", "Test Page.md");
        content.Should().Contain("Normal text **bold** and *italic*");
    }

    [Fact]
    public void Write_WritesBulletList()
    {
        var notebook = CreateNotebookWithElements(
            new BulletList([
                new ListItem([new Paragraph([new Run("Item 1")])]),
                new ListItem([new Paragraph([new Run("Item 2")])])
            ]));

        _writer.Write(notebook, _tempDir);

        var content = ReadOutputFile("Test Notebook", "My Section", "Test Page.md");
        content.Should().Contain("- Item 1");
        content.Should().Contain("- Item 2");
    }

    [Fact]
    public void Write_WritesNumberedList()
    {
        var notebook = CreateNotebookWithElements(
            new NumberedList([
                new ListItem([new Paragraph([new Run("First")])], NumberText: "1."),
                new ListItem([new Paragraph([new Run("Second")])], NumberText: "2.")
            ]));

        _writer.Write(notebook, _tempDir);

        var content = ReadOutputFile("Test Notebook", "My Section", "Test Page.md");
        content.Should().Contain("1. First");
        content.Should().Contain("1. Second");
    }

    [Fact]
    public void Write_WritesTable()
    {
        var notebook = CreateNotebookWithElements(
            new Table([
                new TableRow([
                    new TableCell([new Paragraph([new Run("H1")])]),
                    new TableCell([new Paragraph([new Run("H2")])])
                ]),
                new TableRow([
                    new TableCell([new Paragraph([new Run("A")])]),
                    new TableCell([new Paragraph([new Run("B")])])
                ])
            ]));

        _writer.Write(notebook, _tempDir);

        var content = ReadOutputFile("Test Notebook", "My Section", "Test Page.md");
        content.Should().Contain("| H1 | H2 |");
        content.Should().Contain("| --- | --- |");
        content.Should().Contain("| A | B |");
    }

    [Fact]
    public void Write_WritesLink()
    {
        var notebook = CreateNotebookWithElements(
            new Paragraph([
                new Run("Click ", HrefUrl: null),
                new Run("here", HrefUrl: "https://example.com")
            ]));

        _writer.Write(notebook, _tempDir);

        var content = ReadOutputFile("Test Notebook", "My Section", "Test Page.md");
        content.Should().Contain("[here](https://example.com)");
    }

    [Fact]
    public void Write_WritesImageEmbed()
    {
        var imageBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47 }; // PNG magic bytes
        var notebook = CreateNotebookWithElements(
            new Image("test.png", "png", () => imageBytes));

        _writer.Write(notebook, _tempDir);

        var content = ReadOutputFile("Test Notebook", "My Section", "Test Page.md");
        content.Should().Contain("![[");
        content.Should().Contain(".png]]");
    }

    [Fact]
    public void Write_WritesStrikethrough()
    {
        var notebook = CreateNotebookWithElements(
            new Paragraph([new Run("deleted", Strikethrough: true)]));

        _writer.Write(notebook, _tempDir);

        var content = ReadOutputFile("Test Notebook", "My Section", "Test Page.md");
        content.Should().Contain("~~deleted~~");
    }

    [Fact]
    public void Write_SkipsFrontmatterWhenDisabled()
    {
        _options.IncludeFrontmatter = false;
        var notebook = CreateSimpleNotebook();

        _writer.Write(notebook, _tempDir);

        var content = ReadOutputFile("Test Notebook", "My Section", "Hello World.md");
        content.Should().NotContain("---");
    }

    [Fact]
    public void Write_CreatesSubpageLinks()
    {
        var pages = new List<Page>
        {
            new("p1", "Parent Page", 1, null, [new Paragraph([new Run("Parent content")])]),
            new("p2", "Child Page", 2, null, [new Paragraph([new Run("Child content")])])
        };

        var notebook = new Notebook("Test Notebook", [new Section("My Section", pages)]);

        _writer.Write(notebook, _tempDir);

        // Parent page should have subpage links
        var parentFolder = Path.Combine(_tempDir, "Test Notebook", "My Section", "Parent Page");
        var parentMd = Path.Combine(parentFolder, "Parent Page.md");
        File.Exists(parentMd).Should().BeTrue();
        var content = File.ReadAllText(parentMd);
        content.Should().Contain("## Subpages");
        content.Should().Contain("Child Page");
    }

    // --- Helpers ---

    private Notebook CreateSimpleNotebook()
    {
        var page = new Page("page-1", "Hello World", 1, null,
            [new Paragraph([new Run("Simple content")])]);
        return new Notebook("Test Notebook", [new Section("My Section", [page])]);
    }

    private Notebook CreateNotebookWithElements(params ContentElement[] elements)
    {
        var page = new Page("page-1", "Test Page", 1, null, elements.ToList());
        return new Notebook("Test Notebook", [new Section("My Section", [page])]);
    }

    private string ReadOutputFile(params string[] pathParts)
    {
        var fullPath = Path.Combine([_tempDir, .. pathParts]);
        return File.ReadAllText(fullPath);
    }
}
