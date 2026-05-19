using System.Xml;
using OneNoteUtils.Core.Models;

namespace OneNoteUtils.Core.Parsing;

/// <summary>
/// Parses OneNote hierarchy XML into the domain model (Notebook → Section → Page stubs).
/// Pages are returned without content — use <see cref="PageContentParser"/> to populate elements.
/// </summary>
public static class HierarchyParser
{
    private const string OneNoteNs = "http://schemas.microsoft.com/office/onenote/2013/onenote";

    /// <summary>
    /// Finds a notebook by name or path in the hierarchy XML.
    /// Returns null if not found.
    /// </summary>
    public static Notebook? ParseNotebook(string hierarchyXml, string notebookIdentifier, ExportOptions options)
    {
        var doc = new XmlDocument();
        doc.LoadXml(hierarchyXml);
        var nsMgr = CreateNsMgr(doc);

        var notebookNode = ResolveNotebook(doc, notebookIdentifier);
        if (notebookNode == null) return null;

        var name = notebookNode.GetAttribute("name") ?? notebookIdentifier;
        var notebookId = notebookNode.GetAttribute("ID") ?? "";

        var sections = ParseSections(doc, notebookId, nsMgr, options);
        return new Notebook(name, sections);
    }

    private static XmlElement? ResolveNotebook(XmlDocument doc, string identifier)
    {
        var id = identifier?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(id)) return null;

        var candidatePath = id;
        if (id.EndsWith(".one", StringComparison.OrdinalIgnoreCase))
            candidatePath = Path.GetDirectoryName(id) ?? id;

        var notebooks = doc.SelectNodes("//*[local-name()='Notebook']");
        if (notebooks == null) return null;

        // Match by name first
        foreach (XmlElement nb in notebooks)
        {
            var name = nb.GetAttribute("name");
            if (!string.IsNullOrEmpty(name) &&
                name.Equals(id, StringComparison.OrdinalIgnoreCase))
                return nb;
        }

        // Match by path
        foreach (XmlElement nb in notebooks)
        {
            var path = nb.GetAttribute("path");
            if (!string.IsNullOrEmpty(path) &&
                path.TrimEnd('\\').Equals(candidatePath.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                return nb;
        }

        return null;
    }

    private static IReadOnlyList<Section> ParseSections(
        XmlDocument doc, string notebookId, XmlNamespaceManager nsMgr, ExportOptions options)
    {
        var sectionNodes = doc.SelectNodes(
            $"//*[local-name()='Notebook' and @ID='{notebookId}']//*[local-name()='Section']");

        if (sectionNodes == null) return [];

        var sections = new List<Section>();
        var hasSectionFilter = options.SectionFilter.Count > 0;

        foreach (XmlElement sectionNode in sectionNodes)
        {
            var sectionName = sectionNode.GetAttribute("name") ?? "Untitled";

            if (hasSectionFilter && !options.SectionFilter.Contains(sectionName))
                continue;

            var pages = ParsePages(sectionNode, options);
            sections.Add(new Section(sectionName, pages));
        }

        return sections;
    }

    private static IReadOnlyList<Page> ParsePages(XmlElement sectionNode, ExportOptions options)
    {
        var pageNodes = sectionNode.SelectNodes(".//*[local-name()='Page']");
        if (pageNodes == null) return [];

        var pages = new List<Page>();

        foreach (XmlElement pageNode in pageNodes)
        {
            var pageId = pageNode.GetAttribute("ID");
            if (string.IsNullOrEmpty(pageId)) continue;

            if (options.DateThreshold.HasValue)
            {
                var lastModStr = pageNode.GetAttribute("lastModifiedTime");
                if (DateTime.TryParse(lastModStr, out var lastMod) && lastMod < options.DateThreshold.Value)
                    continue;
            }

            var title = pageNode.GetAttribute("name");
            if (string.IsNullOrWhiteSpace(title)) title = "Untitled";

            var levelStr = pageNode.GetAttribute("pageLevel");
            var level = int.TryParse(levelStr, out var l) ? Math.Max(1, l) : 1;

            var lastModifiedStr = pageNode.GetAttribute("lastModifiedTime");
            DateTime? lastModified = DateTime.TryParse(lastModifiedStr, out var dt) ? dt : null;

            // Elements are populated later by PageContentParser
            pages.Add(new Page(pageId, title, level, lastModified, []));
        }

        return pages;
    }

    private static XmlNamespaceManager CreateNsMgr(XmlDocument doc)
    {
        var nsMgr = new XmlNamespaceManager(doc.NameTable);
        nsMgr.AddNamespace("one", OneNoteNs);
        return nsMgr;
    }
}
