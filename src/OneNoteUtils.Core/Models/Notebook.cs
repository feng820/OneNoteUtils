namespace OneNoteUtils.Core.Models;

/// <summary>
/// A OneNote notebook — the top-level container.
/// </summary>
public record Notebook(
    string Name,
    IReadOnlyList<Section> Sections,
    IReadOnlyList<SectionGroup>? SectionGroups = null)
{
    /// <summary>
    /// Returns all sections including those inside section groups, recursively.
    /// </summary>
    public IEnumerable<(string Path, Section Section)> GetAllSections(string prefix = "")
    {
        foreach (var section in Sections)
            yield return (prefix, section);

        if (SectionGroups != null)
        {
            foreach (var group in SectionGroups)
            {
                var groupPath = string.IsNullOrEmpty(prefix) ? group.Name : $"{prefix}/{group.Name}";
                foreach (var section in group.Sections)
                    yield return (groupPath, section);

                foreach (var item in GetAllSectionsFromGroup(group, groupPath))
                    yield return item;
            }
        }
    }

    private static IEnumerable<(string Path, Section Section)> GetAllSectionsFromGroup(SectionGroup group, string prefix)
    {
        foreach (var nested in group.SectionGroups)
        {
            var nestedPath = $"{prefix}/{nested.Name}";
            foreach (var section in nested.Sections)
                yield return (nestedPath, section);

            foreach (var item in GetAllSectionsFromGroup(nested, nestedPath))
                yield return item;
        }
    }
}

/// <summary>
/// A named folder of sections and/or nested section groups inside a Notebook.
/// </summary>
public record SectionGroup(
    string Name,
    IReadOnlyList<Section> Sections,
    IReadOnlyList<SectionGroup> SectionGroups);

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
    IReadOnlyList<ContentElement> Elements,
    bool HasInk = false);
