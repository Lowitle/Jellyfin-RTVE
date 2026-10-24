using System.Globalization;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.RtvePlay.Rtve;

/// <summary>
/// Media / catalog helpers of the <see cref="RtveClient"/>.
/// </summary>
public sealed partial class RtveClient
{
    /// <summary>
    /// Gets the available subtitle tracks of a video.
    /// </summary>
    /// <param name="videoId">Video id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The subtitle tracks (may be empty).</returns>
    public async Task<IReadOnlyList<RtveSubtitle>> GetSubtitlesAsync(string videoId, CancellationToken cancellationToken)
    {
        var url = string.Format(CultureInfo.InvariantCulture, "https://www.rtve.es/api/videos/{0}/subtitulos", videoId);
        var envelope = await GetJsonAsync<RtveEnvelope<RtveSubtitle>>(url, cancellationToken).ConfigureAwait(false);
        return envelope?.Page?.Items ?? [];
    }

    /// <summary>
    /// Gets a text resource (used for the WebVTT subtitles).
    /// </summary>
    /// <param name="url">Resource URL.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The content, or <c>null</c> when the request fails.</returns>
    public async Task<string?> GetStringAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient
                .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug("RTVE respondió {Status} para {Url}", (int)response.StatusCode, url);
                return null;
            }

            return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogDebug(ex, "Error de red al leer {Url}", url);
            return null;
        }
    }

    /// <summary>
    /// Tells whether a program page belongs to the video catalog. RTVE mixes radio
    /// (<c>/play/audios/</c>) and television (<c>/play/videos/</c>) programs in the same feed and
    /// only the latter have videos to mirror.
    /// </summary>
    /// <param name="htmlUrl">Program page URL.</param>
    /// <returns><c>true</c> when the program is a video program (or the URL is unknown).</returns>
    internal static bool IsVideoProgram(string? htmlUrl)
    {
        if (string.IsNullOrWhiteSpace(htmlUrl))
        {
            return true;
        }

        return htmlUrl.Contains("/play/videos/", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Searches the program catalog by title.
    /// </summary>
    /// <param name="query">Text to search (empty to list the first programs).</param>
    /// <param name="limit">Maximum number of results.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The matching programs.</returns>
    public async Task<IReadOnlyList<RtveProgramSummary>> SearchProgramsAsync(
        string query,
        int limit,
        CancellationToken cancellationToken)
    {
        var results = new List<RtveProgramSummary>();
        var page = 1;
        var totalPages = int.MaxValue;

        while (page <= totalPages && results.Count < limit && page <= 60)
        {
            var url = string.Format(
                CultureInfo.InvariantCulture,
                "https://api.rtve.es/api/programas.json?page={0}&size={1}",
                page,
                PageSize);

            var envelope = await GetJsonAsync<RtveEnvelope<RtveProgram>>(url, cancellationToken).ConfigureAwait(false);
            if (envelope?.Page is null)
            {
                break;
            }

            totalPages = Math.Max(1, envelope.Page.TotalPages);
            foreach (var program in envelope.Page.Items ?? [])
            {
                if (program.Id is null)
                {
                    continue;
                }

                var title = program.Title ?? program.Id;
                if (!IsVideoProgram(program.HtmlUrl))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(query)
                    && title.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                results.Add(new RtveProgramSummary(program.Id, title, program.HtmlUrl, program.PosterImage));
                if (results.Count >= limit)
                {
                    break;
                }
            }

            page++;
        }

        return results;
    }

    /// <summary>
    /// Downloads a resource to disk only when the destination does not exist yet.
    /// </summary>
    /// <param name="url">Resource URL.</param>
    /// <param name="destinationPath">Destination file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> when the file was written.</returns>
    public async Task<bool> DownloadIfMissingAsync(string url, string destinationPath, CancellationToken cancellationToken)
    {
        if (File.Exists(destinationPath))
        {
            return false;
        }

        try
        {
            using var response = await _httpClient
                .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug("RTVE respondió {Status} para {Url}", (int)response.StatusCode, url);
                return false;
            }

            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using var destination = File.Create(destinationPath);
            await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogDebug(ex, "No se pudo descargar {Url}", url);
            return false;
        }
    }
}
