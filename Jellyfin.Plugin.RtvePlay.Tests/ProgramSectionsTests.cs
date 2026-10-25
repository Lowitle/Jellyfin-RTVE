using Jellyfin.Plugin.RtvePlay.Library;
using Xunit;

namespace Jellyfin.Plugin.RtvePlay.Tests;

/// <summary>
/// Tests of the mapping between the kind of program and the subfolder of the library.
/// </summary>
public class ProgramSectionsTests
{
    [Fact]
    public void FolderName_UsesLowerCaseNamesWithoutAccents()
    {
        foreach (var section in Enum.GetValues<ProgramSection>())
        {
            var folder = ProgramSections.FolderName(section);

            Assert.False(string.IsNullOrWhiteSpace(folder));
            Assert.Equal(folder.ToLowerInvariant(), folder);
            Assert.True(folder.All(char.IsAscii));
            Assert.True(folder.All(character => char.IsAsciiLetterOrDigit(character) || character == '-'));
            Assert.Contains(folder, ProgramSections.FolderNames);
        }
    }

    [Fact]
    public void FolderNames_HasOneEntryPerSectionAndNoDuplicates()
    {
        var sections = Enum.GetValues<ProgramSection>().Select(ProgramSections.FolderName).ToArray();

        Assert.Equal(sections.Length, ProgramSections.FolderNames.Count);
        Assert.Equal(sections, ProgramSections.FolderNames);
        Assert.Equal(ProgramSections.FolderNames.Count, ProgramSections.FolderNames.Distinct(StringComparer.Ordinal).Count());
    }

    [Theory]
    [InlineData("Cine", null, ProgramSections.PeliculasFolder)]
    [InlineData("Serie", "Series Prime Time", ProgramSections.SeriesFolder)]
    [InlineData("Serie", "Series de Archivo", ProgramSections.ArchivoFolder)]
    [InlineData("Serie", "SERIES DE ARCHIVO", ProgramSections.ArchivoFolder)]
    [InlineData("Documental", "Serie Documental", ProgramSections.DocumentalesFolder)]
    [InlineData("Magacín", "Magacín semanal", ProgramSections.MagacinesFolder)]
    [InlineData("Informativo", "Informativos Noticias", ProgramSections.NoticiasFolder)]
    [InlineData("Infantil", "Infantiles", ProgramSections.InfantilFolder)]
    [InlineData("Concurso", "Concursos", ProgramSections.ProgramasFolder)]
    [InlineData("Entrevistas", "Entrevistas", ProgramSections.ProgramasFolder)]
    [InlineData("Especial", "Especial Evento", ProgramSections.ProgramasFolder)]
    [InlineData("Entretenimiento", "Late Night", ProgramSections.ProgramasFolder)]
    [InlineData("Música", "Conciertos", ProgramSections.ProgramasFolder)]
    [InlineData("Cocina", "Recetas", ProgramSections.ProgramasFolder)]
    [InlineData("Podcast", "Videopodcast", ProgramSections.PodcastFolder)]
    [InlineData(null, null, ProgramSections.OtrosFolder)]
    [InlineData("Lo que sea", "Tipo nuevo", ProgramSections.OtrosFolder)]
    public void Resolve_MapsTheKindOfProgram(string? kind, string? programType, string expected)
    {
        var section = ProgramSections.Resolve(kind, programType, "Programa");

        Assert.Equal(expected, ProgramSections.FolderName(section));
    }

    [Theory]
    [InlineData("Dragon Ball Z")]
    [InlineData("Pokémon: La serie")]
    [InlineData("Inazuma Eleven GO")]
    [InlineData("Shin-chan")]
    [InlineData("Yu-Gi-Oh! Duel Monsters")]
    public void Resolve_SendsTheAnimeOfTheChildrenCatalogToAnime(string title)
    {
        Assert.Equal(ProgramSection.Anime, ProgramSections.Resolve("Infantil", "Infantiles", title));
    }

