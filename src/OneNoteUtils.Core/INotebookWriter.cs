using OneNoteUtils.Core.Models;

namespace OneNoteUtils.Core;

/// <summary>
/// Serializes a Notebook domain model to a specific output format on disk.
/// </summary>
public interface INotebookWriter
{
    /// <summary>
    /// Writes the notebook content to the specified output directory.
    /// </summary>
    void Write(Notebook notebook, string outputPath);
}
