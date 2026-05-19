namespace OneNoteUtils.Core;

/// <summary>
/// Reads raw data from OneNote. Implementations wrap specific access methods
/// (COM Interop, file parsing, REST API, etc.).
/// </summary>
public interface IOneNoteSource
{
    /// <summary>
    /// Returns the full hierarchy XML for all open notebooks.
    /// </summary>
    string GetHierarchyXml();

    /// <summary>
    /// Returns the page content XML for a specific page, including binary data.
    /// </summary>
    string GetPageContentXml(string pageId);
}
