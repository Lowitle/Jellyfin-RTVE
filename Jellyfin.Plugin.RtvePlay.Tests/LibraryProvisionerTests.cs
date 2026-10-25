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
    public void Plan_CreatesTheLibraryWithOneFolderPerSection()
    {
        var destination = Folder("rtve-play");

        var plan = LibraryProvisioner.Plan(LibraryProvisioner.DefaultLibraryName, destination, []);

        Assert.Equal(LibraryProvisionAction.Create, plan.Action);
        Assert.Equal("RTVE Play", plan.LibraryName);
        Assert.Null(plan.ExistingLibrary);
        Assert.Empty(plan.Removals);
        Assert.Equal(ProgramSections.SectionPaths(destination), plan.Additions);
        Assert.DoesNotContain(destination, plan.Additions);
        Assert.Contains($"con {ProgramSections.FolderNames.Count} carpetas", plan.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Plan_UsesTheRootWhenTheSectionsAreDisabled()
    {
        var destination = Folder("rtve-play");

        var plan = LibraryProvisioner.Plan(
            LibraryProvisioner.DefaultLibraryName,
            destination,
            [],
            organizeSections: false);

        Assert.Equal(LibraryProvisionAction.Create, plan.Action);
        Assert.Equal(new[] { destination }, plan.Additions);
    }

    [Fact]
    public void Plan_UsesTheFolderItselfWhenTheDestinationIsAlreadyASection()
    {
        var destination = Folder("rtve-play", ProgramSections.DocumentalesFolder);

        var plan = LibraryProvisioner.Plan(LibraryProvisioner.DefaultLibraryName, destination, []);

        Assert.Equal(LibraryProvisionAction.Create, plan.Action);
        Assert.Equal(new[] { destination }, plan.Additions);
    }

    [Fact]
    public void Plan_ReplacesTheRootOfALibraryCreatedByAPreviousVersion()
    {
        var destination = Folder("rtve-play");

        var plan = LibraryProvisioner.Plan(
            LibraryProvisioner.DefaultLibraryName,
            destination,
            [TvLibrary("RTVE Play", destination)]);

        Assert.Equal(LibraryProvisionAction.AddPath, plan.Action);
        Assert.Equal(ProgramSections.SectionPaths(destination), plan.Additions);
        Assert.Equal(new[] { destination }, plan.Removals);
        Assert.Contains("carpeta raíz", plan.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Plan_RemovesTheRootWhenTheLibraryAlreadyHasTheSectionFolders()
    {
        var destination = Folder("rtve-play");
        var locations = ProgramSections.SectionPaths(destination).Append(destination).ToArray();

        var plan = LibraryProvisioner.Plan(
            LibraryProvisioner.DefaultLibraryName,
            destination,
            [new LibraryFolder("RTVE Play", true, locations)]);

        Assert.Equal(LibraryProvisionAction.AddPath, plan.Action);
        Assert.Empty(plan.Additions);
        Assert.Equal(new[] { destination }, plan.Removals);
    }

    [Fact]
    public void Plan_DetectsTheLibraryThatAlreadyHasEveryFolder()
    {
        var destination = Folder("rtve-play");

        var plan = LibraryProvisioner.Plan(
            LibraryProvisioner.DefaultLibraryName,
            destination,
            [new LibraryFolder("RTVE Play", true, ProgramSections.SectionPaths(destination))]);

        Assert.Equal(LibraryProvisionAction.AlreadyPresent, plan.Action);
        Assert.Equal("RTVE Play", plan.ExistingLibrary);
        Assert.Empty(plan.Additions);
        Assert.Empty(plan.Removals);
        Assert.Contains("RTVE Play", plan.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Plan_ReusesTheLibraryThatAlreadyIndexesTheRootEvenWithAnotherName()
    {
        var destination = Folder("rtve-play");

        var plan = LibraryProvisioner.Plan(
            LibraryProvisioner.DefaultLibraryName,
            destination,
            [TvLibrary("Series", destination)]);

        Assert.Equal(LibraryProvisionAction.AddPath, plan.Action);
        Assert.Equal("Series", plan.LibraryName);
        Assert.Equal(new[] { destination }, plan.Removals);
    }

    [Fact]
    public void Plan_IgnoresTheTrailingSeparatorOfTheStoredPath()
    {
        var destination = Folder("rtve-play");

        var plan = LibraryProvisioner.Plan(
            LibraryProvisioner.DefaultLibraryName,
            destination + Path.DirectorySeparatorChar,
            [TvLibrary("RTVE Play", destination)],
            organizeSections: false);

        Assert.Equal(LibraryProvisionAction.AlreadyPresent, plan.Action);
    }

    [Fact]
    public void Plan_RejectsAFolderThatBelongsToALibraryOfAnotherType()
    {
        var destination = Folder("rtve-play");

        var plan = LibraryProvisioner.Plan(
            LibraryProvisioner.DefaultLibraryName,
            destination,
            [MoviesLibrary("Películas", destination)]);

        Assert.Equal(LibraryProvisionAction.Conflict, plan.Action);
        Assert.Equal("Películas", plan.ExistingLibrary);
        Assert.Contains("Programas de TV", plan.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Plan_RejectsASectionThatBelongsToALibraryOfAnotherType()
    {
        var destination = Folder("rtve-play");
        var peliculas = Path.Combine(destination, ProgramSections.PeliculasFolder);

        var plan = LibraryProvisioner.Plan(
            LibraryProvisioner.DefaultLibraryName,
            destination,
            [MoviesLibrary("Películas", peliculas)]);

        Assert.Equal(LibraryProvisionAction.Conflict, plan.Action);
        Assert.Contains(peliculas, plan.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Plan_AddsTheFoldersToAnExistingLibraryWithTheSameName()
    {
        var plan = LibraryProvisioner.Plan(
            "Series",
            Folder("otra-carpeta"),
            [TvLibrary("series", Folder("principal"))]);

        Assert.Equal(LibraryProvisionAction.AddPath, plan.Action);
        Assert.Equal("series", plan.LibraryName);
        Assert.Equal(ProgramSections.FolderNames.Count, plan.Additions.Count);
        Assert.Empty(plan.Removals);
    }

    [Fact]
    public void Plan_RejectsANameAlreadyUsedByAnotherType()
    {
        var plan = LibraryProvisioner.Plan(
            "Películas",
            Folder("rtve-play"),
            [MoviesLibrary("Películas", Folder("cine"))]);

        Assert.Equal(LibraryProvisionAction.Conflict, plan.Action);
        Assert.Contains("Programas de TV", plan.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Plan_RequiresANameAndAPath()
    {
        Assert.Throws<ArgumentException>(() => LibraryProvisioner.Plan(" ", "/media/rtve", []));
        Assert.Throws<ArgumentException>(() => LibraryProvisioner.Plan("RTVE Play", "  ", []));
    }

    private static string Folder(params string[] parts)
        => Path.Combine([Root, .. parts]);

    private static LibraryFolder TvLibrary(string name, string path)
        => new(name, true, [path]);

    private static LibraryFolder MoviesLibrary(string name, string path)
        => new(name, false, [path]);
}
