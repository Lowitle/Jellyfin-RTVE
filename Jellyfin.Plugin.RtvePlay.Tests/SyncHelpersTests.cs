using Jellyfin.Plugin.RtvePlay.Rtve;
using Jellyfin.Plugin.RtvePlay.Sync;
using Xunit;

namespace Jellyfin.Plugin.RtvePlay.Tests;

/// <summary>
/// Tests of the helpers shared by the program and episode pipelines.
/// </summary>
public class SyncHelpersTests
{
    [Theory]
    [InlineData("es", "es")]
    [InlineData("ES", "es")]
    [InlineData(" en ", "en")]
    [InlineData("ca", "ca")]
    [InlineData("gl", "gl")]
    [InlineData("eu", "eu")]
    public void NormalizeLanguage_KeepsTheIsoCode(string configured, string expected)
    {
        Assert.Equal(expected, RtveSyncService.NormalizeLanguage(configured));
    }

    [Theory]
    [InlineData("none")]
    [InlineData("NONE")]
    [InlineData("ninguno")]
    [InlineData("disabled")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void NormalizeLanguage_ReturnsNullWhenSubtitlesAreDisabled(string? configured)
    {
        Assert.Null(RtveSyncService.NormalizeLanguage(configured));
    }

    [Fact]
    public void NormalizeLanguages_KeepsTheSelectedLanguagesInOrder()
    {
        Assert.Equal(["es", "en"], RtveSyncService.NormalizeLanguages(["ES", " en ", "es", ""]));
    }

    [Fact]
    public void NormalizeLanguages_IsEmptyWhenNothingIsSelected()
    {
        Assert.Empty(RtveSyncService.NormalizeLanguages([]));
        Assert.Empty(RtveSyncService.NormalizeLanguages(null));
        Assert.Empty(RtveSyncService.NormalizeLanguages(["none"]));
    }

    [Fact]
    public void NormalizeLanguages_MigratesTheLegacySingleLanguage()
    {
        Assert.Equal(["ca"], RtveSyncService.NormalizeLanguages([], "ca"));
        Assert.Equal(["ca"], RtveSyncService.NormalizeLanguages(null, " CA "));
        Assert.Empty(RtveSyncService.NormalizeLanguages([], "none"));
        Assert.Equal(["gl"], RtveSyncService.NormalizeLanguages(["gl"], "ca"));
    }

    [Theory]
    [InlineData("tt0375434", "tt0375434")]
    [InlineData(" TT0375434 ", "tt0375434")]
    [InlineData("tt8739752", "tt8739752")]
    public void NormalizeImdbId_KeepsWellFormedIds(string? value, string expected)
    {
        Assert.Equal(expected, RtveSyncService.NormalizeImdbId(value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("0")]
    [InlineData("tt")]
    [InlineData("0375434")]
    [InlineData("tt-375434")]
    [InlineData("tt0375434 | tt0375435")]
    public void NormalizeImdbId_DropsAnythingElse(string? value)
    {
        Assert.Null(RtveSyncService.NormalizeImdbId(value));
    }

    [Fact]
    public void SelectSubtitleTracks_MatchesEveryRequestedLanguage()
    {
        RtveSubtitle[] tracks =
        [
            new() { Language = "es", Source = "https://host/es.vtt" },
            new() { Language = "en", Source = "https://host/en.vtt" },
            new() { Language = "ca", Source = null },
        ];

        var selection = RtveSyncService.SelectSubtitleTracks(tracks, ["es", "ca"], allLanguages: false);

        Assert.Equal(["es"], selection.Select(item => item.Language));
        Assert.Equal("https://host/es.vtt", selection[0].Source);
    }

    [Fact]
    public void SelectSubtitleTracks_HandlesRegionalTags()
    {
        RtveSubtitle[] tracks = [new() { Language = "es-ES", Source = "https://host/es.vtt" }];

        var selection = RtveSyncService.SelectSubtitleTracks(tracks, ["es"], allLanguages: false);

        Assert.Equal("https://host/es.vtt", Assert.Single(selection).Source);
    }

    [Fact]
    public void SelectSubtitleTracks_DownloadsEveryTrackWhenAllLanguagesIsEnabled()
    {
        RtveSubtitle[] tracks =
        [
            new() { Language = "es-ES", Source = "https://host/es.vtt" },
            new() { Language = "es", Source = "https://host/es2.vtt" },
            new() { Language = "en", Source = "https://host/en.vtt" },
            new() { Language = null, Source = "https://host/und.vtt" },
            new() { Language = "gl", Source = null },
        ];

        var selection = RtveSyncService.SelectSubtitleTracks(tracks, [], allLanguages: true);

        Assert.Equal(["es", "en", "und"], selection.Select(item => item.Language));
    }

    [Fact]
    public void SelectSubtitleTracks_FallsBackToTheOnlyTrackForASingleLanguage()
    {
        RtveSubtitle[] tracks = [new() { Language = "spa", Source = "https://host/spa.vtt" }];

        var selection = RtveSyncService.SelectSubtitleTracks(tracks, ["es"], allLanguages: false);

        var item = Assert.Single(selection);
        Assert.Equal("es", item.Language);
        Assert.Equal("https://host/spa.vtt", item.Source);
    }

    [Fact]
    public void SelectSubtitleTracks_ReturnsNothingWithoutUsableTracks()
    {
        RtveSubtitle[] tracks = [new() { Language = "es", Source = "https://host/es.vtt" }];

        Assert.Empty(RtveSyncService.SelectSubtitleTracks(tracks, [], allLanguages: false));
        Assert.Empty(RtveSyncService.SelectSubtitleTracks([], ["es"], allLanguages: true));
        Assert.Empty(RtveSyncService.SelectSubtitleTracks([new() { Language = "es" }], ["es"], allLanguages: true));
    }
}
