using Jellyfin.Plugin.RtvePlay.Rtve;
using Xunit;

namespace Jellyfin.Plugin.RtvePlay.Tests;

/// <summary>
/// Tests of the HTML to plain text helper and of the stream selection.
/// </summary>
public class RtveHelpersTests
{
    [Theory]
    [InlineData("<p>Hola&nbsp;mundo</p> <b>otra</b>", "Hola mundo otra")]
    [InlineData("Texto   con\n\nsaltos", "Texto con saltos")]
    [InlineData("  <br/>  ", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void FromHtml_ReturnsPlainText(string? html, string? expected)
    {
        Assert.Equal(expected, HtmlText.FromHtml(html));
    }

    [Fact]
    public void FromHtml_DecodesEntities()
    {
        Assert.Equal("Texto & más", HtmlText.FromHtml("<p>Texto &amp; m&aacute;s</p>"));
    }

    [Theory]
    [InlineData("https://www.rtve.es/play/videos/saber-vivir/", true)]
    [InlineData("https://www.rtve.es/play/videos/saber-vivir", true)]
    [InlineData("https://www.rtve.es/play/clan/dragon-ball-daima/", true)]
    [InlineData("https://www.rtve.es/play/audios/y-tu-sin-saberlo/", false)]
    [InlineData("https://www.rtve.es/play/audios/no-necesitaba-saber-esto/", false)]
    [InlineData("", true)]
    [InlineData(null, true)]
    public void HasVideos_SkipsTheRadioCatalog(string? htmlUrl, bool expected)
    {
        Assert.Equal(expected, RtveClient.HasVideos(htmlUrl));
    }

    [Fact]
    public void SelectPreferred_PrefersMp4()
    {
        ZtnrStream[] streams =
        [
            new(string.Empty, "https://host/video.m3u8"),
            new(string.Empty, "https://host/video.mpd"),
            new(string.Empty, "https://host/video.mp4"),
        ];

        Assert.Equal("https://host/video.mp4", ZtnrResolver.SelectPreferred(streams, preferMp4: true)!.Url);
        Assert.Equal("https://host/video.m3u8", ZtnrResolver.SelectPreferred(streams, preferMp4: false)!.Url);
    }

    [Fact]
    public void SelectPreferred_FallsBackToTheOnlyPlayableStream()
    {
        ZtnrStream[] hlsOnly = [new(string.Empty, "https://host/video.m3u8")];

        Assert.Equal("https://host/video.m3u8", ZtnrResolver.SelectPreferred(hlsOnly, preferMp4: true)!.Url);
    }

    [Fact]
    public void SelectPreferred_IgnoresDashManifests()
    {
        ZtnrStream[] dashOnly = [new(string.Empty, "https://host/video.mpd")];

        Assert.Null(ZtnrResolver.SelectPreferred(dashOnly, preferMp4: true));
        Assert.Null(ZtnrResolver.SelectPreferred([], preferMp4: true));
    }
}
