using System.Text.RegularExpressions;
using System.Linq;

namespace OneNoteUtils.Core;

/// <summary>
/// Shared utility methods for file naming and sanitization.
/// </summary>
public static class FileNameUtils
{
    /// <summary>
    /// Sanitizes a string for use as a file or folder name.
    /// </summary>
    public static string SanitizeFileBaseName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "Untitled";

        var clean = name;
        // Replace path/date separators with dashes
        clean = Regex.Replace(clean, @"[\\/]", "-");
        // Remove Windows invalid filename chars
        clean = Regex.Replace(clean, @"[:*?""<>|]", "");
        // Remove Obsidian link-problematic characters
        clean = Regex.Replace(clean, @"[#\^\[\]]", "");
        clean = Regex.Replace(clean, @"%{2,}", "");
        clean = clean.Trim();

        if (clean.Length == 0) return "Untitled";

        // Truncate to avoid Windows MAX_PATH issues
        const int maxBaseLen = 120;
        if (clean.Length > maxBaseLen)
            clean = clean[..maxBaseLen].TrimEnd();

        return clean;
    }

    /// <summary>
    /// Maps a logical section path (segments joined by '/') to a nested folder path,
    /// sanitizing each segment individually. A section group becomes a folder and each
    /// section it contains becomes a subfolder.
    /// Note: '/' is treated as the segment delimiter, so a section whose name literally
    /// contains '/' will be split across multiple folders.
    /// </summary>
    public static string SectionPathToFolder(string sectionPath)
    {
        if (string.IsNullOrWhiteSpace(sectionPath)) return "Untitled";

        var segments = sectionPath
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(SanitizeFileBaseName)
            .ToArray();

        return segments.Length == 0 ? "Untitled" : Path.Combine(segments);
    }

    /// <summary>
    /// Extracts a short ID suffix from a OneNote ID string.
    /// </summary>
    public static string ShortId(string id, int length = 8)
    {
        if (string.IsNullOrWhiteSpace(id)) return "unknownid";

        var cleanId = Regex.Replace(id, @"[^A-Za-z0-9]", "");
        if (string.IsNullOrWhiteSpace(cleanId)) return "unknownid";
        if (cleanId.Length <= length) return cleanId;
        return cleanId[^length..];
    }
}
