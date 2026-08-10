using FluentAssertions;
using OneNoteUtils.Core.Parsing;

namespace OneNoteUtils.Core.Tests.Parsing;

public class SectionResolverTests
{
    private const string Xml = """
        <?xml version="1.0"?>
        <one:Notebooks xmlns:one="http://schemas.microsoft.com/office/onenote/2013/onenote">
          <one:Notebook name="Work">
            <one:Section name="Meetings" ID="{section-meetings}" />
            <one:Section name="Ideas" ID="{section-ideas}" />
            <one:SectionGroup name="Archive 2024">
              <one:Section name="Nested" ID="{section-nested}" />
            </one:SectionGroup>
            <one:Section name="Deleted" ID="{section-deleted}" isInRecycleBin="true" />
          </one:Notebook>
          <one:Notebook name="Personal" />
          <one:Notebook name="Old Notebook" isInRecycleBin="true" />
        </one:Notebooks>
        """;

    [Fact]
    public void Resolve_ReturnsSectionId()
    {
        var result = SectionResolver.Resolve(Xml, "Work", "Meetings");

        result.IsFound.Should().BeTrue();
        result.SectionId.Should().Be("{section-meetings}");
    }

    [Fact]
    public void Resolve_MatchesNamesCaseInsensitively()
    {
        SectionResolver.Resolve(Xml, "work", "meetings").IsFound.Should().BeTrue();
    }

    [Fact]
    public void Resolve_FindsSectionsInsideSectionGroups()
    {
        SectionResolver.Resolve(Xml, "Work", "Nested").SectionId.Should().Be("{section-nested}");
    }

    [Fact]
    public void Resolve_ReportsClosedNotebookAsNotebookProblem()
    {
        // Regression: this used to surface as "Section not found", sending users after the wrong fix.
        var result = SectionResolver.Resolve(Xml, "NotOpen", "Meetings");

        result.Status.Should().Be(SectionLookupStatus.NotebookNotFound);
        result.Candidates.Should().BeEquivalentTo("Work", "Personal");
    }

    [Fact]
    public void Resolve_ClosedNotebookMessageNamesNotebookAndListsOpenOnes()
    {
        var message = SectionResolver.Resolve(Xml, "NotOpen", "Meetings")
            .ToErrorMessage("NotOpen", "Meetings");

        message.Should().Contain("Notebook 'NotOpen' not found");
        message.Should().Contain("open in OneNote");
        message.Should().Contain("Work");
        message.Should().NotContain("Section 'Meetings' not found");
    }

    [Fact]
    public void Resolve_ReportsMissingSectionAsSectionProblem()
    {
        var result = SectionResolver.Resolve(Xml, "Work", "Nope");

        result.Status.Should().Be(SectionLookupStatus.SectionNotFound);
        result.SectionId.Should().BeNull();
    }

    [Fact]
    public void Resolve_MissingSectionMessageSuggestsNearMatches()
    {
        var message = SectionResolver.Resolve(Xml, "Work", "Meeting")
            .ToErrorMessage("Work", "Meeting");

        message.Should().Contain("Closest sections");
        message.Should().Contain("Meetings");
    }

    [Fact]
    public void Resolve_IgnoresRecycleBinSections()
    {
        // Matching one would silently push the page into the recycle bin.
        SectionResolver.Resolve(Xml, "Work", "Deleted").Status
            .Should().Be(SectionLookupStatus.SectionNotFound);
    }

    [Fact]
    public void Resolve_IgnoresRecycleBinNotebooks()
    {
        SectionResolver.Resolve(Xml, "Old Notebook", "Meetings").Status
            .Should().Be(SectionLookupStatus.NotebookNotFound);
    }

    [Fact]
    public void Resolve_HandlesNotebookWithNoSections()
    {
        var result = SectionResolver.Resolve(Xml, "Personal", "Meetings");

        result.Status.Should().Be(SectionLookupStatus.SectionNotFound);
        result.ToErrorMessage("Personal", "Meetings").Should().Contain("(none)");
    }

    [Fact]
    public void ToErrorMessage_IsEmptyOnSuccess()
    {
        SectionResolver.Resolve(Xml, "Work", "Meetings").ToErrorMessage("Work", "Meetings")
            .Should().BeEmpty();
    }
}
