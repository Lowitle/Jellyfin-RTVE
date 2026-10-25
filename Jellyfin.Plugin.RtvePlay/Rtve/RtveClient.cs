using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.RtvePlay.Rtve;

/// <summary>
/// Read-only client for the public RTVE API (<c>www.rtve.es/api</c> and <c>api.rtve.es/api</c>).
/// </summary>
public sealed partial class RtveClient
{
    /// <summary>
    /// Maximum page size accepted by the RTVE API.
    /// </summary>
    public const int PageSize = 100;

    /// <summary>
    /// Maximum number of programs returned when the catalog is filtered by title.
    /// </summary>
    private const int MaxFilteredMatches = 500;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly ILogger<RtveClient> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RtveClient"/> class.
    /// </summary>
    /// <param name="httpClient">HTTP client.</param>
    /// <param name="logger">Logger.</param>
    public RtveClient(HttpClient httpClient, ILogger<RtveClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Resolves the numeric program id from an id, a slug ("saber-vivir") or a program URL.
    /// </summary>
    /// <param name="idOrSlug">Program id, slug or URL.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The numeric program id.</returns>
    public async Task<string> ResolveProgramIdAsync(string idOrSlug, CancellationToken cancellationToken)
    {
        var value = idOrSlug?.Trim() ?? string.Empty;
        if (value.Length == 0)
        {
            throw new ArgumentException("El id del programa no puede estar vacío.", nameof(idOrSlug));
        }

        if (value.All(char.IsAsciiDigit))
        {
            return value;
        }

        var slug = ExtractSlug(value);
        var url = string.Format(CultureInfo.InvariantCulture, "https://www.rtve.es/play/videos/{0}/", slug);
        var html = await GetStringAsync(url, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"No se pudo leer la página del programa '{idOrSlug}'.");

        var match = IdentifierMetaRegex().Match(html);
        if (!match.Success)
        {
            match = IdentifierMetaReversedRegex().Match(html);
        }

        if (!match.Success)
        {
            throw new InvalidOperationException($"No se pudo obtener el id del programa a partir de '{idOrSlug}'.");
        }

        return match.Groups["id"].Value;
    }

    /// <summary>
    /// Gets the program metadata (title, images, plot), or <c>null</c> when it cannot be read.
    /// </summary>
    /// <param name="programId">Program id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The program.</returns>
    public async Task<RtveProgram?> GetProgramAsync(string programId, CancellationToken cancellationToken)
    {
        var url = string.Format(CultureInfo.InvariantCulture, "https://www.rtve.es/api/programas/{0}.json", programId);
        var envelope = await GetJsonAsync<RtveEnvelope<RtveProgram>>(url, cancellationToken).ConfigureAwait(false);
        return envelope?.Page?.Items is { Count: > 0 } items ? items[0] : null;
    }

    /// <summary>
    /// Gets every video of a program, paging through the API (newest first).
    /// </summary>
    /// <param name="programId">Program id.</param>
    /// <param name="maxItems">Maximum number of items (0 means no limit).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The videos.</returns>
    public async Task<IReadOnlyList<RtveVideo>> GetProgramVideosAsync(
        string programId,
        int maxItems,
        CancellationToken cancellationToken)
    {
        var videos = new List<RtveVideo>();
        var page = 1;
        var totalPages = 1;

        while (page <= totalPages)
        {
            var url = string.Format(
                CultureInfo.InvariantCulture,
                "https://www.rtve.es/api/programas/{0}/videos?type=39816&page={1}&size={2}",
                programId,
                page,
                PageSize);

            var envelope = await GetJsonAsync<RtveEnvelope<RtveVideo>>(url, cancellationToken).ConfigureAwait(false);
            if (envelope?.Page is null)
            {
                break;
            }

            totalPages = Math.Max(1, envelope.Page.TotalPages);
            if (envelope.Page.Items is { Count: > 0 } items)
            {
                videos.AddRange(items);
            }

            if (maxItems > 0 && videos.Count >= maxItems)
            {
                break;
            }

            page++;
        }

        if (maxItems > 0 && videos.Count > maxItems)
        {
            videos.RemoveRange(maxItems, videos.Count - maxItems);
        }

        return videos;
    }

    [GeneratedRegex("name=\"DC\\.identifier\"[^>]*content=\"(?<id>\\d+)\"", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex IdentifierMetaRegex();

    [GeneratedRegex("content=\"(?<id>\\d+)\"[^>]*name=\"DC\\.identifier\"", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex IdentifierMetaReversedRegex();

    internal static string ExtractSlug(string value)
    {
        const string Marker = "/play/videos/";
        var marker = value.IndexOf(Marker, StringComparison.OrdinalIgnoreCase);
        var rest = marker >= 0 ? value[(marker + Marker.Length)..] : value;
        var end = rest.IndexOfAny(['/', '?', '#']);
        if (end >= 0)
        {
            rest = rest[..end];
        }

        return rest.Trim().Trim('/');
    }

    internal async Task<T?> GetJsonAsync<T>(string url, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await SendWithRetryAsync(url, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug("RTVE respondió {Status} para {Url}", (int)response.StatusCode, url);
                return default;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Respuesta JSON no válida de {Url}", url);
            return default;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogDebug(ex, "Error de red al leer {Url}", url);
            return default;
        }
    }

    /// <summary>
    /// Sends a GET request, repeating it while RTVE answers with a transient status (throttling,
    /// gateway errors) or the connection fails. The response is returned as is, so the caller decides
    /// what an unsuccessful status means for the resource it asked for.
    /// </summary>
    /// <param name="url">Resource URL.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The response of the last attempt; the caller disposes it.</returns>
    private async Task<HttpResponseMessage> SendWithRetryAsync(string url, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var response = await _httpClient
                    .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                    .ConfigureAwait(false);

                if (!HttpRetryPolicy.IsTransient(response.StatusCode))
                {
                    return response;
                }

                if (attempt >= HttpRetryPolicy.MaxAttempts)
                {
                    _logger.LogWarning(
                        "RTVE sigue respondiendo {Status} para {Url} tras {Attempts} intentos",
                        (int)response.StatusCode,
                        url,
                        attempt);
                    return response;
                }

                _logger.LogInformation(
                    "RTVE respondió {Status} para {Url}; reintento {Attempt}/{Max}",
                    (int)response.StatusCode,
                    url,
                    attempt,
                    HttpRetryPolicy.MaxAttempts);

                response.Dispose();
            }
            catch (HttpRequestException ex) when (attempt < HttpRetryPolicy.MaxAttempts)
            {
                _logger.LogInformation(
                    ex,
                    "Error de red al leer {Url}; reintento {Attempt}/{Max}",
                    url,
                    attempt,
                    HttpRetryPolicy.MaxAttempts);
            }

            await Task.Delay(HttpRetryPolicy.Delay(attempt), cancellationToken).ConfigureAwait(false);
        }
    }
}