    [Theory]
    [InlineData("Clan Clan")]
    [InlineData(null)]
    public void Resolve_KeepsTheOtherChildrenProgramsInInfantil(string? title)
    {
        Assert.Equal(ProgramSection.Infantil, ProgramSections.Resolve("Infantil", "Infantiles", title));
    }

    [Fact]
    public void Resolve_OnlyLooksForAnimeInsideTheChildrenCatalog()
    {
        Assert.Equal(
            ProgramSection.Documentales,
            ProgramSections.Resolve("Documental", "Serie Documental", "Pokémon: el documental"));
    }

    [Fact]
    public void RootFor_AppendsTheFolderOfTheSection()
    {
        var destination = Path.Combine(Path.GetTempPath(), "rtve-play");

        Assert.Equal(
            Path.Combine(destination, ProgramSections.DocumentalesFolder),
            ProgramSections.RootFor(destination, true, ProgramSection.Documentales));
    }

    [Fact]
    public void RootFor_DoesNotAppendTheFolderTwice()
    {
        var destination = Path.Combine(Path.GetTempPath(), "rtve-play");
        var first = ProgramSections.RootFor(destination, true, ProgramSection.Documentales);
        var second = ProgramSections.RootFor(first, true, ProgramSection.Documentales);

        Assert.Equal(first, second);
    }

    [Fact]
    public void RootFor_DoesNotCareAboutTheCaseOfTheFolder()
    {
        var destination = Path.Combine(Path.GetTempPath(), "rtve-play", "Documentales");

        Assert.Equal(destination, ProgramSections.RootFor(destination, true, ProgramSection.Documentales));
    }

    [Fact]
    public void RootFor_KeepsTheDestinationWhenTheSectionsAreDisabled()
    {
        var destination = Path.Combine(Path.GetTempPath(), "rtve-play") + Path.DirectorySeparatorChar;

        Assert.Equal(
            Path.TrimEndingDirectorySeparator(destination),
            ProgramSections.RootFor(destination, false, ProgramSection.Podcast));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void RootFor_RejectsAnEmptyDestination(string destination)
    {
        Assert.Throws<ArgumentException>(() => ProgramSections.RootFor(destination, true, ProgramSection.Otros));
    }

    [Fact]
    public void SectionPaths_ReturnsOneFolderPerSectionInsideTheDestination()
    {
        var destination = Path.Combine(Path.GetTempPath(), "rtve-play");

        var paths = ProgramSections.SectionPaths(destination);

        Assert.Equal(ProgramSections.FolderNames.Count, paths.Count);
        Assert.All(paths, path => Assert.Equal(destination, Path.GetDirectoryName(path)));
        Assert.Equal(
            paths.Select(path => Path.GetFileName(path)),
            ProgramSections.FolderNames);
    }

    [Fact]
    public void SectionPaths_KeepsTheDestinationWithoutTheTrailingSeparator()
    {
        var destination = Path.Combine(Path.GetTempPath(), "rtve-play") + Path.DirectorySeparatorChar;

        var paths = ProgramSections.SectionPaths(destination);

        Assert.All(paths, path => Assert.Equal(
            Path.TrimEndingDirectorySeparator(destination),
            Path.GetDirectoryName(path)));
    }

    [Theory]
    [InlineData(ProgramSections.PeliculasFolder)]
    [InlineData(ProgramSections.SeriesFolder)]
    [InlineData("Documentales")]
    public void IsSectionFolder_DetectsTheFolderOfASection(string folder)
    {
        var path = Path.Combine(Path.GetTempPath(), "rtve-play", folder);

        Assert.True(ProgramSections.IsSectionFolder(path));
        Assert.True(ProgramSections.IsSectionFolder(path + Path.DirectorySeparatorChar));
    }

    [Theory]
    [InlineData("rtve-play")]
    [InlineData("")]
    [InlineData("   ")]
    public void IsSectionFolder_RejectsTheRootAndEmptyPaths(string folder)
    {
        var path = folder.Length == 0
            ? folder
            : Path.Combine(Path.GetTempPath(), folder);

        Assert.False(ProgramSections.IsSectionFolder(path));
    }
}
