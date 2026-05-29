using System.Text;
using Microsoft.Extensions.Logging;
using OneNoteUtils.Core;
using OneNoteUtils.Core.Models;
using OneNoteUtils.Core.Parsing;

namespace OneNoteUtils.Writers.Obsidian;

/// <summary>
/// Writes a Notebook to Obsidian-compatible Markdown files on disk.
/// </summary>
public class ObsidianMarkdownWriter : INotebookWriter
{
    private readonly ExportOptions _options;
    private readonly ILogger<ObsidianMarkdownWriter> _logger;

    public ObsidianMarkdownWriter(ExportOptions options, ILogger<ObsidianMarkdownWriter> logger)
    {
        _options = options;
        _logger = logger;
    }

    public void Write(Notebook notebook, string outputPath)
    {
        var notebookFolder = Path.Combine(outputPath,
            FileNameUtils.SanitizeFileBaseName(notebook.Name));
        Directory.CreateDirectory(notebookFolder);

        // Write all sections including those inside section groups
        foreach (var (path, section) in notebook.GetAllSections())
        {
            var baseFolder = string.IsNullOrEmpty(path)
                ? notebookFolder
                : Path.Combine(notebookFolder, path.Replace('/', Path.DirectorySeparatorChar));
            WriteSection(section, baseFolder);
        }
    }

    private void WriteSection(Section section, string notebookFolder)
    {
        var sectionFolder = Path.Combine(notebookFolder,
            FileNameUtils.SanitizeFileBaseName(section.Name));
        Directory.CreateDirectory(sectionFolder);

        _logger.LogInformation("Processing section: {SectionName}", section.Name);

        if (section.Pages.Count == 0)
        {
            _logger.LogInformation("  No pages found.");
            return;
        }

        // Build page hierarchy
        var pageInfos = BuildPageHierarchy(section.Pages);
        var rootIds = pageInfos.Values
            .Where(p => p.ParentId == null)
            .Select(p => p.Page.PageId)
            .ToList();

        foreach (var rootId in rootIds)
        {
            WritePageTree(rootId, pageInfos, sectionFolder, section.Name);
        }
    }

    private void WritePageTree(
        string pageId,
        Dictionary<string, PageInfo> pageInfos,
        string sectionFolder,
        string sectionName)
    {
        if (!pageInfos.TryGetValue(pageId, out var info)) return;

        var folder = GetPageFolder(pageId, pageInfos, sectionFolder);
        Directory.CreateDirectory(folder);

        try
        {
            var result = WritePageFile(info, folder, sectionName, pageInfos);
            // Result is used by WritePage; for full export via Write() we don't need it
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Skipping page '{PageTitle}': {Error}", info.Page.Title, ex.Message);
        }

        foreach (var childId in info.ChildIds)
        {
            WritePageTree(childId, pageInfos, sectionFolder, sectionName);
        }
    }

    private List<string> _currentPageFiles = [];

