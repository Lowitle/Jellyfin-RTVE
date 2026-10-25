using Jellyfin.Plugin.RtvePlay.Configuration;
using Jellyfin.Plugin.RtvePlay.Library;
using Jellyfin.Plugin.RtvePlay.Rtve;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.RtvePlay.Sync;

/// <summary>
/// Episode level helpers.
/// </summary>
public sealed partial class RtveSyncService
{
    private async Task<LibraryEpisode?> BuildEpisodeAsync(
        PluginConfiguration configuration,
        string seriesTitle,
        RtveVideo video,
        int season,
        int episodeNumber,
        CancellationToken cancellationToken)
    {
        var videoId = video.Id!;

        if (configuration.FilterDrm && video.HasDrm)
        {
            _logger.LogDebug("RTVE Play: el vídeo {VideoId} está protegido con DRM, se omite", videoId);
            return null;
        }

        var streams = await _resolver.ResolveAsync(videoId, cancellationToken).ConfigureAwait(false);
        var stream = ZtnrResolver.SelectPreferred(streams, configuration.PreferMp4);
        if (stream is null)
        {
            _logger.LogWarning(
                "RTVE Play: sin URL reproducible para el vídeo {VideoId} ({Title})",
                videoId,
                video.Title);
            return null;
        }

        var genres = (video.Genres ?? [])
            .Select(g => HtmlText.FromHtml(g.Name))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .ToList();

        return new LibraryEpisode
        {
            Season = season,
            Episode = episodeNumber,
            Title = StripSeriesPrefix(FirstNonEmpty(video.Title, video.LongTitle, videoId), seriesTitle),
            Plot = HtmlText.FromHtml(video.ShortDescription) ?? HtmlText.FromHtml(video.Description),
            Url = stream.Url,
            Subtitles = await GetSubtitlesAsync(configuration, videoId, cancellationToken).ConfigureAwait(false),
            Aired = ToDate(video.PublicationDateTimestamp),
            Runtime = video.Duration is > 0 ? TimeSpan.FromMilliseconds(video.Duration.Value) : null,
            ThumbnailUrl = video.Thumbnail,
            VideoId = videoId,
            Genres = genres,
            Director = video.Director
        };
    }

    private async Task SaveArtworkAsync(RtveProgram program, string seriesFolder, CancellationToken cancellationToken)
    {
        var poster = FirstNonEmptyOrNull(program.PosterImage, program.CoverImage);
        if (poster is not null)
        {
            await _client.DownloadIfMissingAsync(
                poster,
                Path.Combine(seriesFolder, "poster.jpg"),
                cancellationToken).ConfigureAwait(false);
        }

        if (!string.IsNullOrWhiteSpace(program.BackgroundImage))
        {
            await _client.DownloadIfMissingAsync(
                program.BackgroundImage,
                Path.Combine(seriesFolder, "fanart.jpg"),
                cancellationToken).ConfigureAwait(false);
        }
    }
}
