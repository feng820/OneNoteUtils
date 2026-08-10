using System.Xml;

namespace OneNoteUtils.Core.Parsing;

/// <summary>Why a section lookup did (or did not) succeed.</summary>
public enum SectionLookupStatus
{
    Found,

    /// <summary>The notebook is not open in the OneNote client — COM cannot see it at all.</summary>
    NotebookNotFound,

    /// <summary>The notebook is open, but it has no section with that name.</summary>
    SectionNotFound
}

/// <summary>
/// Outcome of resolving a notebook/section pair, carrying enough context to tell the user
/// which of the two was actually wrong and what the valid alternatives were.
/// </summary>
public sealed record SectionLookupResult(
    SectionLookupStatus Status,
    string? SectionId,
    IReadOnlyList<string> Candidates)
{
    public bool IsFound => Status == SectionLookupStatus.Found;

    private const int MaxCandidates = 15;

    /// <summary>
    /// Builds an actionable error message. Returns an empty string when the lookup succeeded.
    /// </summary>
    public string ToErrorMessage(string notebookName, string sectionName) => Status switch
    {
        SectionLookupStatus.Found => "",

        SectionLookupStatus.NotebookNotFound =>
            $"Notebook '{notebookName}' not found. Make sure it is open in OneNote. " +
            "OneNote automation only sees notebooks that are currently open in the desktop client — " +
            "one that exists in SharePoint/Teams but was never opened locally is invisible. " +
            $"Open notebooks: {Format(Candidates)}",

        SectionLookupStatus.SectionNotFound =>
            $"Section '{sectionName}' not found in notebook '{notebookName}' " +
            "(the notebook itself is open, so this is a section problem, not a notebook problem). " +
            $"{DescribeSections(sectionName)}: {Format(Near(sectionName))}",

        _ => $"Section '{sectionName}' not found in notebook '{notebookName}'."
    };

    private string DescribeSections(string sectionName) =>
        Near(sectionName).Count < Candidates.Count ? "Closest sections" : "Sections";

    // Lead with near-matches; an alphabetical head of a 120-section notebook tells the user nothing.
    // Match both ways so a typo'd suffix ("S3600") still surfaces the real section ("S360").
    private IReadOnlyList<string> Near(string sectionName)
    {
        var near = Candidates
            .Where(c => c.Contains(sectionName, StringComparison.OrdinalIgnoreCase) ||
                        sectionName.Contains(c, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return near.Count > 0 ? near : Candidates;
    }

    private static string Format(IReadOnlyList<string> names)
    {
        if (names.Count == 0) return "(none)";

        var shown = string.Join(", ", names.Take(MaxCandidates));
        return names.Count > MaxCandidates
            ? $"{shown}, ... (+{names.Count - MaxCandidates} more)"
            : shown;
    }
}

/// <summary>
/// Resolves a notebook/section pair against OneNote hierarchy XML.
/// Kept free of COM so it can be unit tested; <c>ComOneNoteSource</c> supplies the XML.
/// </summary>
public static class SectionResolver
{
    public static SectionLookupResult Resolve(string hierarchyXml, string notebookName, string sectionName)
    {
        var doc = new XmlDocument();
        doc.LoadXml(hierarchyXml);

        var notebooks = Elements(doc.SelectNodes("//*[local-name()='Notebook']"))
            .Where(IsLive)
            .ToList();

        var notebook = notebooks.FirstOrDefault(
            nb => nb.GetAttribute("name").Equals(notebookName, StringComparison.OrdinalIgnoreCase));

        if (notebook == null)
        {
            var open = notebooks
                .Select(nb => nb.GetAttribute("name"))
                .Where(n => !string.IsNullOrEmpty(n))
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new SectionLookupResult(SectionLookupStatus.NotebookNotFound, null, open);
        }

        var sections = Elements(notebook.SelectNodes(".//*[local-name()='Section']"))
            .Where(IsLive)
            .ToList();

        var match = sections.FirstOrDefault(
            s => s.GetAttribute("name").Equals(sectionName, StringComparison.OrdinalIgnoreCase));

        if (match != null)
            return new SectionLookupResult(SectionLookupStatus.Found, match.GetAttribute("ID"), []);

        var available = sections
            .Select(s => s.GetAttribute("name"))
            .Where(n => !string.IsNullOrEmpty(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new SectionLookupResult(SectionLookupStatus.SectionNotFound, null, available);
    }

    /// <summary>
    /// Recycle-bin nodes are still present in the hierarchy. Matching one would resolve to a deleted
    /// section and silently write the page into the recycle bin.
    /// </summary>
    private static bool IsLive(XmlElement element) =>
        !element.GetAttribute("isInRecycleBin").Equals("true", StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<XmlElement> Elements(XmlNodeList? nodes) =>
        nodes?.OfType<XmlElement>() ?? [];
}
