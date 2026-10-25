using Jellyfin.Plugin.RtvePlay.Library;
using Xunit;

namespace Jellyfin.Plugin.RtvePlay.Tests;

/// <summary>
/// Tests of the decisions taken before creating a library from the configuration page (the button
/// that saves the trip through the «Add media library» wizard).
/// </summary>
public sealed class LibraryProvisionerTests
{
    private static readonly string Root =
        Path.Combine(Path.GetTempPath(), "rtve-library");

    [Fact]
    public void Plan_CreatesTheLibraryWhenNothingMatches()
    {
        var plan = LibraryProvisioner.Plan(
            LibraryProvisioner.DefaultLibraryName,
            Path.Combine(Root, "rtve-play"),
            []);

        Assert.Equal(LibraryProvisionAction.Create, plan.Action);
        Assert.Equal("RTVE Play", plan.LibraryName);
        Assert.Null(plan.ExistingLibrary);
        Assert.Contains("Se creará", plan.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Plan_DetectsAFolderThatIsAlreadyIndexed()
    {
        var plan = LibraryProvisioner.Plan(
            LibraryProvisioner.DefaultLibraryName,
            Path.Combine(Root, "rtve-play"),
            [TvLibrary("Series", Path.Combine(Root, "rtve-play"))]);

        Assert.Equal(LibraryProvisionAction.AlreadyPresent, plan.Action);
        Assert.Equal("Series", plan.ExistingLibrary);
        Assert.Contains("Series", plan.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Plan_IgnoresTheTrailingSeparatorOfTheStoredPath()
    {
        var path = Path.Combine(Root, "rtve-play");
        var plan = LibraryProvisioner.Plan(
            LibraryProvisioner.DefaultLibraryName,
            path + Path.DirectorySeparatorChar,
            [TvLibrary("Series", path)]);

        Assert.Equal(LibraryProvisionAction.AlreadyPresent, plan.Action);
    }

    [Fact]
    public void Plan_RejectsAFolderThatBelongsToALibraryOfAnotherType()
    {
        var path = Path.Combine(Root, "rtve-play");
        var plan = LibraryProvisioner.Plan(
            LibraryProvisioner.DefaultLibraryName,
            path,
            [MoviesLibrary("Películas", path)]);

        Assert.Equal(LibraryProvisionAction.Conflict, plan.Action);
        Assert.Equal("Películas", plan.ExistingLibrary);
    }

    [Fact]
    public void Plan_AddsTheFolderToAnExistingLibraryWithTheSameName()
    {
        var plan = LibraryProvisioner.Plan(
            "Series",
            Path.Combine(Root, "otra-carpeta"),
            [TvLibrary("series", Path.Combine(Root, "principal"))]);

        Assert.Equal(LibraryProvisionAction.AddPath, plan.Action);
        Assert.Equal("series", plan.LibraryName);
    }

    [Fact]
    public void Plan_RejectsANameAlreadyUsedByAnotherType()
    {
        var plan = LibraryProvisioner.Plan(
            "Películas",
            Path.Combine(Root, "rtve-play"),
            [MoviesLibrary("Películas", Path.Combine(Root, "cine"))]);

        Assert.Equal(LibraryProvisionAction.Conflict, plan.Action);
        Assert.Contains("Programas de TV", plan.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Plan_RequiresANameAndAPath()
    {
        Assert.Throws<ArgumentException>(() => LibraryProvisioner.Plan(" ", "/media/rtve", []));
        Assert.Throws<ArgumentException>(() => LibraryProvisioner.Plan("RTVE Play", "  ", []));
    }

    private static LibraryFolder TvLibrary(string name, string path)
        => new(name, true, [path]);

    private static LibraryFolder MoviesLibrary(string name, string path)
        => new(name, false, [path]);
}
