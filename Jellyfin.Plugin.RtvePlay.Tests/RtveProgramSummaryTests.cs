using Jellyfin.Plugin.RtvePlay.Rtve;
using Xunit;

namespace Jellyfin.Plugin.RtvePlay.Tests;

/// <summary>
/// Tests that the catalog entries exposed by the plugin API carry the data the configuration page
/// needs for the «Tipo» and «Género» columns of the program table.
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
        // Without a title the id is used, so the page never renders an empty row.
        Assert.Equal("1837", summary.Title);
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
    }
}
