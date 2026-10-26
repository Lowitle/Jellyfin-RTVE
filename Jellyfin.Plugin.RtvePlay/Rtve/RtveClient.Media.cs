using System.Globalization;
using Jellyfin.Plugin.RtvePlay.Library;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.RtvePlay.Rtve;

/// <summary>
/// Media / catalog helpers of the <see cref="RtveClient"/>.
/// </summary>
public sealed partial class RtveClient
{
    /// <summary>
    /// Catalog pages requested at the same time when the whole catalog is read. RTVE answers a page
    /// of 100 programs in a few hundred milliseconds, so four requests keep the load under a couple of
    /// seconds without hammering the API.
    /// </summary>
    private const int CatalogPageConcurrency = 4;

    /// <summary>
    /// Date formats used by the RTVE API. <c>productionDate</c> and <c>publicationDate</c> arrive as
    /// <c>"21-10-2026 00:00:00"</c>, but the ISO variants are accepted as well in case RTVE changes
    /// the format of a program.
    /// </summary>
    private static readonly string[] RtveDateFormats =
    [
        "dd-MM-yyyy HH:mm:ss",
        "dd-MM-yyyy HH:mm",
        "dd-MM-yyyy",
        "yyyy-MM-dd HH:mm:ss",
        "yyyy-MM-dd HH:mm",
        "yyyy-MM-dd",
        "yyyy-MM-ddTHH:mm:ss"
    ];

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
            using var response = await SendWithRetryAsync(url, cancellationToken).ConfigureAwait(false);

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
    /// (<c>/play/audios/</c>) and television in the same feed and only the television programs have
    /// videos to mirror; the children's catalog (<c>/play/clan/</c>) is television as well.
    /// </summary>
    /// <param name="htmlUrl">Program page URL.</param>
    /// <returns><c>false</c> only for the radio programs.</returns>
    internal static bool HasVideos(string? htmlUrl)
    {
        if (string.IsNullOrWhiteSpace(htmlUrl))
        {
            return true;
        }

        return !htmlUrl.Contains("/play/audios/", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Projects a program read from the RTVE API into the entry exposed by the plugin API, so the
    /// configuration page can show the section, the kind of content and the genres of every program
    /// without a request per program.
    /// </summary>
    /// <param name="program">Program read from the RTVE API.</param>
    /// <returns>The catalog entry.</returns>
    internal static RtveProgramSummary ToSummary(RtveProgram program)
    {
        var id = program.Id ?? string.Empty;
        var title = program.Title ?? id;

        var genres = (program.Genres ?? [])
            .Select(genre => HtmlText.FromHtml(genre.Name))
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var kind = ProgramClassifier.ResolveKind(program.ProgramTypeId, program.ProgramType);

        return new RtveProgramSummary(
            id,
            title,
            program.HtmlUrl,
            program.PosterImage,
            kind,
            HtmlText.FromHtml(program.ProgramType),
            genres,
            // The same resolution the sync uses to pick the subfolder of the library, so the «Tipo»
            // filter of the page and the folder names of the destination never disagree.
            ProgramSections.FolderName(ProgramSections.Resolve(kind, program.ProgramType, title)),
            HasVideos(program.HtmlUrl),
            ParseYear(program.ProductionDate),
            ParseIsoDate(program.PublicationDate));
    }

    /// <summary>
    /// Reads the year of a date published by RTVE («01-01-2026 00:00:00»).
    /// </summary>
    /// <param name="value">Date as published by RTVE.</param>
    /// <returns>The year, or <c>null</c> when RTVE publishes no usable date.</returns>
    internal static int? ParseYear(string? value)
    {
        return TryParseRtveDate(value, out var date) ? date.Year : null;
    }

    /// <summary>
    /// Normalizes a date published by RTVE to <c>yyyy-MM-dd</c>, the format that sorts
    /// chronologically as plain text.
    /// </summary>
    /// <param name="value">Date as published by RTVE.</param>
    /// <returns>The date, or <c>null</c> when RTVE publishes no usable date.</returns>
    internal static string? ParseIsoDate(string? value)
    {
        return TryParseRtveDate(value, out var date)
            ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : null;
    }

    private static bool TryParseRtveDate(string? value, out DateTime date)
    {
        date = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return DateTime.TryParseExact(
            value.Trim(),
            RtveDateFormats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out date);
    }

    /// <summary>
    /// Searches the program catalog by title.
    /// </summary>
    /// <param name="query">Text to search (empty to list the first programs).</param>
    /// <param name="limit">Maximum number of results.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The matching programs.</returns>
    /// <remarks>
    /// The radio programs of the catalog are included as well, so a search never hides a program that
    /// the table is able to list; the configuration page marks them and hides them by default.
    /// </remarks>
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
                if (!string.IsNullOrWhiteSpace(query)
                    && title.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                results.Add(ToSummary(program));
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
    /// Lists the programs of the RTVE catalog, one page at a time.
    /// </summary>
    /// <param name="query">Title filter (empty to list the whole catalog).</param>
    /// <param name="page">Page number (1 based).</param>
    /// <param name="size">Page size (1-100).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The page of programs.</returns>
    /// <remarks>
    /// Every entry RTVE returns is kept, radio included, so a page always holds the same number of
    /// programs and paging stays stable. Hiding the programs without videos is up to the caller,
    /// which has <see cref="RtveProgramSummary.HasVideo"/> for that.
    /// </remarks>
    public async Task<RtveProgramPage> ListProgramsAsync(
        string? query,
        int page,
        int size,
        CancellationToken cancellationToken)
    {
        var currentPage = Math.Max(1, page);
        var pageSize = Math.Clamp(size, 1, PageSize);
        var filter = query?.Trim() ?? string.Empty;

        if (filter.Length > 0)
        {
            // The catalog endpoint cannot filter by title server side, so the search helper is used
            // and the requested slice is cut out of its results.
            var wanted = Math.Min(currentPage * pageSize, MaxFilteredMatches);
            var found = await SearchProgramsAsync(filter, wanted, cancellationToken).ConfigureAwait(false);
            var items = found.Skip((currentPage - 1) * pageSize).Take(pageSize).ToList();
            var hasMore = found.Count >= wanted && wanted < MaxFilteredMatches;

            return new RtveProgramPage(
                items,
                currentPage,
                hasMore ? currentPage + 1 : currentPage,
                found.Count,
                hasMore);
        }

        var url = CatalogPageUrl(currentPage, pageSize);

        var envelope = await GetJsonAsync<RtveEnvelope<RtveProgram>>(url, cancellationToken).ConfigureAwait(false);
        var resultPage = envelope?.Page;

        var programs = (resultPage?.Items ?? [])
            .Where(program => program.Id is not null)
            .Select(ToSummary)
            .ToList();

        var totalPages = Math.Max(1, resultPage?.TotalPages ?? 1);

        return new RtveProgramPage(
            programs,
            currentPage,
            totalPages,
            resultPage?.Total ?? programs.Count,
            currentPage < totalPages);
    }

    /// <summary>
    /// Reads the whole program catalog of RTVE, page by page.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Every program of the catalog, radio included, in the order RTVE publishes them.</returns>
    /// <remarks>
    /// RTVE caps a page of the catalog at <see cref="PageSize"/> programs and offers no way to filter
    /// by type, genre or date, so a page of the API is never enough to sort or filter the catalog as a
    /// whole. The first page tells how many pages there are; the rest are requested a few at a time and
    /// flattened here. A page that RTVE does not answer is skipped (the failure is already logged by
    /// <see cref="GetJsonAsync{T}"/>), so a hiccup in one page does not throw away the other 46.
    /// </remarks>
    public async Task<IReadOnlyList<RtveProgramSummary>> ListAllProgramsAsync(CancellationToken cancellationToken)
    {
        var firstEnvelope = await GetJsonAsync<RtveEnvelope<RtveProgram>>(
            CatalogPageUrl(1),
            cancellationToken).ConfigureAwait(false);

        var firstPage = firstEnvelope?.Page;
        if (firstPage is null)
        {
            return [];
        }

        var programs = new List<RtveProgram>(firstPage.Total);
        programs.AddRange(firstPage.Items ?? []);

        var totalPages = Math.Max(1, firstPage.TotalPages);
        if (totalPages > 1)
        {
            var remaining = new List<RtveProgram>?[totalPages - 1];
            using var gate = new SemaphoreSlim(CatalogPageConcurrency);

            var requests = Enumerable.Range(2, totalPages - 1).Select(async page =>
            {
                await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    var envelope = await GetJsonAsync<RtveEnvelope<RtveProgram>>(
                        CatalogPageUrl(page),
                        cancellationToken).ConfigureAwait(false);

                    remaining[page - 2] = envelope?.Page?.Items;
                }
                finally
                {
                    gate.Release();
                }
            });

            await Task.WhenAll(requests).ConfigureAwait(false);

            // The order of the response is the order of RTVE, page by page: a program that appears in
            // two pages (the catalog moves while it is read) is listed once.
            foreach (var page in remaining)
            {
                if (page is not null)
                {
                    programs.AddRange(page);
                }
            }
        }

        return programs
            .Where(program => program.Id is not null)
            .DistinctBy(program => program.Id)
            .Select(ToSummary)
            .ToList();
    }

    private static string CatalogPageUrl(int page, int size = PageSize)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "https://api.rtve.es/api/programas.json?page={0}&size={1}",
            page,
            size);
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
            using var response = await SendWithRetryAsync(url, cancellationToken).ConfigureAwait(false);

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
