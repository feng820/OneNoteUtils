using FluentAssertions;

namespace OneNoteUtils.Core.Tests;

public class FileNameUtilsTests
{
    [Theory]
    [InlineData("Normal Name", "Normal Name")]
    [InlineData("Has:Colons", "HasColons")]
    [InlineData("Has*Stars", "HasStars")]
    [InlineData("Has/Slashes", "Has-Slashes")]
    [InlineData("Has\\Backslash", "Has-Backslash")]
    [InlineData("Has[Brackets]", "HasBrackets")]
    [InlineData("Has#Hash", "HasHash")]
    [InlineData("", "Untitled")]
    [InlineData("   ", "Untitled")]
    public void SanitizeFileBaseName_HandlesSpecialCharacters(string input, string expected)
    {
        var result = FileNameUtils.SanitizeFileBaseName(input);
        result.Should().Be(expected);
    }

    [Fact]
    public void SanitizeFileBaseName_TruncatesLongNames()
    {
        var longName = new string('A', 200);
        var result = FileNameUtils.SanitizeFileBaseName(longName);
        result.Length.Should().BeLessThanOrEqualTo(120);
    }

    [Fact]
    public void ShortId_ExtractsLastNCharacters()
    {
        var result = FileNameUtils.ShortId("{ABCD-1234-EFGH-5678}", 8);
        result.Should().HaveLength(8);
        result.Should().MatchRegex("^[A-Za-z0-9]+$");
    }

    [Fact]
    public void ShortId_HandlesEmptyString()
    {
        FileNameUtils.ShortId("").Should().Be("unknownid");
        FileNameUtils.ShortId("   ").Should().Be("unknownid");
    }

    [Fact]
    public void ShortId_HandlesShortInput()
    {
        FileNameUtils.ShortId("ABC", 8).Should().Be("ABC");
    }
}