    private WritePageResult WritePageFile(
        PageInfo info,
        string folder,
        string sectionName,
        Dictionary<string, PageInfo> pageInfos)
    {
        _currentPageFiles = [];
        var sb = new StringBuilder();
        var imageIndex = 1;

        // Frontmatter
        if (_options.IncludeFrontmatter)
        {
            sb.AppendLine("---");
            sb.AppendLine($"title: \"{info.Page.Title.Replace("\"", "\\\"")}\"");
            sb.AppendLine("source: OneNote COM");
            sb.AppendLine($"onenote_page_id: \"{info.Page.PageId}\"");
            sb.AppendLine($"section: \"{sectionName.Replace("\"", "\\\"")}\"");
            sb.AppendLine("---");
            sb.AppendLine();
        }

        // Page title as h1
        sb.AppendLine($"# {info.Page.Title}");
        sb.AppendLine();

        // Content elements
        foreach (var element in info.Page.Elements)
        {
            WriteContentElement(sb, element, 0, info.SafeBase, folder, ref imageIndex);
        }

        // Subpage links
        if (info.ChildIds.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Subpages");
            sb.AppendLine();
            foreach (var childId in info.ChildIds)
            {
                if (pageInfos.TryGetValue(childId, out var child))
                {
                    var link = FormatLink(child.SafeBase, child.Page.Title);
                    sb.AppendLine($"- {link}");
                }
            }
        }

        var mdPath = Path.Combine(folder, info.SafeBase + ".md");
        File.WriteAllText(mdPath, sb.ToString(), Encoding.UTF8);
        _logger.LogInformation("Exported: {Path}", mdPath);

        return new WritePageResult(mdPath, _currentPageFiles);
    }

    /// <inheritdoc />
    public WritePageResult WritePage(Page page, string sectionName, Notebook notebook, string outputPath)
    {
        var notebookFolder = Path.Combine(outputPath,
            FileNameUtils.SanitizeFileBaseName(notebook.Name));
        var sectionFolder = Path.Combine(notebookFolder,
            FileNameUtils.SanitizeFileBaseName(sectionName));
        Directory.CreateDirectory(sectionFolder);

        // Find the section to build hierarchy context
        var section = notebook.Sections.FirstOrDefault(s => s.Name == sectionName);
        var pages = section?.Pages ?? [page];
        var pageInfos = BuildPageHierarchy(pages);

        if (!pageInfos.TryGetValue(page.PageId, out var info))
        {
            // Page not in hierarchy — create a simple entry
            var safeBase = FileNameUtils.SanitizeFileBaseName(page.Title);
            info = new PageInfo(page, safeBase, null, []);
        }

        var folder = GetPageFolder(page.PageId, pageInfos, sectionFolder);
        Directory.CreateDirectory(folder);

        return WritePageFile(info, folder, sectionName, pageInfos);
    }

    private void WriteContentElement(
        StringBuilder sb,
        ContentElement element,
        int depth,
        string pageFileBaseName,
        string pageOutFolder,
        ref int imageIndex)
    {
        var indent = new string(' ', depth * 4);

        switch (element)
        {
            case Heading heading:
                sb.AppendLine($"{new string('#', heading.Level)} {heading.Text}");
                sb.AppendLine();
                break;

            case Paragraph paragraph:
                sb.Append(indent);
                foreach (var run in paragraph.Runs)
                {
                    sb.Append(FormatRun(run));
                }
                sb.AppendLine();
                if (depth == 0) sb.AppendLine();
                break;

            case BulletList bulletList:
                foreach (var item in bulletList.Items)
                {
                    WriteListItem(sb, item, depth, "- ", pageFileBaseName, pageOutFolder, ref imageIndex);
                }
                if (depth == 0) sb.AppendLine();
                break;

            case NumberedList numberedList:
                foreach (var item in numberedList.Items)
                {
                    WriteListItem(sb, item, depth, "1. ", pageFileBaseName, pageOutFolder, ref imageIndex);
                }
                if (depth == 0) sb.AppendLine();
                break;

            case Table table:
                WriteTable(sb, table, pageFileBaseName, pageOutFolder, ref imageIndex);
                break;

            case Image image:
                var imgFile = WriteImageFile(image, pageFileBaseName, pageOutFolder, ref imageIndex);
                if (imgFile != null)
                {
                    sb.AppendLine(_options.EmbedImages
                        ? $"![[{imgFile}]]"
                        : $"![{imgFile}]({imgFile})");
                    sb.AppendLine();
                }
                break;

            case Attachment attachment:
                var attFile = WriteAttachmentFile(attachment, pageOutFolder);
                if (attFile != null)
                {
                    var isPdf = attFile.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);
                    if (_options.UseObsidianWikilinks)
                    {
                        sb.AppendLine(isPdf && _options.EmbedPdfs
                            ? $"![[{attFile}]]"
                            : $"[[{attFile}]]");
                    }
                    else
                    {
                        sb.AppendLine($"[{attFile}]({attFile})");
                    }
                    sb.AppendLine();
                }
                break;

            case CodeBlock codeBlock:
                var lang = codeBlock.Language != null ? codeBlock.Language : "";
                sb.AppendLine($"```{lang}");
                sb.AppendLine(codeBlock.Code);
                sb.AppendLine("```");
                sb.AppendLine();
                break;

            case HorizontalRule:
                sb.AppendLine("---");
                sb.AppendLine();
                break;

            case Blockquote blockquote:
                foreach (var bqElement in blockquote.Elements)
                {
                    if (bqElement is Paragraph bqPara)
                    {
                        sb.Append("> ");
                        foreach (var run in bqPara.Runs)
                            sb.Append(FormatRun(run));
                        sb.AppendLine();
                    }
                }
                sb.AppendLine();
                break;

            case Checkbox checkbox:
                var checkMark = checkbox.Checked ? "x" : " ";
                sb.Append($"- [{checkMark}] ");
                foreach (var run in checkbox.Runs)
                    sb.Append(FormatRun(run));
                sb.AppendLine();
                break;
        }
    }

    private void WriteListItem(
        StringBuilder sb,
        ListItem item,
        int depth,
        string marker,
        string pageFileBaseName,
        string pageOutFolder,
        ref int imageIndex)
    {
        var indent = new string(' ', depth * 4);
        sb.Append($"{indent}{marker}");

        // Inline content on the same line (text, images)
        var hasBlockContent = false;
        foreach (var element in item.Elements)
        {
            if (element is Paragraph paragraph)
            {
                foreach (var run in paragraph.Runs)
                {
                    sb.Append(FormatRun(run));
                }
            }
            else if (element is Image image)
            {
                var imgFile = WriteImageFile(image, pageFileBaseName, pageOutFolder, ref imageIndex);
                if (imgFile != null)
                    sb.Append(_options.EmbedImages ? $"![[{imgFile}]]" : $"![{imgFile}]({imgFile})");
            }
            else
            {
                // Block-level content (tables, etc.) — render after the list marker line
                hasBlockContent = true;
            }
        }
        sb.AppendLine();

        // Render block-level elements that couldn't go inline
        if (hasBlockContent)
        {
            foreach (var element in item.Elements)
            {
                if (element is not Paragraph and not Image)
                {
                    WriteContentElement(sb, element, depth + 1, pageFileBaseName, pageOutFolder, ref imageIndex);
                }
            }
        }

        // Nested children
        if (item.Children != null)
        {
            foreach (var child in item.Children)
            {
                WriteContentElement(sb, child, depth + 1, pageFileBaseName, pageOutFolder, ref imageIndex);
            }
        }
    }

    private void WriteTable(
        StringBuilder sb,
        Table table,
        string pageFileBaseName,
        string pageOutFolder,
        ref int imageIndex)
    {
        // Single-column tables in OneNote are typically content containers (bordered boxes),
        // not actual tabular data. Render their content as regular block elements.
        if (table.Rows.Count > 0 && table.Rows.All(r => r.Cells.Count == 1))
        {
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
            foreach (var row in table.Rows)
            {
                // Run code block grouping on cell content (Kusto queries inside bordered boxes)
                var cellElements = PageContentParser.GroupCodeBlocks(row.Cells[0].Elements);
                foreach (var element in cellElements)
                {
                    WriteContentElement(sb, element, 0, pageFileBaseName, pageOutFolder, ref imageIndex);
                }
            }
            sb.AppendLine("---");
            sb.AppendLine();
            return;
        }

        sb.AppendLine();

        for (int rowIdx = 0; rowIdx < table.Rows.Count; rowIdx++)
        {
            var row = table.Rows[rowIdx];
            var cellTexts = new List<string>();
            foreach (var cell in row.Cells)
            {
                cellTexts.Add(FormatCellContent(cell, pageFileBaseName, pageOutFolder, ref imageIndex));
            }

            sb.AppendLine("| " + string.Join(" | ", cellTexts) + " |");

            if (rowIdx == 0)
            {
                sb.AppendLine("| " + string.Join(" | ", cellTexts.Select(_ => "---")) + " |");
            }
        }

        sb.AppendLine();
    }

    private string FormatCellContent(
        TableCell cell,
        string pageFileBaseName,
        string pageOutFolder,
        ref int imageIndex)
    {
        var parts = new List<string>();

        foreach (var element in cell.Elements)
        {
            if (element is Paragraph paragraph)
            {
                var text = string.Concat(paragraph.Runs.Select(FormatRun));
                if (!string.IsNullOrWhiteSpace(text))
                    parts.Add(text.Trim());
            }
            else if (element is Image image)
            {
                var imgFile = WriteImageFile(image, pageFileBaseName, pageOutFolder, ref imageIndex);
                if (imgFile != null)
                    parts.Add(_options.EmbedImages ? $"![[{imgFile}]]" : $"![{imgFile}]({imgFile})");
            }
        }

        return string.Join("<br>", parts).Replace("|", "\\|");
    }

    private static string FormatRun(Run run)
    {
        var text = run.Text;
        if (string.IsNullOrEmpty(text)) return "";

        if (run.Code) return $"`{text}`";
        if (run.Highlight && text.Trim().Length > 0) text = $"=={text}==";
        if (run.Strikethrough && text.Trim().Length > 0) text = $"~~{text}~~";
        if (run.Bold && text.Trim().Length > 0) text = $"**{text}**";
        if (run.Italic && text.Trim().Length > 0) text = $"*{text}*";
        if (run.Underline && text.Trim().Length > 0) text = $"<u>{text}</u>";

        if (run.HrefUrl != null)
        {
            // Convert onenote:// links to Obsidian wikilinks
            if (run.HrefUrl.StartsWith("onenote:", StringComparison.OrdinalIgnoreCase))
            {
                // Use the link text as the page name for the wikilink
                var pageName = run.Text.Trim();
                if (!string.IsNullOrEmpty(pageName))
                    return $"[[{pageName}]]";
            }
            text = $"[{text}]({run.HrefUrl})";
        }

        return text;
    }

    private string FormatLink(string target, string? alias = null)
    {
        if (!_options.UseObsidianWikilinks)
            return $"[{alias ?? target}]({target}.md)";

        if (_options.UseAliasLinks && alias != null && alias != target)
            return $"[[{target}|{alias}]]";

        return $"[[{target}]]";
    }

    private const string AttachmentsFolder = "_attachments";

    private string? WriteImageFile(
        Image image,
        string pageFileBaseName,
        string pageOutFolder,
        ref int imageIndex)
    {
        var bytes = image.LoadBytes();
        if (bytes == null) return null;

        var attachDir = Path.Combine(pageOutFolder, AttachmentsFolder);
        Directory.CreateDirectory(attachDir);

        var fileName = $"{pageFileBaseName}-image{imageIndex:00}.{image.Format}"
            .Replace(' ', '-');
        var filePath = Path.Combine(attachDir, fileName);
        File.WriteAllBytes(filePath, bytes);
        imageIndex++;
        _currentPageFiles.Add(filePath);

        return $"{AttachmentsFolder}/{fileName}";
    }

    private string? WriteAttachmentFile(Attachment attachment, string pageOutFolder)
    {
        var bytes = attachment.LoadBytes();
        if (bytes == null) return null;

        var attachDir = Path.Combine(pageOutFolder, AttachmentsFolder);
        Directory.CreateDirectory(attachDir);

        var safeName = FileNameUtils.SanitizeFileBaseName(
            Path.GetFileNameWithoutExtension(attachment.PreferredName));
        var ext = Path.GetExtension(attachment.PreferredName);
        var fileName = safeName + ext;

        var filePath = Path.Combine(attachDir, fileName);
        File.WriteAllBytes(filePath, bytes);
        _currentPageFiles.Add(filePath);

        return $"{AttachmentsFolder}/{fileName}";
    }

    // --- Page hierarchy helpers ---

    private Dictionary<string, PageInfo> BuildPageHierarchy(IReadOnlyList<Page> pages)
    {
        var infos = new Dictionary<string, PageInfo>();
        var stack = new Dictionary<int, string>(); // level -> last page id

        foreach (var page in pages)
        {
            string? parentId = null;
            if (page.Level > 1 && stack.TryGetValue(page.Level - 1, out var pid))
                parentId = pid;

            var safeBase = FileNameUtils.SanitizeFileBaseName(page.Title);
            if (_options.AlwaysSuffixWithShortId)
                safeBase = $"{safeBase}-{FileNameUtils.ShortId(page.PageId, _options.ShortIdLength)}";

            // Auto-detect duplicate names and append short ID to disambiguate
            if (!_options.AlwaysSuffixWithShortId && infos.Values.Any(i => i.SafeBase == safeBase))
                safeBase = $"{safeBase}-{FileNameUtils.ShortId(page.PageId, _options.ShortIdLength)}";

            infos[page.PageId] = new PageInfo(page, safeBase, parentId, []);

            if (parentId != null && infos.TryGetValue(parentId, out var parent))
                parent.ChildIds.Add(page.PageId);

            stack[page.Level] = page.PageId;
            foreach (var key in stack.Keys.Where(k => k > page.Level).ToList())
                stack.Remove(key);
        }

        return infos;
    }

    private string GetPageFolder(
        string pageId,
        Dictionary<string, PageInfo> pageInfos,
        string sectionFolder)
    {
        var chain = GetParentChain(pageId, pageInfos);
        var folder = sectionFolder;

        foreach (var info in chain)
        {
            if (info.ChildIds.Count > 0)
                folder = Path.Combine(folder, info.SafeBase);
        }

        return folder;
    }

    private List<PageInfo> GetParentChain(string pageId, Dictionary<string, PageInfo> pageInfos)
    {
        var chain = new List<PageInfo>();
        var current = pageId;

        while (current != null && pageInfos.TryGetValue(current, out var info))
        {
            chain.Insert(0, info);
            current = info.ParentId;
        }

        return chain;
    }

    private record PageInfo(Page Page, string SafeBase, string? ParentId, List<string> ChildIds);
}
