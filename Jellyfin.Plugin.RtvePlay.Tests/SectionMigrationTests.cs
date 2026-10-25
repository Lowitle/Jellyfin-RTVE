using Jellyfin.Plugin.RtvePlay.Library;
using Xunit;

namespace Jellyfin.Plugin.RtvePlay.Tests;

/// <summary>
/// Tests of the move of the series folders written before the sections existed.
/// </summary>
public sealed class SectionMigrationTests : IDisposable
{
    private readonly string _destination = Path.Combine(
        Path.GetTempPath(),
        "rtve-play-sections-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void FindLegacyFolder_ReturnsTheFolderWrittenInTheRoot()
    {
        var legacy = CreateSeriesFolder(_destination, "Saber Vivir");
        var targetRoot = Path.Combine(_destination, ProgramSections.MagacinesFolder);

        Assert.Equal(legacy, SectionMigration.FindLegacyFolder(_destination, targetRoot, "Saber Vivir", true));
    }

    [Fact]
    public void FindLegacyFolder_ReturnsNullWhenTheSeriesIsAlreadyInItsSection()
    {
        var targetRoot = Path.Combine(_destination, ProgramSections.MagacinesFolder);
        CreateSeriesFolder(targetRoot, "Saber Vivir");

        Assert.Null(SectionMigration.FindLegacyFolder(_destination, targetRoot, "Saber Vivir", true));
    }

    [Fact]
    public void FindLegacyFolder_ReturnsNullWhenTheSectionsAreDisabled()
    {
        CreateSeriesFolder(_destination, "Saber Vivir");

        Assert.Null(SectionMigration.FindLegacyFolder(_destination, _destination, "Saber Vivir", false));
    }

    [Fact]
    public void FindLegacyFolder_MovesAProgramThatChangedOfSection()
    {
        var legacy = CreateSeriesFolder(
            Path.Combine(_destination, ProgramSections.MagacinesFolder),
            "Saber Vivir");
        var targetRoot = Path.Combine(_destination, ProgramSections.ProgramasFolder);

        Assert.Equal(legacy, SectionMigration.FindLegacyFolder(_destination, targetRoot, "Saber Vivir", true));
    }

    [Fact]
    public void FindLegacyFolder_PrefersTheCopyWrittenInTheRoot()
    {
        var legacy = CreateSeriesFolder(_destination, "Saber Vivir");
        CreateSeriesFolder(Path.Combine(_destination, ProgramSections.MagacinesFolder), "Saber Vivir");
        var targetRoot = Path.Combine(_destination, ProgramSections.NoticiasFolder);

        Assert.Equal(legacy, SectionMigration.FindLegacyFolder(_destination, targetRoot, "Saber Vivir", true));
    }

    [Fact]
    public void FindLegacyFolder_ReturnsNullForAProgramThatWasNeverWritten()
    {
        Directory.CreateDirectory(_destination);

        Assert.Null(SectionMigration.FindLegacyFolder(
            _destination,
            Path.Combine(_destination, ProgramSections.SeriesFolder),
            "Saber Vivir",
            true));
    }

    [Fact]
    public void FindDuplicateAtRoot_WarnsOnlyWhenBothCopiesExist()
    {
        var targetRoot = Path.Combine(_destination, ProgramSections.MagacinesFolder);
        var sectionCopy = CreateSeriesFolder(targetRoot, "Saber Vivir");

        Assert.Null(SectionMigration.FindDuplicateAtRoot(_destination, targetRoot, "Saber Vivir"));

        var rootCopy = CreateSeriesFolder(_destination, "Saber Vivir");

        Assert.Equal(rootCopy, SectionMigration.FindDuplicateAtRoot(_destination, targetRoot, "Saber Vivir"));
        Assert.True(Directory.Exists(sectionCopy));
    }

    [Fact]
    public void FindDuplicateAtRoot_IgnoresTheSeriesFolderItselfWhenTheDestinationIsASection()
    {
        var destination = Path.Combine(_destination, ProgramSections.MagacinesFolder);
        CreateSeriesFolder(destination, "Saber Vivir");

        Assert.Null(SectionMigration.FindDuplicateAtRoot(destination, destination, "Saber Vivir"));
    }

    [Fact]
    public void MovingTheLegacyFolderKeepsItsContentsAndHappensOnlyOnce()
    {
        var legacy = CreateSeriesFolder(_destination, "Saber Vivir");
        var targetRoot = Path.Combine(_destination, ProgramSections.MagacinesFolder);

        var found = SectionMigration.FindLegacyFolder(_destination, targetRoot, "Saber Vivir", true);
        Assert.Equal(legacy, found);

        Directory.CreateDirectory(targetRoot);
        Directory.Move(found!, StrmLibrary.SeriesFolder(targetRoot, "Saber Vivir"));

        Assert.False(Directory.Exists(legacy));
        Assert.True(File.Exists(Path.Combine(targetRoot, "Saber Vivir", "tvshow.nfo")));
        Assert.Null(SectionMigration.FindLegacyFolder(_destination, targetRoot, "Saber Vivir", true));
        Assert.Null(SectionMigration.FindDuplicateAtRoot(_destination, targetRoot, "Saber Vivir"));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_destination))
            {
                Directory.Delete(_destination, true);
            }
        }
        catch (IOException)
        {
            // A locked temp folder must not fail the test run.
        }
    }

    private static string CreateSeriesFolder(string root, string title)
    {
        var folder = Path.Combine(root, LibraryNaming.SeriesFolderName(title));
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "tvshow.nfo"), "<tvshow />");

        return folder;
    }
}
