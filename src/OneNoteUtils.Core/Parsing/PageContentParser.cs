using System.Xml;
using OneNoteUtils.Core.Models;

namespace OneNoteUtils.Core.Parsing;

/// <summary>
/// Parses a single OneNote page's XML content into a list of ContentElements.
/// Handles text, images, tables, attachments, lists, and headings.
/// </summary>
public static class PageContentParser
{
    /// <summary>
    /// Parses page XML into content elements, returning a new Page with populated elements.
    /// </summary>
    public static Page ParsePageContent(Page pageStub, string pageXml)
    {
        var doc = new XmlDocument();
        doc.LoadXml(pageXml);

        var binaryData = ExtractBinaryData(doc);
        var quickStyles = BuildQuickStyleMap(doc);
        var tagDefs = BuildTagDefMap(doc);
        var elements = ParseOutlines(doc, binaryData, quickStyles, tagDefs);

        // Post-process: group consecutive all-code paragraphs into CodeBlock elements
        elements = GroupCodeBlocks(elements);

        return pageStub with { Elements = elements };
    }

    /// <summary>
    /// Groups consecutive Paragraph elements where all runs are Code into CodeBlock elements.
    /// Also continues code blocks across comment lines and lines that look like code context.
    /// </summary>
    private static IReadOnlyList<ContentElement> GroupCodeBlocks(IReadOnlyList<ContentElement> elements)
    {
        var result = new List<ContentElement>();
        var codeLines = new List<string>();

        for (var idx = 0; idx < elements.Count; idx++)
        {
            var element = elements[idx];
            if (element is Paragraph p && p.Runs.Count > 0)
            {
                var text = string.Concat(p.Runs.Select(r => r.Text));
                var allCode = p.Runs.All(r => r.Code);
                var anyCode = p.Runs.Any(r => r.Code);

                if (allCode)
                {
                    codeLines.Add(text);
                }
                else if (codeLines.Count > 0 && (anyCode || IsCodeContinuation(text)))
                {
                    codeLines.Add(text);
                }
                else if (IsCodeContinuation(text))
                {
                    codeLines.Add(text);
                }
                else if (codeLines.Count > 0 && IsShortGapBeforeMoreCode(elements, idx))
                {
                    // Non-code line but more code follows — bridge the gap
                    // (e.g. a table name like "IfxUlsEvents" between let/pipe blocks)
                    codeLines.Add(text);
                }
                else
                {
                    if (codeLines.Count > 0)
                        FlushCodeLines(result, codeLines);
                    result.Add(element);
                }
            }
            else
            {
                if (codeLines.Count > 0)
                    FlushCodeLines(result, codeLines);
                result.Add(element);
            }
        }

        if (codeLines.Count > 0)
            FlushCodeLines(result, codeLines);

        return result;
    }

    /// <summary>
    /// Flushes accumulated code lines as a CodeBlock, or as individual paragraphs
    /// if there's only one line (avoid false-positive single-line code blocks from pattern matching).
    /// </summary>
    private static void FlushCodeLines(List<ContentElement> result, List<string> codeLines)
    {
        if (codeLines.Count >= 2)
        {
            result.Add(new CodeBlock(string.Join("\n", codeLines)));
        }
        else if (codeLines.Count == 1)
        {
            // Single line — only treat as code if it was monospace (Run.Code),
            // otherwise put it back as a paragraph
            result.Add(new Paragraph([new Run(codeLines[0])]));
        }
        codeLines.Clear();
    }

