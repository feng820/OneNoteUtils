using System.Text.Json;
using System.Text.Json.Serialization;

namespace OneNoteUtils.Core.Sync;

/// <summary>
/// Persisted record of what was last synced, stored as .onenote-sync.json
/// in the output directory.
/// </summary>
public class SyncManifest
{
    public const string FileName = ".onenote-sync.json";

    [JsonPropertyName("notebookName")]
    public string NotebookName { get; set; } = "";

    [JsonPropertyName("lastSyncTime")]
    public DateTime LastSyncTime { get; set; }

    [JsonPropertyName("pages")]
    public Dictionary<string, SyncPageEntry> Pages { get; set; } = new();

    [JsonPropertyName("pushed")]
    public Dictionary<string, PushEntry> Pushed { get; set; } = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Loads a manifest from the output directory. Returns an empty manifest if none exists.
    /// </summary>
    public static SyncManifest Load(string outputPath)
    {
        var path = Path.Combine(outputPath, FileName);
        if (!File.Exists(path))
            return new SyncManifest();

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<SyncManifest>(json, JsonOptions) ?? new SyncManifest();
    }

    /// <summary>
    /// Saves the manifest to the output directory.
    /// </summary>
    public void Save(string outputPath)
    {
        Directory.CreateDirectory(outputPath);
        var path = Path.Combine(outputPath, FileName);
        var json = JsonSerializer.Serialize(this, JsonOptions);
        File.WriteAllText(path, json);
    }
}

/// <summary>
/// Per-page entry in the sync manifest.
/// </summary>
public class SyncPageEntry
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("section")]
    public string Section { get; set; } = "";

    [JsonPropertyName("lastModified")]
    public DateTime? LastModified { get; set; }

    [JsonPropertyName("exportedPath")]
    public string ExportedPath { get; set; } = "";

    [JsonPropertyName("exportedFiles")]
    public List<string> ExportedFiles { get; set; } = [];
}

/// <summary>
/// Per-file entry tracking a markdown file pushed to OneNote.
/// </summary>
public class PushEntry
{
    [JsonPropertyName("pageId")]
    public string PageId { get; set; } = "";

    [JsonPropertyName("notebookName")]
    public string NotebookName { get; set; } = "";

    [JsonPropertyName("sectionName")]
    public string SectionName { get; set; } = "";

    [JsonPropertyName("lastPushed")]
    public DateTime LastPushed { get; set; }
}
