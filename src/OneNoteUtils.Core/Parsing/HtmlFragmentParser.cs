using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using OneNoteUtils.Core.Models;

namespace OneNoteUtils.Core.Parsing;

/// <summary>
/// Converts HTML fragments from OneNote T nodes into Run objects with formatting.
/// </summary>
public static class HtmlFragmentParser
{
    /// <summary>
    /// Extracts the text/HTML content from a OneNote T node (handles CDATA, text, elements).
    /// </summary>
    public static string GetTNodeText(XmlNode tNode)
    {
        var sb = new StringBuilder();
        foreach (XmlNode child in tNode.ChildNodes)
        {
            switch (child.NodeType)
            {
                case XmlNodeType.CDATA:
                case XmlNodeType.Text:
                case XmlNodeType.SignificantWhitespace:
                case XmlNodeType.Whitespace:
                    sb.Append(child.Value);
                    break;
                case XmlNodeType.Element:
                    sb.Append(child.OuterXml);
                    break;
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Parses an HTML fragment (from a T node) into a list of Runs with formatting.
    /// </summary>
    public static IReadOnlyList<Run> ParseHtmlToRuns(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return [];

        // Normalize for XML parsing
        var normalized = html;
        normalized = Regex.Replace(normalized, @"<br\s*/?>", "<br/>");
        normalized = normalized.Replace("&nbsp;", "&#160;");
        normalized = Regex.Replace(normalized, @"<(img|hr|input)(\s[^>]*?)(?<!/)>", "<$1$2/>");

        var xmlStr = $"<root>{normalized}</root>";

        try
        {
            var doc = new XmlDocument();
            doc.PreserveWhitespace = true;
            doc.LoadXml(xmlStr);
            var runs = new List<Run>();
            WalkHtmlNode(doc.DocumentElement!, runs, false, false, false, false, null);
            return runs;
        }
        catch (XmlException)
        {
            // Fallback: strip tags and decode common entities
            return [new Run(StripHtmlTags(html))];
        }
    }

    private static void WalkHtmlNode(
        XmlNode node,
        List<Run> runs,
        bool bold,
        bool italic,
        bool strikethrough,
        bool underline,
        string? href)
    {
        foreach (XmlNode child in node.ChildNodes)
        {
            if (child.NodeType is XmlNodeType.Text or XmlNodeType.Whitespace
                or XmlNodeType.SignificantWhitespace)
            {
                var text = child.Value ?? "";
                if (text.Length > 0)
                    runs.Add(new Run(text, Bold: bold, Italic: italic, Strikethrough: strikethrough, Underline: underline, HrefUrl: href));
            }
            else if (child.NodeType == XmlNodeType.Element)
            {
                var el = (XmlElement)child;
                var tag = el.LocalName.ToLowerInvariant();
                var style = el.GetAttribute("style") ?? "";

                var newBold = bold;
                var newItalic = italic;
                var newStrike = strikethrough;
                var newUnderline = underline;
                var newHref = href;

                switch (tag)
                {
                    case "b":
                    case "strong":
                        newBold = true;
                        break;
                    case "i":
                    case "em":
                        newItalic = true;
                        break;
                    case "s":
                    case "strike":
                        newStrike = true;
                        break;
                    case "u":
                        newUnderline = true;
                        break;
                    case "a":
                        newHref = el.GetAttribute("href");
                        break;
                    case "br":
                        runs.Add(new Run("\n"));
                        continue;
                    case "span":
                        if (Regex.IsMatch(style, @"font-weight\s*:\s*bold")) newBold = true;
                        if (Regex.IsMatch(style, @"font-style\s*:\s*italic")) newItalic = true;
                        if (Regex.IsMatch(style, @"text-decoration\s*:\s*line-through")) newStrike = true;
                        break;
                }

                WalkHtmlNode(el, runs, newBold, newItalic, newStrike, newUnderline, newHref);
            }
        }
    }

    private static string StripHtmlTags(string html)
    {
        var stripped = Regex.Replace(html, "<[^>]+>", "");
        stripped = stripped.Replace("&amp;", "&");
        stripped = stripped.Replace("&lt;", "<");
        stripped = stripped.Replace("&gt;", ">");
        stripped = stripped.Replace("&quot;", "\"");
        stripped = stripped.Replace("&#160;", " ");
        stripped = stripped.Replace("&nbsp;", " ");
        return stripped;
    }
}
