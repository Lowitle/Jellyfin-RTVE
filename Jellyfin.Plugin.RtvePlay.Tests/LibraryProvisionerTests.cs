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
    public void Plan_CreatesTheLibraryEvenWhenTheFolderBelongsToAnotherLibrary()
    {
        var destination = Folder("rtve-play");

        var plan = LibraryProvisioner.Plan(
            LibraryProvisioner.DefaultLibraryName,
            destination,
            [MoviesLibrary("Películas", destination)]);

        // Jellyfin indexes a folder in every library that points at it, so sharing it is not a conflict.
        Assert.Equal(LibraryProvisionAction.Create, plan.Action);
        Assert.Equal(ProgramSections.SectionPaths(destination), plan.Additions);
    }

    [Fact]
    public void Plan_AddsTheSectionsEvenWhenOneOfThemBelongsToAnotherLibrary()
    {
        var destination = Folder("rtve-play");
        var programas = Path.Combine(destination, ProgramSections.ProgramasFolder);

        var plan = LibraryProvisioner.Plan(
            LibraryProvisioner.DefaultLibraryName,
            destination,
            [MoviesLibrary("Programes de Televisió", programas)]);

        Assert.Equal(LibraryProvisionAction.Create, plan.Action);
        Assert.Contains(programas, plan.Additions);
    }

    [Fact]
    public void Plan_LeavesAloneATvLibraryThatOnlyIndexesASection()
    {
        var destination = Folder("rtve-play");
        var programas = Path.Combine(destination, ProgramSections.ProgramasFolder);

        // A TV library that only points at one section is a deliberate setup: the new library is created
        // next to it instead of adding the ten remaining sections to it.
        var plan = LibraryProvisioner.Plan(
            LibraryProvisioner.DefaultLibraryName,
            destination,
            [TvLibrary("Programes de Televisió", programas)]);

        Assert.Equal(LibraryProvisionAction.Create, plan.Action);
        Assert.Equal("RTVE Play", plan.LibraryName);
    }

    [Fact]
    public void Plan_ReusesATvLibraryThatAlreadyIndexesEverySection()
    {
        var destination = Folder("rtve-play");

        var plan = LibraryProvisioner.Plan(
            LibraryProvisioner.DefaultLibraryName,
            destination,
            [new LibraryFolder("Series", true, ProgramSections.SectionPaths(destination), "tvshows")]);

        Assert.Equal(LibraryProvisionAction.AlreadyPresent, plan.Action);
        Assert.Equal("Series", plan.LibraryName);
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

    [Fact]
    public void Describe_ListsTheSectionsAndTheLibrariesThatIndexThem()
    {
        var destination = Folder("rtve-play");
        var programas = Path.Combine(destination, ProgramSections.ProgramasFolder);
        var peliculas = Path.Combine(destination, ProgramSections.PeliculasFolder);

        var matrix = LibraryProvisioner.Describe(
            destination,
            LibraryProvisioner.DefaultLibraryName,
            organizeSections: true,
            [
                new LibraryFolder("RTVE Play", true, ProgramSections.SectionPaths(destination), "tvshows"),
                new LibraryFolder("Programes de Televisió", false, [programas], "movies")
            ]);

        Assert.Equal(ProgramSections.FolderNames.Count, matrix.Folders.Count);
        Assert.True(matrix.LibraryExists);
        Assert.Empty(matrix.Missing);
        Assert.Equal(destination, matrix.Path);

        var programasFolder = matrix.Folders.Single(folder => folder.Section == ProgramSections.ProgramasFolder);
        Assert.True(programasFolder.Covered);
        Assert.Equal(new[] { "RTVE Play", "Programes de Televisió" }, programasFolder.Libraries);
        Assert.Equal(programas, programasFolder.Path);

        var peliculasFolder = matrix.Folders.Single(folder => folder.Section == ProgramSections.PeliculasFolder);
        Assert.Equal(new[] { "RTVE Play" }, peliculasFolder.Libraries);
        Assert.Equal(peliculas, peliculasFolder.Path);

        Assert.Equal("Películas", matrix.Libraries.Single(library => library.Name == "Programes de Televisió").Type);
        Assert.Equal("Programas de TV", matrix.Libraries.Single(library => library.Name == "RTVE Play").Type);
    }

    [Fact]
    public void Describe_ReportsTheSectionsTheLibraryDoesNotIndexYet()
    {
        var destination = Folder("rtve-play");
        var series = Path.Combine(destination, ProgramSections.SeriesFolder);

        var matrix = LibraryProvisioner.Describe(
            destination,
            LibraryProvisioner.DefaultLibraryName,
            organizeSections: true,
            [TvLibrary("RTVE Play", series)]);

        Assert.Equal(ProgramSections.FolderNames.Count - 1, matrix.Missing.Count);
        Assert.DoesNotContain(ProgramSections.SeriesFolder, matrix.Missing);
        Assert.Contains(ProgramSections.PeliculasFolder, matrix.Missing);
    }

    [Fact]
    public void Describe_TreatsALibraryThatStillPointsAtTheRootAsComplete()
    {
        var destination = Folder("rtve-play");

        var matrix = LibraryProvisioner.Describe(
            destination,
            LibraryProvisioner.DefaultLibraryName,
            organizeSections: true,
            [TvLibrary("RTVE Play", destination)]);

        Assert.Empty(matrix.Missing);
        Assert.All(matrix.Folders, folder => Assert.True(folder.Covered));
    }

    [Fact]
    public void Describe_ListsTheSectionsBeforeTheLibraryExists()
    {
        var destination = Folder("rtve-play");

        var matrix = LibraryProvisioner.Describe(
            destination,
            LibraryProvisioner.DefaultLibraryName,
            organizeSections: true,
            [MoviesLibrary("Películas", Folder("cine"))]);

        Assert.False(matrix.LibraryExists);
        Assert.Equal(ProgramSections.FolderNames.Count, matrix.Missing.Count);
        Assert.Equal(new[] { "peliculas", "series-de-tv", "documentales" }, ProgramSections.FolderNames.Take(3));
        Assert.Equal("Películas", matrix.Libraries.Single().Type);
    }

    [Fact]
    public void Describe_ReportsWhetherTheFoldersExist()
    {
        var destination = Path.Combine(Root, "rtve-play-exists");
        var series = Path.Combine(destination, ProgramSections.SeriesFolder);
        Directory.CreateDirectory(series);

        try
        {
            var matrix = LibraryProvisioner.Describe(
                destination,
                LibraryProvisioner.DefaultLibraryName,
                organizeSections: true,
                []);

            Assert.True(matrix.Folders.Single(folder => folder.Section == ProgramSections.SeriesFolder).Exists);
            Assert.False(matrix.Folders.Single(folder => folder.Section == ProgramSections.PeliculasFolder).Exists);
        }
        finally
        {
            Directory.Delete(destination, recursive: true);
        }
    }


    [Fact]
    public void PlanAssociation_AddsTheFolderToTheChosenLibrary()
    {
        var destination = Folder("rtve-play");
        var programas = Path.Combine(destination, ProgramSections.ProgramasFolder);

        var plan = LibraryProvisioner.PlanAssociation(
            programas,
            "Programes de Televisió",
            [MoviesLibrary("Programes de Televisió", Folder("cine")), TvLibrary("RTVE Play", destination)]);

        Assert.Equal(LibraryAssociationAction.Added, plan.Action);
        Assert.Equal("Programes de Televisió", plan.LibraryName);
        Assert.Equal(programas, plan.Folder);
        Assert.Contains("Películas", plan.Message, StringComparison.Ordinal);
        Assert.Contains("no es de tipo «Programas de TV»", plan.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PlanAssociation_ReportsAFolderThatTheLibraryAlreadyIndexes()
    {
        var programas = Folder("rtve-play", ProgramSections.ProgramasFolder);

        var plan = LibraryProvisioner.PlanAssociation(
            programas,
            "Programes de Televisió",
            [MoviesLibrary("Programes de Televisió", programas)]);

        Assert.Equal(LibraryAssociationAction.Existing, plan.Action);
        Assert.Contains("ya estaba asociada", plan.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PlanAssociation_MatchesTheLibraryNameWithoutCase()
    {
        var programas = Folder("rtve-play", ProgramSections.ProgramasFolder);

        var plan = LibraryProvisioner.PlanAssociation(
            programas,
            "programes de televisió",
            [TvLibrary("Programes de Televisió", Folder("cine"))]);

        Assert.Equal(LibraryAssociationAction.Added, plan.Action);
        Assert.Equal("Programes de Televisió", plan.LibraryName);
        Assert.DoesNotContain("no es de tipo", plan.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PlanAssociation_RejectsAnUnknownLibrary()
    {
        var plan = LibraryProvisioner.PlanAssociation(
            Folder("rtve-play", ProgramSections.ProgramasFolder),
            "No existe",
            [TvLibrary("RTVE Play", Folder("rtve-play"))]);

        Assert.Equal(LibraryAssociationAction.Invalid, plan.Action);
        Assert.Contains("no existe", plan.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PlanAssociation_RequiresAFolderAndALibrary()
    {
        var programas = Folder("rtve-play", ProgramSections.ProgramasFolder);

        Assert.Equal(
            LibraryAssociationAction.Invalid,
            LibraryProvisioner.PlanAssociation(" ", "RTVE Play", []).Action);

        Assert.Equal(
            LibraryAssociationAction.Invalid,
            LibraryProvisioner.PlanAssociation(programas, "  ", []).Action);
    }

    private static string Folder(params string[] parts)
        => Path.Combine([Root, .. parts]);

    private static LibraryFolder TvLibrary(string name, params string[] paths)
        => new(name, true, paths, "tvshows");

    private static LibraryFolder MoviesLibrary(string name, params string[] paths)
        => new(name, false, paths, "movies");
}
