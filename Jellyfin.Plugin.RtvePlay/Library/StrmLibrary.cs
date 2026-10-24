using System.Globalization;
using System.Text;

namespace Jellyfin.Plugin.RtvePlay.Library;

/// <summary>
/// Writes a series folder (.strm + .nfo + .srt) that Jellyfin can import as a TV show.
/// </summary>
public static class StrmLibrary
{
    private static readonly UTF8Encoding Utf8NoBom = new(false);

    /// <summary>
    /// Writes the episodes of a series into <paramref name="rootPath"/>.
    /// </summary>
    /// <param name="rootPath">Library root folder.</param>
    /// <param name="seriesTitle">Series title.</param>
    /// <param name="programId">RTVE program id.</param>
    /// <param name="seriesPlot">Series plot.</param>
    /// <param name="episodes">Episodes to write.</param>
    /// <param name="options">Write options.</param>
    /// <returns>The result of the operation.</returns>
    public static LibraryWriteResult Write(
        string rootPath,
        string seriesTitle,
        string programId,
        string? seriesPlot,
        IEnumerable<LibraryEpisode> episodes,
        LibraryWriteOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(seriesTitle);
        ArgumentNullException.ThrowIfNull(episodes);
        ArgumentNullException.ThrowIfNull(options);

        var seriesFolder = Path.Combine(rootPath, LibraryNaming.SeriesFolderName(seriesTitle));
        Directory.CreateDirectory(seriesFolder);

        var written = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var filesWritten = 0;
        var count = 0;

        if (options.WriteNfo)
        {
            filesWritten += WriteText(
                Path.Combine(seriesFolder, "tvshow.nfo"),
                NfoBuilder.BuildSeries(seriesTitle, programId, seriesPlot),
                written) ? 1 : 0;
        }

        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var episode in episodes)
        {
            var seasonFolder = Path.Combine(
                seriesFolder,
                string.Format(CultureInfo.InvariantCulture, "Season {0}", episode.Season));
            Directory.CreateDirectory(seasonFolder);

            var baseName = Unique(
                LibraryNaming.EpisodeFileName(seriesTitle, episode.Season, episode.Episode, episode.Title),
                usedNames);

            var strmPath = Path.Combine(seasonFolder, baseName + ".strm");
            filesWritten += WriteText(strmPath, episode.Url + "\n", written) ? 1 : 0;

            if (options.WriteNfo)
            {
                filesWritten += WriteText(
                    Path.Combine(seasonFolder, baseName + ".nfo"),
                    NfoBuilder.BuildEpisode(seriesTitle, episode),
                    written) ? 1 : 0;
            }

            if (options.WriteSubtitles && !string.IsNullOrWhiteSpace(episode.SubtitleSrt))
            {
                var language = string.IsNullOrWhiteSpace(episode.SubtitleLanguage) ? "es" : episode.SubtitleLanguage;
                var srtPath = Path.Combine(seasonFolder, $"{baseName}.{language}.srt");
                filesWritten += WriteText(srtPath, ToCrlf(episode.SubtitleSrt), written) ? 1 : 0;
            }

            count++;
        }

        var removed = options.DeleteObsolete ? DeleteObsolete(seriesFolder, written) : 0;

        return new LibraryWriteResult(seriesFolder, count, filesWritten, removed);
    }

    private static string Unique(string baseName, HashSet<string> used)
    {
        if (used.Add(baseName))
        {
            return baseName;
        }

        for (var suffix = 2; ; suffix++)
        {
            var candidate = string.Format(CultureInfo.InvariantCulture, "{0} [{1}]", baseName, suffix);
            if (used.Add(candidate))
            {
                return candidate;
            }
        }
    }

    private static bool WriteText(string path, string content, HashSet<string> written)
    {
        written.Add(path);

        if (File.Exists(path) && string.Equals(File.ReadAllText(path), content, StringComparison.Ordinal))
        {
            return false;
        }

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, content, Utf8NoBom);
        return true;
    }

    private static string ToCrlf(string value)
        => value.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\n", "\r\n", StringComparison.Ordinal);

    private static int DeleteObsolete(string seriesFolder, HashSet<string> written)
    {
        var removed = 0;

        foreach (var file in Directory.EnumerateFiles(seriesFolder, "*", SearchOption.AllDirectories))
        {
            var extension = Path.GetExtension(file);
            if (extension is not (".strm" or ".nfo" or ".srt"))
            {
                continue;
            }

            if (string.Equals(Path.GetFileName(file), "tvshow.nfo", StringComparison.OrdinalIgnoreCase)
                || written.Contains(file))
            {
                continue;
            }

            File.Delete(file);
            removed++;
        }

        foreach (var directory in Directory.EnumerateDirectories(seriesFolder))
        {
            if (!Directory.EnumerateFileSystemEntries(directory).Any())
            {
                Directory.Delete(directory);
            }
        }

        return removed;
    }
}
