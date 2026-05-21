using OneNoteUtils.Core.Models;

namespace OneNoteUtils.Core.Sync;

/// <summary>
/// The result of comparing a SyncManifest against the current OneNote hierarchy.
/// </summary>
public class SyncPlan
{
    /// <summary>Pages not in the manifest — need full export.</summary>
    public List<SyncPageAction> NewPages { get; set; } = [];

    /// <summary>Pages with a newer lastModifiedTime — need re-export.</summary>
    public List<SyncPageAction> ModifiedPages { get; set; } = [];

    /// <summary>Pages in the manifest but no longer in OneNote — need cleanup.</summary>
    public List<SyncPageAction> DeletedPages { get; set; } = [];

    /// <summary>Pages that haven't changed — skip.</summary>
    public List<SyncPageAction> UnchangedPages { get; set; } = [];

    public int TotalWork => NewPages.Count + ModifiedPages.Count + DeletedPages.Count;
}

public class SyncPageAction
{
    public string PageId { get; set; } = "";
    public string Title { get; set; } = "";
    public string Section { get; set; } = "";
    public DateTime? LastModified { get; set; }

    /// <summary>Only set for deleted/modified pages — the previous manifest entry.</summary>
    public SyncPageEntry? PreviousEntry { get; set; }
}

/// <summary>
/// Compares a SyncManifest against the current OneNote hierarchy to produce a SyncPlan.
/// </summary>
public static class SyncDiffer
{
    /// <summary>
    /// Diffs the manifest against the current notebook to determine what needs syncing.
    /// </summary>
    public static SyncPlan Diff(SyncManifest manifest, Notebook notebook)
    {
        var plan = new SyncPlan();

        // Build a set of all current page IDs for deletion detection
        var currentPageIds = new HashSet<string>();

        foreach (var section in notebook.Sections)
        {
            foreach (var page in section.Pages)
            {
                currentPageIds.Add(page.PageId);

                if (!manifest.Pages.TryGetValue(page.PageId, out var entry))
                {
                    // New page — not in manifest
                    plan.NewPages.Add(new SyncPageAction
                    {
                        PageId = page.PageId,
                        Title = page.Title,
                        Section = section.Name,
                        LastModified = page.LastModified
                    });
                }
                else if (IsModified(page, entry))
                {
                    // Modified or renamed
                    plan.ModifiedPages.Add(new SyncPageAction
                    {
                        PageId = page.PageId,
                        Title = page.Title,
                        Section = section.Name,
                        LastModified = page.LastModified,
                        PreviousEntry = entry
                    });
                }
                else
                {
                    // Unchanged
                    plan.UnchangedPages.Add(new SyncPageAction
                    {
                        PageId = page.PageId,
                        Title = page.Title,
                        Section = section.Name,
                        LastModified = page.LastModified
                    });
                }
            }
        }

        // Detect deletions — pages in manifest but not in current hierarchy
        foreach (var (pageId, entry) in manifest.Pages)
        {
            if (!currentPageIds.Contains(pageId))
            {
                plan.DeletedPages.Add(new SyncPageAction
                {
                    PageId = pageId,
                    Title = entry.Title,
                    Section = entry.Section,
                    PreviousEntry = entry
                });
            }
        }

        return plan;
    }

    private static bool IsModified(Page page, SyncPageEntry entry)
    {
        // Title changed (rename)
        if (page.Title != entry.Title)
            return true;

        // Timestamp newer than what we last synced
        if (page.LastModified.HasValue && entry.LastModified.HasValue
            && page.LastModified.Value > entry.LastModified.Value)
            return true;

        // Had no timestamp before but has one now (or vice versa)
        if (page.LastModified.HasValue != entry.LastModified.HasValue)
            return true;

        return false;
    }
}
