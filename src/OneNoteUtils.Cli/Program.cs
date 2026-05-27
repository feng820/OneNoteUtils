using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OneNoteUtils.Core;
using OneNoteUtils.Core.Parsing;
using OneNoteUtils.Core.Sync;
using OneNoteUtils.OneNote;
using OneNoteUtils.Writers.Obsidian;

// --- Parse CLI arguments ---
var (notebookName, outputPath, configPath, verbose, sections, fullExport, pushPath) = ParseArgs(args);

// Push mode has different required args
if (!string.IsNullOrEmpty(pushPath))
{
    if (string.IsNullOrEmpty(notebookName) || sections.Count == 0)
    {
        Console.Error.WriteLine("Push requires --notebook and --section. Example:");
        Console.Error.WriteLine("  OneNoteUtils.Cli --push \"Note.md\" -n \"Team Notebook\" -s \"Shared Notes\"");
        return 1;
    }
}
else if (string.IsNullOrEmpty(notebookName) || string.IsNullOrEmpty(outputPath))
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
if (sections.Count > 0)
    options.SectionFilter = sections;

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

// --- Run ---
if (!string.IsNullOrEmpty(pushPath))
    return RunPush(provider, pushPath, notebookName!, sections[0], outputPath ?? ".");
else if (fullExport)
    return RunFullExport(provider, notebookName!, outputPath!, options);
else
    return RunSync(provider, notebookName!, outputPath!, options);

// --- Sync logic (default) ---
static int RunSync(ServiceProvider provider, string notebookName, string outputPath, ExportOptions options)
{
    var logger = provider.GetRequiredService<ILogger<Program>>();

    try
    {
        var source = provider.GetRequiredService<IOneNoteSource>();
        var writer = provider.GetRequiredService<INotebookWriter>();

        // 1. Load manifest
        var manifest = SyncManifest.Load(outputPath);
        var isFirstRun = manifest.Pages.Count == 0;

        if (isFirstRun)
            logger.LogInformation("No sync manifest found — performing initial full export.");
        else
            logger.LogInformation("Sync manifest loaded: {PageCount} pages from last sync.", manifest.Pages.Count);

        // 2. Get hierarchy and parse notebook structure (page stubs only — cheap)
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

        // 3. Diff manifest against current hierarchy
        var plan = SyncDiffer.Diff(manifest, notebook);

        logger.LogInformation("Sync plan: {New} new, {Modified} modified, {Deleted} deleted, {Unchanged} unchanged",
            plan.NewPages.Count, plan.ModifiedPages.Count, plan.DeletedPages.Count, plan.UnchangedPages.Count);

        if (plan.TotalWork == 0)
        {
            logger.LogInformation("Nothing to sync — everything is up to date.");
            if (source is IDisposable d1) d1.Dispose();
            return 0;
        }

        // 4. Delete stale files (deleted + modified/renamed pages' old files)
        foreach (var deleted in plan.DeletedPages)
        {
            if (deleted.PreviousEntry != null)
            {
                DeletePageFiles(deleted.PreviousEntry, logger);
                manifest.Pages.Remove(deleted.PageId);
            }
        }

        foreach (var modified in plan.ModifiedPages)
        {
            if (modified.PreviousEntry != null)
                DeletePageFiles(modified.PreviousEntry, logger);
        }

        // 5. Fetch content and export new + modified pages
        var pagesToExport = plan.NewPages.Concat(plan.ModifiedPages).ToList();
        var failedPages = 0;
        var current = 0;

        // Build a populated notebook for the writer's hierarchy context
        var populatedSections = new List<OneNoteUtils.Core.Models.Section>();
        foreach (var section in notebook.Sections)
        {
            var populatedPages = new List<OneNoteUtils.Core.Models.Page>();
            foreach (var page in section.Pages)
            {
                var needsExport = pagesToExport.Any(p => p.PageId == page.PageId);
                if (needsExport)
                {
                    current++;
                    logger.LogInformation("[{Current}/{Total}] Syncing: {PageTitle}",
                        current, pagesToExport.Count, page.Title);
                    try
                    {
                        var pageXml = source.GetPageContentXml(page.PageId);
                        var populated = PageContentParser.ParsePageContent(page, pageXml);
                        populatedPages.Add(populated);
                    }
                    catch (Exception ex)
                    {
                        failedPages++;
                        logger.LogWarning("Skipping page '{PageTitle}': {Error}", page.Title, ex.Message);
                        populatedPages.Add(page);
                    }
                }
                else
                {
                    populatedPages.Add(page);
                }
            }
            populatedSections.Add(new OneNoteUtils.Core.Models.Section(section.Name, populatedPages));
        }

        var populatedNotebook = new OneNoteUtils.Core.Models.Notebook(notebook.Name, populatedSections);

        // 6. Write pages and update manifest
        foreach (var action in pagesToExport)
        {
            var section = populatedNotebook.Sections.FirstOrDefault(s => s.Name == action.Section);
            var page = section?.Pages.FirstOrDefault(p => p.PageId == action.PageId);
            if (page == null) continue;

            try
            {
                var result = writer.WritePage(page, action.Section, populatedNotebook, outputPath);
                manifest.Pages[action.PageId] = new SyncPageEntry
                {
                    Title = action.Title,
                    Section = action.Section,
                    LastModified = action.LastModified,
                    ExportedPath = result.ExportedPath,
                    ExportedFiles = result.ExportedFiles
                };
            }
            catch (Exception ex)
            {
                logger.LogWarning("Failed to write page '{PageTitle}': {Error}", action.Title, ex.Message);
            }
        }

        // 7. Save manifest
        manifest.NotebookName = notebook.Name;
        manifest.LastSyncTime = DateTime.UtcNow;
        manifest.Save(outputPath);

        logger.LogInformation("Sync complete. {Exported} pages synced ({Failed} failed), {Deleted} deleted.",
            pagesToExport.Count - failedPages, failedPages, plan.DeletedPages.Count);

        if (source is IDisposable d2) d2.Dispose();
        return 0;
    }
    catch (Exception ex)
    {
        logger.LogCritical(ex, "Sync failed: {Error}", ex.Message);
        return 1;
    }
}

