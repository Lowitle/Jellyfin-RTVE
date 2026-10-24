using System.Globalization;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.RtvePlay.Rtve;

/// <summary>
/// Downloads the ZTNR thumbnail PNG of a video and decrypts the playback URLs it contains.
/// </summary>
public sealed class ZtnrResolver
{
    private static readonly string[] Managers = ["rtveplayw", "default"];
    private const string ThumbnailUrlTemplate = "https://www.rtve.es/ztnr/movil/thumbnail/{0}/videos/{1}.png?q=v2";

    private readonly HttpClient _httpClient;
    private readonly ILogger<ZtnrResolver> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ZtnrResolver"/> class.
    /// </summary>
    /// <param name="httpClient">HTTP client used to download the PNG.</param>
    /// <param name="logger">Logger.</param>
    public ZtnrResolver(HttpClient httpClient, ILogger<ZtnrResolver> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Resolves the playback URLs of a video.
    /// </summary>
    /// <param name="videoId">RTVE video id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The resolved streams (may be empty when the video is not available).</returns>
    public async Task<IReadOnlyList<ZtnrStream>> ResolveAsync(string videoId, CancellationToken cancellationToken)
    {
        foreach (var manager in Managers)
        {
            var url = string.Format(CultureInfo.InvariantCulture, ThumbnailUrlTemplate, manager, videoId);
            string png;
            try
            {
                png = await _httpClient.GetStringAsync(url, cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogDebug(ex, "No se pudo descargar el PNG de ZTNR ({Url})", url);
                continue;
            }

            IReadOnlyList<ZtnrStream> streams;
            try
            {
                streams = ZtnrCrypto.Decrypt(png);
            }
            catch (FormatException ex)
            {
                _logger.LogDebug(ex, "El PNG de ZTNR no es base64 válido ({Url})", url);
                continue;
            }

            if (streams.Count > 0)
            {
                return streams;
            }
        }

        _logger.LogDebug("ZTNR no devolvió ninguna URL para el vídeo {VideoId}", videoId);
        return [];
    }

    /// <summary>
    /// Chooses the best stream for Jellyfin: progressive MP4 when available (seekable, no remuxing)
    /// or HLS as a fallback. DASH manifests are ignored because Jellyfin cannot play them from a .strm.
    /// </summary>
    /// <param name="streams">Resolved streams.</param>
    /// <param name="preferMp4">Whether MP4 is preferred over HLS.</param>
    /// <returns>The selected stream, or <c>null</c> when none is playable.</returns>
    public static ZtnrStream? SelectPreferred(IEnumerable<ZtnrStream> streams, bool preferMp4)
    {
        ArgumentNullException.ThrowIfNull(streams);

        var playable = streams
            .Where(s => s.Container is "mp4" or "m3u8")
            .ToList();

        if (playable.Count == 0)
        {
            return null;
        }

        string[] order = preferMp4 ? ["mp4", "m3u8"] : ["m3u8", "mp4"];
        foreach (var container in order)
        {
            var match = playable.FirstOrDefault(
                s => string.Equals(s.Container, container, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                return match;
            }
        }

        return playable[0];
    }
}
