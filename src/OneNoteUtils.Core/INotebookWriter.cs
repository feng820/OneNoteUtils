using OneNoteUtils.Core.Models;

namespace OneNoteUtils.Core;

/// <summary>
/// Result of writing a single page to disk.
/// </summary>
public record WritePageResult(
    string ExportedPath,
    List<string> ExportedFiles);

/// <summary>
/// Serializes a Notebook domain model to a specific output format on disk.
/// </summary>
public interface INotebookWriter
{
    /// <summary>
    /// Writes the entire notebook content to the specified output directory,
    /// returning the per-page results keyed by OneNote page id so callers can
    /// record exactly which files each page produced.
    /// </summary>
    IReadOnlyDictionary<string, WritePageResult> Write(Notebook notebook, string outputPath);

    /// <summary>
    /// Writes a single page to disk, returning the paths of all files created.
    /// Used by incremental sync to export individual pages.
    /// </summary>
    WritePageResult WritePage(Page page, string sectionName, Notebook notebook, string outputPath);

    /// <summary>
    /// Resolves the on-disk markdown path for a page using the same file layout
    /// rules as <see cref="WritePage"/> (hierarchy nesting and name disambiguation),
    /// without writing anything. Used to locate companion files for an exported page.
    /// </summary>
    string GetPageMarkdownPath(Page page, string sectionName, Notebook notebook, string outputPath);
}
