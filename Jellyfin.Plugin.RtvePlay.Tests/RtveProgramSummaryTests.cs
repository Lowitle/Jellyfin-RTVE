using Jellyfin.Plugin.RtvePlay.Library;
using Jellyfin.Plugin.RtvePlay.Rtve;
using Xunit;

namespace Jellyfin.Plugin.RtvePlay.Tests;

/// <summary>
/// Tests that the catalog entries exposed by the plugin API carry the data the configuration page
/// needs for the «Tipo» and «Género» columns of the program table, and for the «Solo con vídeos»
/// filter that replaced the hard coded radio filter of the catalog endpoint.
/// </summary>
public class RtveProgramSummaryTests
{
    [Fact]
    public void ToSummary_MapsKindAndGenres()
    {
        var program = new RtveProgram
        {
            Id = "111570",
            Title = "Saber vivir",
            HtmlUrl = "https://www.rtve.es/play/videos/saber-vivir/",
            PosterImage = "https://img.rtve.es/imagenes/saber-vivir/01759842836347.jpg",
            ProgramType = "Magacín semanal",
            ProgramTypeId = "136612",
            Genres =
            [
                new RtveGenre { Name = "Cultura" },
                new RtveGenre { Name = "Humor" }
            ]
        };

        var summary = RtveClient.ToSummary(program);

        Assert.Equal("111570", summary.Id);
        Assert.Equal("Saber vivir", summary.Title);
        Assert.Equal("Magacín", summary.Kind);
        Assert.Equal("Magacín semanal", summary.ProgramType);
        Assert.Equal(["Cultura", "Humor"], summary.Genres!);
        Assert.Equal(ProgramSections.MagacinesFolder, summary.Section);
        Assert.True(summary.HasVideo);
        Assert.EndsWith("01759842836347.jpg", summary.ImageUrl, StringComparison.Ordinal);
    }

    [Fact]
    public void ToSummary_FallsBackToTheTypeNameAndCleansHtml()
    {
        var program = new RtveProgram
        {
            Id = "1837",
            ProgramType = "Serie Documental",
            Genres =
            [
                new RtveGenre { Name = "<b>Cultura</b>" },
                new RtveGenre { Name = "Cultura" },
                new RtveGenre { Name = "  " }
            ]
        };

        var summary = RtveClient.ToSummary(program);

        Assert.Equal("Documental", summary.Kind);
        Assert.Equal("Serie Documental", summary.ProgramType);
        Assert.Equal(["Cultura"], summary.Genres!);
        Assert.Equal(ProgramSections.DocumentalesFolder, summary.Section);
        // Without a title the id is used, so the page never renders an empty row.
        Assert.Equal("1837", summary.Title);
    }

    [Fact]
    public void ToSummary_MapsTheCineContainerToTheMoviesFolder()
    {
        var program = new RtveProgram
        {
            Id = "69830",
            Title = "Cine en el Archivo de RTVE",
            HtmlUrl = "https://www.rtve.es/play/videos/cine-en-el-archivo-de-rtve/",
            ProgramType = "Contenedor Películas",
            ProgramTypeId = "137310"
        };

        var summary = RtveClient.ToSummary(program);

        Assert.Equal("Cine", summary.Kind);
        Assert.Equal("peliculas", summary.Section);
        Assert.True(summary.HasVideo);
    }

    [Fact]
    public void ToSummary_MarksTheRadioProgramsWithoutVideo()
    {
        var program = new RtveProgram
        {
            Id = "151900",
            Title = "No es un día cualquiera",
            HtmlUrl = "https://www.rtve.es/play/audios/no-es-un-dia-cualquiera/",
            ProgramType = "Magacín semanal",
            ProgramTypeId = "136612"
        };

        var summary = RtveClient.ToSummary(program);

        Assert.False(summary.HasVideo);
        Assert.Equal("magacines", summary.Section);
    }

    [Fact]
    public void ToSummary_WithoutType_LeavesKindEmpty()
    {
        var program = new RtveProgram { Id = "42", Title = "Sin tipo" };

        var summary = RtveClient.ToSummary(program);

        Assert.Equal("42", summary.Id);
        Assert.Null(summary.Kind);
        Assert.Null(summary.ProgramType);
        Assert.Empty(summary.Genres!);
        Assert.Equal(ProgramSections.OtrosFolder, summary.Section);
    }

    [Fact]
    public void ToSummary_MapsTheDatesOfTheYearAndLastEmissionColumns()
    {
        // Values copied from https://api.rtve.es/api/programas.json: RTVE formats them as
        // «dd-MM-yyyy HH:mm:ss».
        var program = new RtveProgram
        {
            Id = "1031439",
            Title = "La cena de los idiotas",
            ProductionDate = "01-01-2026 00:00:00",
            PublicationDate = "22-10-2026 10:25:00"
        };

        var summary = RtveClient.ToSummary(program);

        Assert.Equal(2026, summary.FirstYear);
        Assert.Equal("2026-10-22", summary.LastEmission);
    }

    [Fact]
    public void ToSummary_KeepsTheArchiveYear()
    {
        var program = new RtveProgram
        {
            Id = "69830",
            Title = "Cine en el Archivo de RTVE",
            ProductionDate = "28-10-1968 00:00:00",
            PublicationDate = "30-09-2026 08:00:00"
        };

        var summary = RtveClient.ToSummary(program);

        Assert.Equal(1968, summary.FirstYear);
        Assert.Equal("2026-09-30", summary.LastEmission);
    }

    [Fact]
    public void ToSummary_WithoutDates_LeavesTheDateColumnsEmpty()
    {
        var summary = RtveClient.ToSummary(new RtveProgram { Id = "42", Title = "Sin fechas" });

        Assert.Null(summary.FirstYear);
        Assert.Null(summary.LastEmission);
    }

    [Theory]
    [InlineData("01-01-2026 00:00:00", 2026)]
    [InlineData("28-10-1968 00:00:00", 1968)]
    [InlineData("31-12-1999 23:59:59", 1999)]
    [InlineData("1999-12-31", 1999)]
    [InlineData("2026-10-21T09:30:00", 2026)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    [InlineData("sin fecha", null)]
    [InlineData("31-02-2026 00:00:00", null)]
    public void ParseYear_ReadsTheFormatsOfTheApi(string? value, int? expected)
    {
        Assert.Equal(expected, RtveClient.ParseYear(value));
    }

    [Theory]
    [InlineData("21-10-2026 00:00:00", "2026-10-21")]
    [InlineData("28-10-1968 00:00:00", "1968-10-28")]
    [InlineData("2026-10-21", "2026-10-21")]
    [InlineData("2026-10-21T09:30:00", "2026-10-21")]
    [InlineData("", null)]
    [InlineData(null, null)]
    [InlineData("21/10/2026", null)]
    public void ParseIsoDate_NormalizesTheDateToSortItAsText(string? value, string? expected)
    {
        Assert.Equal(expected, RtveClient.ParseIsoDate(value));
    }
}
