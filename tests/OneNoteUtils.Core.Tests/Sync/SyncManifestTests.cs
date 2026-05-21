using FluentAssertions;
using OneNoteUtils.Core.Sync;

namespace OneNoteUtils.Core.Tests.Sync;

public class SyncManifestTests : IDisposable
{
    private readonly string _tempDir;

    public SyncManifestTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "SyncManifestTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void Load_ReturnsEmptyManifest_WhenFileDoesNotExist()
    {
        var manifest = SyncManifest.Load(_tempDir);

        manifest.Should().NotBeNull();
        manifest.Pages.Should().BeEmpty();
        manifest.NotebookName.Should().BeEmpty();
    }

    [Fact]
    public void Save_ThenLoad_RoundTrips()
    {
        var manifest = new SyncManifest
        {
            NotebookName = "Test Notebook",
            LastSyncTime = new DateTime(2026, 5, 21, 12, 0, 0, DateTimeKind.Utc),
            Pages =
            {
                ["page-001"] = new SyncPageEntry
                {
                    Title = "My Page",
                    Section = "Daily Notes",
                    LastModified = new DateTime(2026, 5, 20, 9, 30, 0, DateTimeKind.Utc),
                    ExportedPath = @"C:\Export\Notebook\Section\My Page.md",
                    ExportedFiles = [@"C:\Export\Notebook\Section\_attachments\image01.png"]
                }
            }
        };

        manifest.Save(_tempDir);
        var loaded = SyncManifest.Load(_tempDir);

        loaded.NotebookName.Should().Be("Test Notebook");
        loaded.Pages.Should().HaveCount(1);
        loaded.Pages["page-001"].Title.Should().Be("My Page");
        loaded.Pages["page-001"].Section.Should().Be("Daily Notes");
        loaded.Pages["page-001"].ExportedPath.Should().Contain("My Page.md");
        loaded.Pages["page-001"].ExportedFiles.Should().HaveCount(1);
    }

    [Fact]
    public void Save_CreatesDirectoryIfMissing()
    {
        var subDir = Path.Combine(_tempDir, "nested", "path");

        var manifest = new SyncManifest { NotebookName = "Test" };
        manifest.Save(subDir);

        File.Exists(Path.Combine(subDir, SyncManifest.FileName)).Should().BeTrue();
    }

    [Fact]
    public void Save_OverwritesExistingManifest()
    {
        var m1 = new SyncManifest { NotebookName = "First" };
        m1.Save(_tempDir);

        var m2 = new SyncManifest { NotebookName = "Second" };
        m2.Save(_tempDir);

        var loaded = SyncManifest.Load(_tempDir);
        loaded.NotebookName.Should().Be("Second");
    }
}
