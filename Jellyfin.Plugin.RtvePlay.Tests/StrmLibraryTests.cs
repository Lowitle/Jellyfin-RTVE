using Jellyfin.Plugin.RtvePlay.Library;
using Xunit;

namespace Jellyfin.Plugin.RtvePlay.Tests;

/// <summary>
/// Tests of the writer that turns RTVE episodes into a Jellyfin TV show folder.
/// </summary>
public sealed class StrmLibraryTests : IDisposable
{
    private const string Srt = "1\n00:00:01,000 --> 00:00:02,000\nHola\n\n";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "rtve-tests-" + Guid.NewGuid().ToString("N"));

    public StrmLibraryTests()
    {
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public void Write_CreatesTheSeriesFolderWithStrmNfoAndSrt()
    {
        var result = StrmLibrary.Write(
            _root,
            Series("Saber vivir", "saber-vivir", "Divulgación"),
            [Episode(2, 5, "Piloto")],
            new LibraryWriteOptions());

        var series = Path.Combine(_root, "Saber vivir");
        var season = Path.Combine(series, "Season 2");
        var baseName = Path.Combine(season, "Saber vivir - S02E05 - Piloto");

        Assert.Equal(series, result.SeriesPath);
        Assert.Equal(1, result.EpisodesWritten);
        Assert.Equal(4, result.FilesWritten);
        Assert.Equal(0, result.FilesRemoved);

        Assert.True(File.Exists(Path.Combine(series, "tvshow.nfo")));
        Assert.Equal("https://host/video.mp4\n", File.ReadAllText(baseName + ".strm"));
        Assert.True(File.Exists(baseName + ".nfo"));
        Assert.Equal(Srt.Replace("\n", "\r\n", StringComparison.Ordinal), File.ReadAllText(baseName + ".es.srt"));
    }

    [Fact]
    public void Write_IsIdempotent()
    {
        var episodes = new[] { Episode(1, 1, "Uno") };

        var first = StrmLibrary.Write(
            _root,
            Series("Serie", "serie"), episodes, new LibraryWriteOptions());
        var second = StrmLibrary.Write(
            _root,
            Series("Serie", "serie"), episodes, new LibraryWriteOptions());

        Assert.Equal(4, first.FilesWritten);
        Assert.Equal(0, second.FilesWritten);
        Assert.Equal(1, second.EpisodesWritten);
    }

    [Fact]
    public void Write_AddsASuffixWhenTwoEpisodesCollide()
    {
        var result = StrmLibrary.Write(
            _root,
            Series("Serie", "serie"),
            [Episode(1, 1, "Piloto"), Episode(1, 1, "Piloto")],
            new LibraryWriteOptions { WriteNfo = false, WriteSubtitles = false });

        var season = Path.Combine(_root, "Serie", "Season 1");

        Assert.Equal(2, result.FilesWritten);
        Assert.True(File.Exists(Path.Combine(season, "Serie - S01E01 - Piloto.strm")));
        Assert.True(File.Exists(Path.Combine(season, "Serie - S01E01 - Piloto [2].strm")));
    }

    [Fact]
    public void Write_WritesOneSrtPerLanguage()
    {
        var episode = Episode(1, 1, "Uno");
        episode.Subtitles =
        [
            new LibrarySubtitle("es", Srt),
            new LibrarySubtitle("EN", Srt + "2"),
            new LibrarySubtitle("es", "duplicado"),
        ];

        var result = StrmLibrary.Write(
            _root,
            Series("Serie", "serie"),
            [episode],
            new LibraryWriteOptions { WriteNfo = false });

        var season = Path.Combine(_root, "Serie", "Season 1");
        var baseName = Path.Combine(season, "Serie - S01E01 - Uno");

        Assert.Equal(3, result.FilesWritten);
        Assert.Equal(2, Directory.EnumerateFiles(season, "*.srt").Count());
        Assert.Equal(Srt.Replace("\n", "\r\n", StringComparison.Ordinal), File.ReadAllText(baseName + ".es.srt"));
        Assert.Equal((Srt + "2").Replace("\n", "\r\n", StringComparison.Ordinal), File.ReadAllText(baseName + ".en.srt"));
    }

    [Fact]
    public void Write_SkipsEmptySubtitles()
    {
        var episode = Episode(1, 1, "Uno");
        episode.Subtitles = [new LibrarySubtitle("es", "   "), new LibrarySubtitle("en", string.Empty)];

        var result = StrmLibrary.Write(
            _root,
            Series("Serie", "serie"),
            [episode],
            new LibraryWriteOptions { WriteNfo = false });

        var season = Path.Combine(_root, "Serie", "Season 1");

        Assert.Equal(1, result.FilesWritten);
        Assert.Empty(Directory.EnumerateFiles(season, "*.srt"));
    }

    [Fact]
    public void Write_SanitizesTheSubtitleLanguage()
    {
        var episode = Episode(1, 1, "Uno");
        episode.Subtitles = [new LibrarySubtitle("../es-ES", Srt)];

        StrmLibrary.Write(
            _root,
            Series("Serie", "serie"),
            [episode],
            new LibraryWriteOptions { WriteNfo = false });

        var season = Path.Combine(_root, "Serie", "Season 1");

        Assert.Equal(
            ["Serie - S01E01 - Uno.eses.srt"],
            Directory.EnumerateFiles(season, "*.srt").Select(Path.GetFileName));
    }

    [Fact]
    public void Write_HonoursTheToggles()
    {
        var result = StrmLibrary.Write(
            _root,
            Series("Serie", "serie"),
            [Episode(1, 1, "Uno")],
            new LibraryWriteOptions { WriteNfo = false, WriteSubtitles = false });

        var season = Path.Combine(_root, "Serie", "Season 1");

        Assert.Equal(1, result.FilesWritten);
        Assert.Equal(["Serie - S01E01 - Uno.strm"], Directory.EnumerateFiles(season).Select(Path.GetFileName));
    }

    [Fact]
    public void Write_DeletesObsoleteFilesWhenRequested()
    {
        var season = Path.Combine(_root, "Serie", "Season 1");
        Directory.CreateDirectory(season);
        File.WriteAllText(Path.Combine(season, "Serie - S01E09 - Viejo.strm"), "https://host/old.mp4");

        var kept = StrmLibrary.Write(
            _root,
            Series("Serie", "serie"),
            [Episode(1, 1, "Uno")],
            new LibraryWriteOptions { WriteNfo = false, WriteSubtitles = false, DeleteObsolete = true });

        Assert.Equal(1, kept.FilesRemoved);
        Assert.False(File.Exists(Path.Combine(season, "Serie - S01E09 - Viejo.strm")));
        Assert.True(File.Exists(Path.Combine(season, "Serie - S01E01 - Uno.strm")));
    }

    [Fact]
    public void Write_KeepsObsoleteFilesWhenNotRequested()
    {
        var season = Path.Combine(_root, "Serie", "Season 1");
        Directory.CreateDirectory(season);
        var stale = Path.Combine(season, "Serie - S01E09 - Viejo.strm");
        File.WriteAllText(stale, "https://host/old.mp4");

        var result = StrmLibrary.Write(
            _root,
            Series("Serie", "serie"),
            [Episode(1, 1, "Uno")],
            new LibraryWriteOptions { WriteNfo = false, WriteSubtitles = false });

        Assert.Equal(0, result.FilesRemoved);
        Assert.True(File.Exists(stale));
    }

    [Fact]
    public void Write_PutsTheClassificationInTheTvShowNfo()
    {
        var series = Series("Cuéntame cómo pasó", "111570");
        series.Genres = ["Serie", "Cultura"];
        series.Tags = ["Series Prime Time"];
        series.Country = "ES";
        series.ImdbId = "tt0375434";

        StrmLibrary.Write(
            _root,
            series,
            [Episode(1, 1, "Uno")],
            new LibraryWriteOptions { WriteSubtitles = false });

        var nfo = File.ReadAllText(Path.Combine(_root, "Cuéntame cómo pasó", "tvshow.nfo"));

        Assert.Contains("<genre>Serie</genre>", nfo, StringComparison.Ordinal);
        Assert.Contains("<genre>Cultura</genre>", nfo, StringComparison.Ordinal);
        Assert.Contains("<tag>Series Prime Time</tag>", nfo, StringComparison.Ordinal);
        Assert.Contains("<tag>RTVE Play</tag>", nfo, StringComparison.Ordinal);
        Assert.Contains("<country>ES</country>", nfo, StringComparison.Ordinal);
        Assert.Contains("<uniqueid type=\"imdb\">tt0375434</uniqueid>", nfo, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static LibrarySeries Series(string title, string programId, string? plot = null)
        => new()
        {
            Title = title,
            ProgramId = programId,
            Plot = plot,
        };

    private static LibraryEpisode Episode(int season, int episode, string title)
        => new()
        {
            Season = season,
            Episode = episode,
            Title = title,
            Url = "https://host/video.mp4",
            Subtitles = [new LibrarySubtitle("es", Srt)],
        };
}
