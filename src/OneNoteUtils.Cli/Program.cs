using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OneNoteUtils.Core;
using OneNoteUtils.Core.Parsing;
using OneNoteUtils.OneNote;
using OneNoteUtils.Writers.Obsidian;

// --- Parse CLI arguments ---
var (notebookName, outputPath, configPath, verbose) = ParseArgs(args);

if (string.IsNullOrEmpty(notebookName) || string.IsNullOrEmpty(outputPath))
{
    PrintUsage();
    return 1;
}

// --- Load configuration ---
var configBuilder = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true);

if (!string.IsNullOrEmpty(configPath))
    configBuilder.AddJsonFile(configPath, optional: false);

var config = configBuilder.Build();

var options = new ExportOptions
{
    NotebookIdentifier = notebookName,
    OutputPath = outputPath
};

config.GetSection("ExportOptions").Bind(options);
options.NotebookIdentifier = notebookName;
options.OutputPath = outputPath;

// --- Set up DI ---
var services = new ServiceCollection();

services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(verbose ? LogLevel.Debug : LogLevel.Information);
});

services.AddSingleton(options);
services.AddTransient<IOneNoteSource, ComOneNoteSource>();
services.AddSingleton<INotebookWriter, ObsidianMarkdownWriter>();

var provider = services.BuildServiceProvider();

// --- Run export ---
return RunExport(provider, notebookName, outputPath, options);

// --- Export logic ---
static int RunExport(ServiceProvider provider, string notebookName, string outputPath, ExportOptions options)
{
    var logger = provider.GetRequiredService<ILogger<Program>>();

    try
    {
        logger.LogInformation("Starting export of notebook '{Notebook}' to '{Output}'",
            notebookName, outputPath);

        var source = provider.GetRequiredService<IOneNoteSource>();
        var writer = provider.GetRequiredService<INotebookWriter>();

        // 1. Get hierarchy and parse notebook structure
        logger.LogInformation("Reading notebook hierarchy...");
        var hierarchyXml = source.GetHierarchyXml();
        var notebook = HierarchyParser.ParseNotebook(hierarchyXml, notebookName, options);

        if (notebook == null)
        {
            logger.LogError("Notebook '{Notebook}' not found. Make sure it is open in OneNote.", notebookName);
            return 1;
        }

        logger.LogInformation("Found notebook: {Name} ({SectionCount} sections)",
            notebook.Name, notebook.Sections.Count);

        // 2. Parse page content for each page
        var totalPages = 0;
        var failedPages = 0;
        var populatedSections = new List<OneNoteUtils.Core.Models.Section>();

        foreach (var section in notebook.Sections)
        {
            var populatedPages = new List<OneNoteUtils.Core.Models.Page>();

            var sectionPageCount = section.Pages.Count;
            foreach (var page in section.Pages)
            {
                totalPages++;
                logger.LogInformation("[{Current}/{Total}] Fetching: {PageTitle}",
                    totalPages, sectionPageCount, page.Title);
                try
                {
                    var pageXml = source.GetPageContentXml(page.PageId);
                    var populatedPage = PageContentParser.ParsePageContent(page, pageXml);
                    populatedPages.Add(populatedPage);
                }
                catch (Exception ex)
                {
                    failedPages++;
                    logger.LogWarning("Skipping page '{PageTitle}': {Error}", page.Title, ex.Message);
                    populatedPages.Add(page);
                }
            }

            populatedSections.Add(new OneNoteUtils.Core.Models.Section(section.Name, populatedPages));
        }

        var populatedNotebook = new OneNoteUtils.Core.Models.Notebook(notebook.Name, populatedSections);

        // 3. Write to disk
        logger.LogInformation("Writing Obsidian Markdown...");
        writer.Write(populatedNotebook, outputPath);

        logger.LogInformation("Done. Exported {Total} pages ({Failed} failed) to: {Output}",
            totalPages, failedPages, outputPath);

        if (source is IDisposable disposable)
            disposable.Dispose();

        return 0;
    }
    catch (Exception ex)
    {
        logger.LogCritical(ex, "Export failed: {Error}", ex.Message);
        return 1;
    }
}

// --- Argument parsing ---
static (string? notebook, string? output, string? config, bool verbose) ParseArgs(string[] args)
{
    string? notebook = null, output = null, config = null;
    var verbose = false;

    for (int i = 0; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--notebook" or "-n":
                if (i + 1 < args.Length) notebook = args[++i];
                break;
            case "--output" or "-o":
                if (i + 1 < args.Length) output = args[++i];
                break;
            case "--config" or "-c":
                if (i + 1 < args.Length) config = args[++i];
                break;
            case "--verbose" or "-v":
                verbose = true;
                break;
            case "--help" or "-h":
                PrintUsage();
                Environment.Exit(0);
                break;
        }
    }

    return (notebook, output, config, verbose);
}

static void PrintUsage()
{
    Console.WriteLine("""
        OneNoteUtils - Export OneNote notebooks to Obsidian Markdown

        Usage:
          OneNoteUtils.Cli --notebook <name> --output <path> [options]

        Required:
          -n, --notebook <name>    Notebook name or folder path
          -o, --output <path>      Output directory

        Options:
          -c, --config <path>      Path to a JSON config file (default: appsettings.json)
          -v, --verbose            Enable debug logging
          -h, --help               Show this help message

        Configuration:
          Default settings can be overridden in appsettings.json under "ExportOptions".
          See docs/agents/domain.md for the domain model.
        """);
}

/// <summary>
/// Anchor type for logger category.
/// </summary>
public partial class Program { }
