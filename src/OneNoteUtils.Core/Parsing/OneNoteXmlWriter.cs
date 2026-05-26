using System.Text;
using System.Web;
using OneNoteUtils.Core.Models;

namespace OneNoteUtils.Core.Parsing;

/// <summary>
/// Serializes a list of ContentElements into OneNote page XML
/// suitable for UpdatePageContent.
/// </summary>
public static class OneNoteXmlWriter
{
    private const string OneNoteNs = "http://schemas.microsoft.com/office/onenote/2013/onenote";

    /// <summary>
    /// Creates a full page XML document for UpdatePageContent.
    /// If pageId is provided, updates an existing page; otherwise creates content for a new page.
    /// </summary>
    public static string BuildPageXml(string pageId, string title, IReadOnlyList<ContentElement> elements)
    {
        var sb = new StringBuilder();
        sb.Append($"<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.Append($"<one:Page xmlns:one=\"{OneNoteNs}\" ID=\"{Escape(pageId)}\">");

        // Title
        sb.Append("<one:Title>");
        sb.Append("<one:OE>");
        sb.Append($"<one:T><![CDATA[{HtmlEncode(title)}]]></one:T>");
        sb.Append("</one:OE>");
        sb.Append("</one:Title>");

        // Content in a single Outline
        sb.Append("<one:Outline>");
        sb.Append("<one:OEChildren>");

        foreach (var element in elements)
        {
            WriteElement(sb, element);
        }

        sb.Append("</one:OEChildren>");
        sb.Append("</one:Outline>");
        sb.Append("</one:Page>");

        return sb.ToString();
    }

    private static void WriteElement(StringBuilder sb, ContentElement element)
    {
        switch (element)
        {
            case Heading heading:
                WriteHeading(sb, heading);
                break;
            case Paragraph paragraph:
                WriteParagraph(sb, paragraph);
                break;
            case BulletList bulletList:
                foreach (var item in bulletList.Items)
                    WriteListItem(sb, item, isBullet: true);
                break;
            case NumberedList numberedList:
                foreach (var item in numberedList.Items)
                    WriteListItem(sb, item, isBullet: false);
                break;
            case Table table:
                WriteTable(sb, table);
                break;
        }
    }

    private static void WriteHeading(StringBuilder sb, Heading heading)
    {
        // OneNote uses QuickStyleDef for headings; for UpdatePageContent
        // we use inline font-size styling to approximate heading levels
        var fontSize = heading.Level switch
        {
            1 => 20,
            2 => 17,
            3 => 14,
            4 => 12,
            _ => 11
        };

        sb.Append("<one:OE>");
        sb.Append($"<one:T><![CDATA[<span style=\"font-size:{fontSize}pt;font-weight:bold\">");
        sb.Append(HtmlEncode(heading.Text));
        sb.Append("</span>]]></one:T>");
        sb.Append("</one:OE>");
    }

    private static void WriteParagraph(StringBuilder sb, Paragraph paragraph)
    {
        sb.Append("<one:OE>");
        sb.Append("<one:T><![CDATA[");
        foreach (var run in paragraph.Runs)
        {
            WriteRun(sb, run);
        }
        sb.Append("]]></one:T>");
        sb.Append("</one:OE>");
    }

    private static void WriteRun(StringBuilder sb, Run run)
    {
        var text = HtmlEncode(run.Text);
        var hasStyle = run.Bold || run.Italic || run.Strikethrough || run.Underline;

        if (run.HrefUrl != null)
        {
            var inner = text;
            if (hasStyle) inner = WrapWithStyles(inner, run);
            sb.Append($"<a href=\"{HtmlEncode(run.HrefUrl)}\">{inner}</a>");
            return;
        }

        if (hasStyle)
        {
            sb.Append(WrapWithStyles(text, run));
            return;
        }

        sb.Append(text);
    }

    private static string WrapWithStyles(string text, Run run)
    {
        var styles = new List<string>();
        if (run.Bold) styles.Add("font-weight:bold");
        if (run.Italic) styles.Add("font-style:italic");
        if (run.Strikethrough) styles.Add("text-decoration:line-through");
        if (run.Underline) styles.Add("text-decoration:underline");

        return $"<span style=\"{string.Join(";", styles)}\">{text}</span>";
    }

    private static void WriteListItem(StringBuilder sb, ListItem item, bool isBullet)
    {
        sb.Append("<one:OE>");

        // List marker
        sb.Append("<one:List>");
        if (isBullet)
            sb.Append("<one:Bullet bullet=\"2\" fontSize=\"11.0\"/>");
        else
            sb.Append("<one:Number numberSequence=\"0\" numberFormat=\"##.\" fontSize=\"11.0\"/>");
        sb.Append("</one:List>");

        // Item text
        sb.Append("<one:T><![CDATA[");
        foreach (var element in item.Elements)
        {
            if (element is Paragraph p)
            {
                foreach (var run in p.Runs)
                    WriteRun(sb, run);
            }
        }
        sb.Append("]]></one:T>");

        // Nested children
        if (item.Children is { Count: > 0 })
        {
            sb.Append("<one:OEChildren>");
            foreach (var child in item.Children)
            {
                WriteElement(sb, child);
            }
            sb.Append("</one:OEChildren>");
        }

        sb.Append("</one:OE>");
    }

    private static void WriteTable(StringBuilder sb, Table table)
    {
        if (table.Rows.Count == 0) return;

        var colCount = table.Rows.Max(r => r.Cells.Count);

        sb.Append("<one:OE>");
        sb.Append("<one:Table bordersVisible=\"true\">");

        // Columns
        sb.Append("<one:Columns>");
        for (int c = 0; c < colCount; c++)
            sb.Append($"<one:Column index=\"{c}\" width=\"120\"/>");
        sb.Append("</one:Columns>");

        // Rows
        foreach (var row in table.Rows)
        {
            sb.Append("<one:Row>");
            for (int c = 0; c < colCount; c++)
            {
                sb.Append("<one:Cell>");
                sb.Append("<one:OEChildren>");
                if (c < row.Cells.Count)
                {
                    foreach (var element in row.Cells[c].Elements)
                    {
                        if (element is Paragraph p)
                            WriteParagraph(sb, p);
                        else
                            sb.Append("<one:OE><one:T><![CDATA[]]></one:T></one:OE>");
                    }
                }
                else
                {
                    sb.Append("<one:OE><one:T><![CDATA[]]></one:T></one:OE>");
                }
                sb.Append("</one:OEChildren>");
                sb.Append("</one:Cell>");
            }
            sb.Append("</one:Row>");
        }

        sb.Append("</one:Table>");
        sb.Append("</one:OE>");
    }

    private static string HtmlEncode(string text) => HttpUtility.HtmlEncode(text);
    private static string Escape(string text) => HttpUtility.HtmlAttributeEncode(text);
}
