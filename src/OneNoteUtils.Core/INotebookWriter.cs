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
    /// Writes the entire notebook content to the specified output directory.
    /// </summary>
    void Write(Notebook notebook, string outputPath);

    /// <summary>
    /// Writes a single page to disk, returning the paths of all files created.
    /// Used by incremental sync to export individual pages.
    /// </summary>
    WritePageResult WritePage(Page page, string sectionName, Notebook notebook, string outputPath);
}
