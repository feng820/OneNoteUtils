namespace OneNoteUtils.Core.Models;

/// <summary>
/// A OneNote notebook — the top-level container.
/// </summary>
public record Notebook(
    string Name,
    IReadOnlyList<Section> Sections);

/// <summary>
/// A named grouping of pages inside a Notebook.
/// </summary>
public record Section(
    string Name,
    IReadOnlyList<Page> Pages);

/// <summary>
/// A single OneNote page within a Section.
/// </summary>
public record Page(
    string PageId,
    string Title,
    int Level,
    DateTime? LastModified,
    IReadOnlyList<ContentElement> Elements);