// --- Full export logic ---
static int RunFullExport(ServiceProvider provider, string notebookName, string outputPath, ExportOptions options)
{
    var logger = provider.GetRequiredService<ILogger<Program>>();

    try
    {
        logger.LogInformation("Starting full export of notebook '{Notebook}' to '{Output}'",
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
        var totalPageCount = notebook.Sections.Sum(s => s.Pages.Count);
        var populatedSections = new List<OneNoteUtils.Core.Models.Section>();
        var manifest = new SyncManifest { NotebookName = notebook.Name };

        foreach (var section in notebook.Sections)
        {
            var populatedPages = new List<OneNoteUtils.Core.Models.Page>();

            foreach (var page in section.Pages)
            {
                totalPages++;
                logger.LogInformation("[{Current}/{Total}] Fetching: {PageTitle}",
                    totalPages, totalPageCount, page.Title);
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

        // 4. Build and save manifest for future syncs
        foreach (var section in populatedNotebook.Sections)
        {
            foreach (var page in section.Pages)
            {
                manifest.Pages[page.PageId] = new SyncPageEntry
                {
                    Title = page.Title,
                    Section = section.Name,
                    LastModified = page.LastModified
                };
            }
        }
        manifest.LastSyncTime = DateTime.UtcNow;
        manifest.Save(outputPath);

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

static void DeletePageFiles(SyncPageEntry entry, ILogger logger)
{
    if (!string.IsNullOrEmpty(entry.ExportedPath) && File.Exists(entry.ExportedPath))
    {
        File.Delete(entry.ExportedPath);
        logger.LogInformation("Deleted: {Path}", entry.ExportedPath);
    }

    foreach (var file in entry.ExportedFiles)
    {
        if (File.Exists(file))
        {
            File.Delete(file);
            logger.LogDebug("Deleted: {Path}", file);
        }
    }
}

// --- Push logic ---
static int RunPush(ServiceProvider provider, string pushPath, string notebookName, string sectionName, string manifestDir)
{
    var logger = provider.GetRequiredService<ILogger<Program>>();

    try
    {
        var source = provider.GetRequiredService<IOneNoteSource>();

        // Resolve markdown files to push
        var mdFiles = new List<string>();
        if (File.Exists(pushPath) && pushPath.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            mdFiles.Add(Path.GetFullPath(pushPath));
        }
        else if (Directory.Exists(pushPath))
        {
            mdFiles.AddRange(Directory.GetFiles(pushPath, "*.md", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFullPath));
        }
        else
        {
            logger.LogError("Push path '{Path}' is not a .md file or directory.", pushPath);
            return 1;
        }

        if (mdFiles.Count == 0)
        {
            logger.LogWarning("No .md files found at '{Path}'.", pushPath);
            return 0;
        }

        logger.LogInformation("Pushing {Count} file(s) to '{Notebook}' / '{Section}'",
            mdFiles.Count, notebookName, sectionName);

        // Find the target section ID
        var sectionId = source.FindSectionId(notebookName, sectionName);
        if (sectionId == null)
        {
            logger.LogError("Section '{Section}' not found in notebook '{Notebook}'.", sectionName, notebookName);
            return 1;
        }

        // Load manifest for push tracking
        var manifest = SyncManifest.Load(manifestDir);
        var pushed = 0;

        foreach (var mdFile in mdFiles)
        {
            var fileName = Path.GetFileNameWithoutExtension(mdFile);
            logger.LogInformation("[{Current}/{Total}] Pushing: {File}",
                ++pushed, mdFiles.Count, fileName);

            try
            {
                var markdown = File.ReadAllText(mdFile);
                var elements = MarkdownReader.Parse(markdown);

                // Check if we've pushed this file before
                string pageId;
                if (manifest.Pushed.TryGetValue(mdFile, out var existing))
                {
                    // Update existing page — clear existing outlines first
                    pageId = existing.PageId;
                    logger.LogDebug("Updating existing page: {PageId}", pageId);
                    ClearPageOutlines(source, pageId, logger);
                }
                else
                {
                    // Create new page
                    pageId = source.CreatePage(sectionId);
                    logger.LogDebug("Created new page: {PageId}", pageId);
                }

                // Build and push page XML
                var pageXml = OneNoteXmlWriter.BuildPageXml(pageId, fileName, elements);
                source.UpdatePageContent(pageXml);

                // Update manifest
                manifest.Pushed[mdFile] = new PushEntry
                {
                    PageId = pageId,
                    NotebookName = notebookName,
                    SectionName = sectionName,
                    LastPushed = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                logger.LogWarning("Failed to push '{File}': {Error}", fileName, ex.Message);
            }
        }

        manifest.Save(manifestDir);

        logger.LogInformation("Push complete. {Count} file(s) pushed.", pushed);

        if (source is IDisposable disposable) disposable.Dispose();
        return 0;
    }
    catch (Exception ex)
    {
        logger.LogCritical(ex, "Push failed: {Error}", ex.Message);
        return 1;
    }
}

static void ClearPageOutlines(IOneNoteSource source, string pageId, ILogger logger)
{
    try
    {
        var pageXml = source.GetPageContentXml(pageId);
        var doc = new System.Xml.XmlDocument();
        doc.LoadXml(pageXml);

        var outlines = doc.SelectNodes("//*[local-name()='Outline']");
        if (outlines == null) return;

        foreach (System.Xml.XmlElement outline in outlines)
        {
            var objectId = outline.GetAttribute("objectID");
            if (!string.IsNullOrEmpty(objectId))
            {
                source.DeletePageContent(pageId, objectId);
                logger.LogDebug("Cleared outline: {ObjectId}", objectId);
            }
        }
    }
    catch (Exception ex)
    {
        logger.LogWarning("Failed to clear existing outlines: {Error}", ex.Message);
    }
}

// --- Argument parsing ---
static (string? notebook, string? output, string? config, bool verbose, List<string> sections, bool fullExport, string? pushPath) ParseArgs(string[] args)
{
    string? notebook = null, output = null, config = null, pushPath = null;
    var verbose = false;
    var fullExport = false;
    var sections = new List<string>();

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
            case "--section" or "-s":
                if (i + 1 < args.Length) sections.Add(args[++i]);
                break;
            case "--push":
                if (i + 1 < args.Length) pushPath = args[++i];
                break;
            case "--full":
                fullExport = true;
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

    return (notebook, output, config, verbose, sections, fullExport, pushPath);
}

static void PrintUsage()
{
    Console.WriteLine("""
        OneNoteUtils - Sync OneNote notebooks with Obsidian Markdown

        Usage (sync/export):
          OneNoteUtils.Cli -n <notebook> -o <path> [options]

        Usage (push to OneNote):
          OneNoteUtils.Cli --push <file-or-folder> -n <notebook> -s <section>

        Required (sync/export):
          -n, --notebook <name>    Notebook name or folder path
          -o, --output <path>      Output directory

        Required (push):
          --push <path>            .md file or folder to push to OneNote
          -n, --notebook <name>    Target notebook name
          -s, --section <name>     Target section name

        Options:
          -s, --section <name>     Filter sections for sync (repeatable)
              --full               Force full export (skip incremental sync)
          -c, --config <path>      Path to a JSON config file (default: appsettings.json)
          -v, --verbose            Enable debug logging
          -h, --help               Show this help message

        Examples:
          OneNoteUtils.Cli -n "My Notebook" -o C:\Export
          OneNoteUtils.Cli -n "My Notebook" -o C:\Export -s "Daily Notes"
          OneNoteUtils.Cli -n "My Notebook" -o C:\Export --full
          OneNoteUtils.Cli --push "C:\Vault\Note.md" -n "Team Notebook" -s "Shared"
          OneNoteUtils.Cli --push "C:\Vault\Notes\" -n "Team Notebook" -s "Shared"
        """);
}

/// <summary>
/// Anchor type for logger category.
/// </summary>
public partial class Program { }
