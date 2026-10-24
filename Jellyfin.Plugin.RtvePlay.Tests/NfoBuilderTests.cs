using System.Xml.Linq;
using Jellyfin.Plugin.RtvePlay.Library;
using Xunit;

namespace Jellyfin.Plugin.RtvePlay.Tests;

/// <summary>
/// Tests of the NFO documents read by Jellyfin.
/// </summary>
public class NfoBuilderTests
{
    [Fact]
    public void BuildSeries_WritesTheTvShowFields()
    {
        var document = XDocument.Parse(NfoBuilder.BuildSeries("Saber vivir", "saber-vivir-123", "<b>Divulgación</b> & más"));
        var root = document.Root!;

        Assert.Equal("tvshow", root.Name.LocalName);
        Assert.Equal("Saber vivir", root.Element("title")!.Value);
        Assert.Equal("saber-vivir-123", root.Element("uniqueid")!.Value);
        Assert.Equal("rtve", root.Element("uniqueid")!.Attribute("type")!.Value);
        Assert.Equal("true", root.Element("uniqueid")!.Attribute("default")!.Value);
        Assert.Equal("<b>Divulgación</b> & más", root.Element("plot")!.Value);
        Assert.Equal("RTVE", root.Element("studio")!.Value);
        Assert.Contains("&lt;b&gt;Divulgación&lt;/b&gt; &amp; más", document.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void BuildSeries_UsesADefaultPlot()
    {
        var root = XDocument.Parse(NfoBuilder.BuildSeries("Saber vivir", "id", "  ")).Root!;

        Assert.Equal("Programas de Saber vivir en RTVE Play.", root.Element("plot")!.Value);
    }

    [Fact]
    public void BuildEpisode_WritesEveryAvailableField()
    {
        var episode = new LibraryEpisode
        {
            Season = 2,
            Episode = 5,
            Title = "Piloto",
            Plot = "Una trama",
            Aired = new DateTimeOffset(2024, 10, 1, 22, 30, 0, TimeSpan.FromHours(2)),
            Runtime = TimeSpan.FromMinutes(47.6),
            VideoId = "16909076",
            ThumbnailUrl = "https://www.rtve.es/api/thumb.png",
            Genres = ["Divulgación", " ", "Ciencia"],
            Director = "Ana Pérez",
        };

        var root = XDocument.Parse(NfoBuilder.BuildEpisode("Saber vivir", episode)).Root!;

        Assert.Equal("episodedetails", root.Name.LocalName);
        Assert.Equal("Piloto", root.Element("title")!.Value);
        Assert.Equal("Saber vivir", root.Element("showtitle")!.Value);
        Assert.Equal("2", root.Element("season")!.Value);
        Assert.Equal("5", root.Element("episode")!.Value);
        Assert.Equal("Una trama", root.Element("plot")!.Value);
        Assert.Equal("2024-10-01", root.Element("aired")!.Value);
        Assert.Equal("48", root.Element("runtime")!.Value);
        Assert.Equal("16909076", root.Element("uniqueid")!.Value);
        Assert.Equal("https://www.rtve.es/api/thumb.png", root.Element("thumb")!.Value);
        Assert.Equal(["Divulgación", "Ciencia"], root.Elements("genre").Select(g => g.Value));
        Assert.Equal("Ana Pérez", root.Element("director")!.Value);
    }

    [Fact]
    public void BuildEpisode_OmitsTheOptionalFields()
    {
        var root = XDocument.Parse(NfoBuilder.BuildEpisode(
            "Saber vivir",
            new LibraryEpisode { Season = 1, Episode = 1, Title = "01/10/24" })).Root!;

        Assert.Null(root.Element("plot"));
        Assert.Null(root.Element("aired"));
        Assert.Null(root.Element("runtime"));
        Assert.Null(root.Element("uniqueid"));
        Assert.Null(root.Element("thumb"));
        Assert.Null(root.Element("genre"));
        Assert.Null(root.Element("director"));
    }

    [Fact]
    public void BuildEpisode_IgnoresRuntimesUnderOneMinute()
    {
        var root = XDocument.Parse(NfoBuilder.BuildEpisode(
            "Saber vivir",
            new LibraryEpisode { Title = "x", Runtime = TimeSpan.FromSeconds(30) })).Root!;

        Assert.Null(root.Element("runtime"));
    }

    [Fact]
    public void Documents_StartWithAnXmlDeclaration()
    {
        Assert.StartsWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>", NfoBuilder.BuildSeries("x", "y", null), StringComparison.Ordinal);
    }
}
