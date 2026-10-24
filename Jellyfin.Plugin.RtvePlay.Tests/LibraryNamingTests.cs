using Jellyfin.Plugin.RtvePlay.Library;
using Xunit;

namespace Jellyfin.Plugin.RtvePlay.Tests;

/// <summary>
/// Tests of the file and folder names written to the library folder.
/// </summary>
public class LibraryNamingTests
{
    [Theory]
    [InlineData("Saber vivir: ¿qué pasa?", "Saber vivir ¿qué pasa")]
    [InlineData("A/B\\C|D?E*F<G>H\"I", "A B C D E F G H I")]
    [InlineData("  Nombre   con    espacios  ", "Nombre con espacios")]
    [InlineData("Nombre...", "Nombre")]
    [InlineData("Nombre - ", "Nombre")]
    [InlineData("a\tb\nc", "a b c")]
    public void Sanitize_RemovesForbiddenCharacters(string value, string expected)
    {
        Assert.Equal(expected, LibraryNaming.Sanitize(value, 100, "Serie"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("***")]
    [InlineData("...")]
    public void Sanitize_ReturnsTheFallbackWhenNothingUsableRemains(string? value)
    {
        Assert.Equal("Serie", LibraryNaming.Sanitize(value, 100, "Serie"));
    }

    [Fact]
    public void Sanitize_TruncatesAndRemovesDanglingSeparators()
    {
        Assert.Equal("aaaaaaaaaa", LibraryNaming.Sanitize(new string('a', 50), 10, "Serie"));
        Assert.Equal("aaaaaaaaa", LibraryNaming.Sanitize(new string('a', 9) + "-colita", 10, "Serie"));
    }

    [Fact]
    public void SeriesFolderName_UsesTheFallbackForEmptyTitles()
    {
        Assert.Equal("Saber vivir", LibraryNaming.SeriesFolderName("Saber vivir"));
        Assert.Equal("Serie", LibraryNaming.SeriesFolderName("???"));
    }

    [Theory]
    [InlineData(1, 3, "01/10/24", "Saber vivir - S01E03 - 01 10 24")]
    [InlineData(12, 7, "Título", "Saber vivir - S12E07 - Título")]
    [InlineData(1, 1, "", "Saber vivir - S01E01")]
    public void EpisodeFileName_BuildsTheJellyfinNamingScheme(int season, int episode, string title, string expected)
    {
        Assert.Equal(expected, LibraryNaming.EpisodeFileName("Saber vivir", season, episode, title));
    }

    [Fact]
    public void EpisodeFileName_KeepsTheNameEvenWithoutTitle()
    {
        Assert.Equal(
            "Saber vivir - S01E02",
            LibraryNaming.EpisodeFileName("Saber vivir", 1, 2, null));
    }

    [Fact]
    public void EpisodeFileName_TruncatesLongTitles()
    {
        var name = LibraryNaming.EpisodeFileName("Saber vivir", 1, 2, new string('x', 300));

        Assert.True(name.Length <= 160);
        Assert.StartsWith("Saber vivir - S01E02 - ", name, StringComparison.Ordinal);
    }
}
