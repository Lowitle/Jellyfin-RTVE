using Jellyfin.Plugin.RtvePlay.Configuration;
using Jellyfin.Plugin.RtvePlay.Library;
using Jellyfin.Plugin.RtvePlay.Rtve;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.RtvePlay.Sync;

/// <summary>
/// Subtitle pipeline: picks the RTVE tracks requested in the configuration and turns them into SRT.
/// </summary>
public sealed partial class RtveSyncService
{
    private async Task<IReadOnlyList<LibrarySubtitle>> GetSubtitlesAsync(
        PluginConfiguration configuration,
        string videoId,
        CancellationToken cancellationToken)
    {
        if (!configuration.WriteSubtitles)
        {
            return [];
        }

        var languages = NormalizeLanguages(configuration.SubtitleLanguages, configuration.SubtitleLanguage);
        if (!configuration.DownloadAllSubtitleLanguages && languages.Count == 0)
        {
            _logger.LogDebug(
                "RTVE Play: la descarga de subtítulos está activa pero no hay idiomas marcados; "
                + "no se escribe ningún .srt del vídeo {VideoId} (marca al menos un idioma en la configuración)",
                videoId);
            return [];
        }

        var tracks = await _client.GetSubtitlesAsync(videoId, cancellationToken).ConfigureAwait(false);
        if (tracks.Count == 0)
        {
            return [];
        }

        var selection = SelectSubtitleTracks(tracks, languages, configuration.DownloadAllSubtitleLanguages);
        if (selection.Count == 0)
        {
            _logger.LogInformation(
                "RTVE Play: el vídeo {VideoId} publica subtítulos en {Published} pero ninguno coincide con los idiomas configurados ({Requested})",
                videoId,
                string.Join(", ", tracks.Select(track => track.Language).Where(lang => !string.IsNullOrWhiteSpace(lang))),
                string.Join(", ", languages));
            return [];
        }

        var subtitles = new List<LibrarySubtitle>(selection.Count);

        foreach (var track in selection)
        {
            var vtt = await _client.GetStringAsync(track.Source, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(vtt))
            {
                // The download swallows the HTTP status, so the URL is logged to tell a gone track
                // from a throttled request.
                _logger.LogWarning(
                    "RTVE Play: no se pudo descargar el subtítulo {Language} del vídeo {VideoId} ({Url}); "
                    + "se reintentará en la próxima sincronización",
                    track.Language,
                    videoId,
                    track.Source);
                continue;
            }

            string srt;
            try
            {
                srt = VttToSrt.Convert(vtt);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(
                    ex,
                    "RTVE Play: no se pudo convertir el subtítulo {Language} del vídeo {VideoId}",
                    track.Language,
                    videoId);
                continue;
            }

            if (srt.Length > 0)
            {
                subtitles.Add(new LibrarySubtitle(track.Language, srt));
            }
        }

        return subtitles;
    }

    /// <summary>
    /// Pairs the requested languages with the subtitle tracks published by RTVE.
    /// </summary>
    /// <param name="tracks">Subtitle tracks of the video.</param>
    /// <param name="languages">Requested languages (already normalized).</param>
    /// <param name="allLanguages">When <c>true</c> every published track is selected.</param>
    /// <returns>The tracks to download, at most one per language.</returns>
    internal static IReadOnlyList<SubtitleSource> SelectSubtitleTracks(
        IReadOnlyList<RtveSubtitle> tracks,
        IReadOnlyList<string> languages,
        bool allLanguages)
    {
        var usable = tracks.Where(t => !string.IsNullOrWhiteSpace(t.Source)).ToList();
        if (usable.Count == 0)
        {
            return [];
        }

        var selection = new List<SubtitleSource>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (allLanguages)
        {
            foreach (var track in usable)
            {
                var language = TrackLanguage(track.Language) ?? "und";
                if (seen.Add(language))
                {
                    selection.Add(new SubtitleSource(language, track.Source!));
                }
            }

            return selection;
        }

        foreach (var language in languages)
        {
            var track = usable.FirstOrDefault(
                t => string.Equals(TrackLanguage(t.Language), language, StringComparison.OrdinalIgnoreCase));

            // Legacy behaviour: when a single language was configured and RTVE publishes a single
            // track under a different (or missing) code, keep that track with the requested name.
            track ??= usable.Count == 1 && languages.Count == 1 ? usable[0] : null;

            if (track is null || !seen.Add(language))
            {
                continue;
            }

            selection.Add(new SubtitleSource(language, track.Source!));
        }

        return selection;
    }

    /// <summary>
    /// Reduces an RTVE language tag ("es", "es-ES", "ES") to its primary subtag.
    /// </summary>
    /// <param name="value">Language tag.</param>
    /// <returns>The primary subtag, or <c>null</c> when the tag is missing.</returns>
    private static string? TrackLanguage(string? value)
    {
        var language = NormalizeLanguage(value);
        if (language is null)
        {
            return null;
        }

        var separator = language.IndexOf('-', StringComparison.Ordinal);
        return separator > 0 ? language[..separator] : language;
    }
}

/// <summary>
/// A subtitle track selected for download.
/// </summary>
/// <param name="Language">ISO language code used in the SRT file name.</param>
/// <param name="Source">WebVTT URL.</param>
public sealed record SubtitleSource(string Language, string Source);