    /// <summary>
    /// Detects lines that should continue a code block even without monospace font.
    /// Matches comments, Kusto operators, variable declarations, and similar patterns.
    /// </summary>
    private static bool IsCodeContinuation(string line)
    {
        var trimmed = line.Trim();
        if (string.IsNullOrEmpty(trimmed)) return false;

        // Comments
        if (trimmed.StartsWith("//")) return true;

        // Kusto pipe operators
        if (trimmed.StartsWith("| ") || trimmed.StartsWith("|")) return true;

        // Variable declarations
        if (System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^(let|var|const)\s+\w+\s*=")) return true;

        // Function calls that look like table sources: EU('...'), cluster('...').database('...')
        if (System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^\w+\s*\(")) return true;

        // Semicolon-terminated statements
        if (trimmed.EndsWith(";")) return true;

        return false;
    }

    /// <summary>
    /// Looks ahead from the current position to see if more code lines follow within
    /// the next few elements. Used to bridge short gaps (e.g., a table name between
    /// code blocks) without breaking the code block.
    /// </summary>
    private static bool IsShortGapBeforeMoreCode(IReadOnlyList<ContentElement> elements, int currentIndex)
    {
        // Look ahead up to 2 elements for code
        for (var i = currentIndex + 1; i < elements.Count && i <= currentIndex + 2; i++)
        {
            if (elements[i] is Paragraph np && np.Runs.Count > 0)
            {
                var nextText = string.Concat(np.Runs.Select(r => r.Text));
                if (np.Runs.All(r => r.Code) || np.Runs.Any(r => r.Code) || IsCodeContinuation(nextText))
                    return true;
            }
        }
        return false;
    }

    private static Dictionary<string, byte[]> ExtractBinaryData(XmlDocument doc)
    {
        var map = new Dictionary<string, byte[]>();
        var dataNodes = doc.SelectNodes("//*[local-name()='Data' and @ID]");
        if (dataNodes == null) return map;

        foreach (XmlElement bin in dataNodes)
        {
            var id = bin.GetAttribute("ID");
            if (!string.IsNullOrEmpty(id) && !string.IsNullOrWhiteSpace(bin.InnerText))
            {
                try
                {
                    map[id] = Convert.FromBase64String(bin.InnerText);
                }
                catch (FormatException) { }
            }
        }

        return map;
    }

    private static Dictionary<string, QuickStyleInfo> BuildQuickStyleMap(XmlDocument doc)
    {
        var map = new Dictionary<string, QuickStyleInfo>();
        var nodes = doc.SelectNodes("//*[local-name()='QuickStyleDef']");
        if (nodes == null) return map;

        foreach (XmlElement qsd in nodes)
        {
            var idx = qsd.GetAttribute("index");
            var name = qsd.GetAttribute("name") ?? "";
            var fontSizeStr = qsd.GetAttribute("fontSize");
            var fontSize = double.TryParse(fontSizeStr, out var fs) ? fs : 0;

            if (!string.IsNullOrEmpty(idx))
                map[idx] = new QuickStyleInfo(name, fontSize);
        }

        return map;
    }

    private static Dictionary<string, TagDefInfo> BuildTagDefMap(XmlDocument doc)
    {
        var map = new Dictionary<string, TagDefInfo>();
        var nodes = doc.SelectNodes("//*[local-name()='TagDef']");
        if (nodes == null) return map;

        foreach (XmlElement td in nodes)
        {
            var idx = td.GetAttribute("index");
            var typeStr = td.GetAttribute("type");
            var symbol = td.GetAttribute("symbol");
            var name = td.GetAttribute("name") ?? "";

            if (!string.IsNullOrEmpty(idx))
            {
                var type = int.TryParse(typeStr, out var t) ? t : -1;
                // Types 0, 1, 2 with "To Do" name are checkboxes
                var isCheckbox = name.Contains("To Do", StringComparison.OrdinalIgnoreCase)
                    || symbol == "3" || type == 0 || type == 1 || type == 2;
                map[idx] = new TagDefInfo(name, type, isCheckbox);
            }
        }

        return map;
    }

    private static IReadOnlyList<ContentElement> ParseOutlines(XmlDocument doc, Dictionary<string, byte[]> binaryData, Dictionary<string, QuickStyleInfo> quickStyles, Dictionary<string, TagDefInfo> tagDefs)
    {
        var outlineNodes = doc.SelectNodes("//*[local-name()='Page']/*[local-name()='Outline']");
        if (outlineNodes == null || outlineNodes.Count == 0) return [];

        // Sort outlines by vertical then horizontal position for correct reading order
        var sorted = outlineNodes.Cast<XmlElement>()
            .OrderBy(outline =>
            {
                var pos = outline.SelectSingleNode("./*[local-name()='Position']") as XmlElement;
                var y = 0.0;
                var x = 0.0;
                if (pos != null)
                {
                    if (double.TryParse(pos.GetAttribute("y"), out var yVal)) y = yVal;
                    if (double.TryParse(pos.GetAttribute("x"), out var xVal)) x = xVal;
                }
                return y * 100000 + x;
            })
            .ToList();

        var elements = new List<ContentElement>();

        foreach (var outline in sorted)
        {
            var oeChildrenNodes = outline.SelectNodes("./*[local-name()='OEChildren']");
            if (oeChildrenNodes == null) continue;

            foreach (XmlElement oeChildren in oeChildrenNodes)
            {
                var parsed = ParseOEChildren(oeChildren, 0, binaryData, quickStyles, tagDefs);
                elements.AddRange(parsed);
            }
        }

        return elements;
    }

    private static List<ContentElement> ParseOEChildren(XmlNode node, int depth, Dictionary<string, byte[]> binaryData, Dictionary<string, QuickStyleInfo> quickStyles, Dictionary<string, TagDefInfo> tagDefs)
    {
        var elements = new List<ContentElement>();
        var oeNodes = node.SelectNodes("./*[local-name()='OE']");
        if (oeNodes == null) return elements;

        // Accumulate consecutive list items into a single list
        var bulletItems = new List<ListItem>();
        var numberedItems = new List<ListItem>();

        foreach (XmlElement oe in oeNodes)
        {
            var (isBullet, isNumber, numberText) = DetectListMarker(oe);

            if (isBullet)
            {
                FlushNumberedList(numberedItems, elements);
                var item = ParseListItem(oe, depth, binaryData, quickStyles, tagDefs, null);
                bulletItems.Add(item);
            }
            else if (isNumber)
            {
                FlushBulletList(bulletItems, elements);
                var item = ParseListItem(oe, depth, binaryData, quickStyles, tagDefs, numberText);
                numberedItems.Add(item);
            }
            else
            {
                FlushBulletList(bulletItems, elements);
                FlushNumberedList(numberedItems, elements);
                var oeElements = ParseOE(oe, depth, binaryData, quickStyles, tagDefs);
                elements.AddRange(oeElements);
            }
        }

        FlushBulletList(bulletItems, elements);
        FlushNumberedList(numberedItems, elements);

        return elements;
    }

    private static void FlushBulletList(List<ListItem> items, List<ContentElement> elements)
    {
        if (items.Count > 0)
        {
            elements.Add(new BulletList(items.ToList()));
            items.Clear();
        }
    }

    private static void FlushNumberedList(List<ListItem> items, List<ContentElement> elements)
    {
        if (items.Count > 0)
        {
            elements.Add(new NumberedList(items.ToList()));
            items.Clear();
        }
    }

    private static ListItem ParseListItem(XmlElement oe, int depth, Dictionary<string, byte[]> binaryData, Dictionary<string, QuickStyleInfo> quickStyles, Dictionary<string, TagDefInfo> tagDefs, string? numberText)
    {
        var inlineElements = ParseOEInlineContent(oe, binaryData, quickStyles);

        // Parse nested OEChildren as children of this list item
        List<ContentElement>? children = null;
        var oeChildrenNodes = oe.SelectNodes("./*[local-name()='OEChildren']");
        if (oeChildrenNodes != null && oeChildrenNodes.Count > 0)
        {
            children = [];
            foreach (XmlElement oeChildren in oeChildrenNodes)
            {
                children.AddRange(ParseOEChildren(oeChildren, depth + 1, binaryData, quickStyles, tagDefs));
            }
        }

        return new ListItem(inlineElements, numberText, children);
    }

    private static List<ContentElement> ParseOE(XmlElement oe, int depth, Dictionary<string, byte[]> binaryData, Dictionary<string, QuickStyleInfo> quickStyles, Dictionary<string, TagDefInfo> tagDefs)
    {
        var elements = new List<ContentElement>();
        var headingLevel = GetHeadingLevel(oe, quickStyles);

        // Check if this is a transparent container (no text, no list, no heading)
        var allTContent = string.Concat(
            oe.SelectNodes("./*[local-name()='T']")?.Cast<XmlElement>()
                .Select(t => HtmlFragmentParser.GetTNodeText(t).Trim()) ?? []);

        var isTransparent = headingLevel == 0 && string.IsNullOrWhiteSpace(allTContent);

        // Always parse inline content — even transparent containers may hold
        // block-level elements like Table or Image directly
        var inlineElements = ParseOEInlineContent(oe, binaryData, quickStyles);

        // Check for checkbox tags
        var tagNode = oe.SelectSingleNode("./*[local-name()='Tag']") as XmlElement;
        if (tagNode != null)
        {
            var tagIndex = tagNode.GetAttribute("index");
            var completed = tagNode.GetAttribute("completed") == "true";
            if (tagDefs.TryGetValue(tagIndex, out var tagDef) && tagDef.IsCheckbox)
            {
                var runs = inlineElements.OfType<Paragraph>()
                    .SelectMany(p => p.Runs).ToList();
                elements.Add(new Checkbox(completed, runs));

                // Process children
                var oeChildrenNodes2 = oe.SelectNodes("./*[local-name()='OEChildren']");
                if (oeChildrenNodes2 != null)
                {
                    foreach (XmlElement oeChildren in oeChildrenNodes2)
                        elements.AddRange(ParseOEChildren(oeChildren, depth + 1, binaryData, quickStyles, tagDefs));
                }
                return elements;
            }
        }

        if (!isTransparent)
        {
            if (headingLevel > 0 && inlineElements.Count > 0)
            {
                var text = string.Concat(inlineElements.OfType<Paragraph>()
                    .SelectMany(p => p.Runs).Select(r => r.Text));
                if (!string.IsNullOrWhiteSpace(text))
                    elements.Add(new Heading(headingLevel, text));
            }
            else
            {
                elements.AddRange(inlineElements);
            }
        }
        else
        {
            // For transparent containers, only add non-paragraph block elements
            // (tables, images, attachments) — skip empty paragraphs
            elements.AddRange(inlineElements.Where(e => e is not Paragraph));
        }

        // Process nested OEChildren
        var childDepth = isTransparent ? depth : depth + 1;
        var oeChildrenNodes = oe.SelectNodes("./*[local-name()='OEChildren']");
        if (oeChildrenNodes != null)
        {
            foreach (XmlElement oeChildren in oeChildrenNodes)
            {
                elements.AddRange(ParseOEChildren(oeChildren, childDepth, binaryData, quickStyles, tagDefs));
            }
        }

        return elements;
    }

    private static List<ContentElement> ParseOEInlineContent(
        XmlElement oe,
        Dictionary<string, byte[]> binaryData,
        Dictionary<string, QuickStyleInfo> quickStyles)
    {
        var elements = new List<ContentElement>();
        var runs = new List<Run>();

        foreach (XmlNode child in oe.ChildNodes)
        {
            switch (child.LocalName)
            {
                case "T":
                    var parsed = HtmlFragmentParser.ParseHtmlToRuns(
                        HtmlFragmentParser.GetTNodeText(child));
                    runs.AddRange(parsed);
                    break;

                case "Image":
                    FlushRuns(runs, elements);
                    var image = ParseImage(child, binaryData);
                    if (image != null) elements.Add(image);
                    break;

                case "Table":
                    FlushRuns(runs, elements);
                    var table = ParseTable(child, binaryData, quickStyles);
                    if (table != null) elements.Add(table);
                    break;

                case "InsertedFile":
                    FlushRuns(runs, elements);
                    var attachment = ParseAttachment(child, binaryData);
                    if (attachment != null) elements.Add(attachment);
                    break;
            }
        }

        FlushRuns(runs, elements);
        return elements;
    }

    private static void FlushRuns(List<Run> runs, List<ContentElement> elements)
    {
        if (runs.Count > 0)
        {
            elements.Add(new Paragraph(runs.ToList()));
            runs.Clear();
        }
    }

    private static Image? ParseImage(XmlNode imageNode, Dictionary<string, byte[]> binaryData)
    {
        var element = imageNode as XmlElement;
        var format = element?.GetAttribute("format");
        if (string.IsNullOrEmpty(format)) format = "png";
        format = format.ToLowerInvariant();

        var dataId = element?.GetAttribute("dataID") ?? "";

        var imgBytes = GetNodeBinaryBytes(imageNode, binaryData);
        if (imgBytes == null && string.IsNullOrEmpty(dataId)) return null;

        var fileName = $"image.{format}";
        var capturedBytes = imgBytes;

        return new Image(
            fileName,
            format,
            () => capturedBytes ?? (binaryData.TryGetValue(dataId, out var b) ? b : null));
    }

    private static Attachment? ParseAttachment(XmlNode fileNode, Dictionary<string, byte[]> binaryData)
    {
        var element = fileNode as XmlElement;
        var preferredName = element?.GetAttribute("preferredName") ?? "";
        if (string.IsNullOrEmpty(preferredName)) return null;

        var dataId = element?.GetAttribute("dataID") ?? "";
        var directBytes = GetNodeBinaryBytes(fileNode, binaryData);

        if (directBytes == null && (string.IsNullOrEmpty(dataId) || !binaryData.ContainsKey(dataId)))
            return null;

        return new Attachment(
            preferredName,
            () => directBytes ?? (binaryData.TryGetValue(dataId, out var b) ? b : null));
    }

    private static Table? ParseTable(
        XmlNode tableNode,
        Dictionary<string, byte[]> binaryData,
        Dictionary<string, QuickStyleInfo> quickStyles)
    {
        var rowNodes = tableNode.SelectNodes("./*[local-name()='Row']");
        if (rowNodes == null || rowNodes.Count == 0) return null;

        var rows = new List<TableRow>();

        foreach (XmlElement row in rowNodes)
        {
            var cellNodes = row.SelectNodes("./*[local-name()='Cell']");
            if (cellNodes == null) continue;

            var cells = new List<TableCell>();
            foreach (XmlElement cell in cellNodes)
            {
                var cellElements = new List<ContentElement>();
                // Select only the direct OEChildren of the cell, not deeply nested ones
                var oeChildrenNodes = cell.SelectNodes("./*[local-name()='OEChildren']");
                if (oeChildrenNodes != null)
                {
                    foreach (XmlElement oeChildren in oeChildrenNodes)
                    {
                        // Select only direct child OE nodes of this OEChildren
                        var oeNodes = oeChildren.SelectNodes("./*[local-name()='OE']");
                        if (oeNodes != null)
                        {
                            foreach (XmlElement oe in oeNodes)
                            {
                                cellElements.AddRange(ParseOEInlineContent(oe, binaryData, quickStyles));
                            }
                        }
                    }
                }
                cells.Add(new TableCell(cellElements));
            }

            rows.Add(new TableRow(cells));
        }

        return new Table(rows);
    }

    private static byte[]? GetNodeBinaryBytes(XmlNode node, Dictionary<string, byte[]> binaryData)
    {
        var dataId = (node as XmlElement)?.GetAttribute("dataID") ?? "";
        if (!string.IsNullOrEmpty(dataId) && binaryData.TryGetValue(dataId, out var bytes))
            return bytes;

        var dataNode = node.SelectSingleNode("./*[local-name()='Data']");
        if (dataNode != null && !string.IsNullOrWhiteSpace(dataNode.InnerText))
        {
            try
            {
                return Convert.FromBase64String(dataNode.InnerText);
            }
            catch (FormatException)
            {
                return null;
            }
        }

        return null;
    }

    private static (bool isBullet, bool isNumber, string? numberText) DetectListMarker(XmlElement oe)
    {
        var listNode = oe.SelectSingleNode("./*[local-name()='List']");
        if (listNode == null) return (false, false, null);

        if (listNode.SelectSingleNode("./*[local-name()='Bullet']") != null)
            return (true, false, null);

        var numNode = listNode.SelectSingleNode("./*[local-name()='Number']") as XmlElement;
        if (numNode != null)
        {
            var text = numNode.GetAttribute("text");
            return (false, true, string.IsNullOrEmpty(text) ? "1." : text);
        }

        return (false, false, null);
    }

    private static int GetHeadingLevel(XmlElement oe, Dictionary<string, QuickStyleInfo> quickStyles)
    {
        var qsi = oe.GetAttribute("quickStyleIndex");

        if (!string.IsNullOrEmpty(qsi) && quickStyles.TryGetValue(qsi, out var style))
        {
            if (System.Text.RegularExpressions.Regex.IsMatch(style.Name, @"^h(\d)$"))
            {
                var level = int.Parse(style.Name[1..]);
                return Math.Max(2, level + 1); // page title is h1, so offset
            }
            if (style.Name == "PageTitle") return 0;
        }

        // Check inline font-size in first T node
        var firstT = oe.SelectSingleNode("./*[local-name()='T']");
        if (firstT != null)
        {
            var tHtml = HtmlFragmentParser.GetTNodeText(firstT);
            var match = System.Text.RegularExpressions.Regex.Match(tHtml, @"font-size\s*:\s*([\d.]+)\s*pt");
            if (match.Success && double.TryParse(match.Groups[1].Value, out var fs))
            {
                if (fs >= 20) return 2;
                if (fs >= 16) return 3;
                if (fs >= 14) return 4;
            }
        }

        // Check quick-style font size
        if (!string.IsNullOrEmpty(qsi) && quickStyles.TryGetValue(qsi, out var qs))
        {
            if (qs.FontSize >= 20) return 2;
            if (qs.FontSize >= 16) return 3;
            if (qs.FontSize >= 14) return 4;
        }

        return 0;
    }

    private record QuickStyleInfo(string Name, double FontSize);
    private record TagDefInfo(string Name, int Type, bool IsCheckbox);
}
