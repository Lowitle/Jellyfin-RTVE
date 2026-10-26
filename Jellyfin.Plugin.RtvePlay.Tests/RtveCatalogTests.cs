using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Jellyfin.Plugin.RtvePlay.Rtve;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jellyfin.Plugin.RtvePlay.Tests;

/// <summary>
/// Tests of <see cref="RtveClient.ListAllProgramsAsync"/>, the call that loads the whole catalog
/// (4640 programs, 47 pages of 100) so the configuration page can filter and sort it as a whole.
/// </summary>
public class RtveCatalogTests
{
    [Fact]
    public async Task ListAllProgramsAsync_ReadsEveryPage()
    {
        using var handler = new CatalogHandler(totalPages: 3, pageSize: 2);

        var programs = await CreateClient(handler).ListAllProgramsAsync(CancellationToken.None);

        Assert.Equal(["1", "2", "3", "4", "5", "6"], programs.Select(program => program.Id));
        Assert.Equal([1, 2, 3], handler.RequestedPages.OrderBy(page => page));
        Assert.All(handler.RequestedUrls, url => Assert.Contains("size=100", url, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ListAllProgramsAsync_WithOnePage_MakesOneRequest()
    {
        using var handler = new CatalogHandler(totalPages: 1, pageSize: 4);

        var programs = await CreateClient(handler).ListAllProgramsAsync(CancellationToken.None);

        Assert.Equal(4, programs.Count);
        Assert.Equal([1], handler.RequestedPages);
    }

    [Fact]
    public async Task ListAllProgramsAsync_SkipsAPageThatRtvEDoesNotAnswer()
    {
        using var handler = new CatalogHandler(totalPages: 3, pageSize: 2, failingPage: 2);

        var programs = await CreateClient(handler).ListAllProgramsAsync(CancellationToken.None);

        // The other pages are not thrown away because one of them failed.
        Assert.Equal(["1", "2", "5", "6"], programs.Select(program => program.Id));
    }

    [Fact]
    public async Task ListAllProgramsAsync_WithoutTheFirstPage_ReturnsNoPrograms()
    {
        using var handler = new CatalogHandler(totalPages: 1, pageSize: 4, failingPage: 1);

        var programs = await CreateClient(handler).ListAllProgramsAsync(CancellationToken.None);

        Assert.Empty(programs);
    }

    [Fact]
    public async Task ListAllProgramsAsync_DoesNotRepeatAProgramListedInTwoPages()
    {
        // RTVE reorders the catalog while it is read, so a program can appear in two consecutive pages.
        using var handler = new CatalogHandler(
            totalPages: 2,
            pageSize: 2,
            ids: page => page == 1 ? ["1", "2"] : ["2", "3"]);

        var programs = await CreateClient(handler).ListAllProgramsAsync(CancellationToken.None);

        Assert.Equal(["1", "2", "3"], programs.Select(program => program.Id));
    }

    [Fact]
    public async Task ListAllProgramsAsync_MapsTheDatesOfEveryProgram()
    {
        using var handler = new CatalogHandler(totalPages: 1, pageSize: 1);

        var program = Assert.Single(await CreateClient(handler).ListAllProgramsAsync(CancellationToken.None));

        Assert.Equal(2001, program.FirstYear);
        Assert.Equal("2026-10-21", program.LastEmission);
    }

    private static RtveClient CreateClient(HttpMessageHandler handler)
        => new(new HttpClient(handler), NullLogger<RtveClient>.Instance);

    /// <summary>
    /// Answers <c>programas.json</c> with a page of the requested size, keeping track of the pages that
    /// were asked for so the test can check the paging.
    /// </summary>
    private sealed class CatalogHandler : HttpMessageHandler
    {
        private readonly int _totalPages;
        private readonly int _pageSize;
        private readonly int _failingPage;
        private readonly Func<int, IReadOnlyList<string>> _ids;
        private readonly object _gate = new();

        public CatalogHandler(
            int totalPages,
            int pageSize,
            int failingPage = 0,
            Func<int, IReadOnlyList<string>>? ids = null)
        {
            _totalPages = totalPages;
            _pageSize = pageSize;
            _failingPage = failingPage;
            _ids = ids ?? (page => Enumerable
                .Range(1, pageSize)
                .Select(index => ((page - 1) * pageSize + index).ToString(CultureInfo.InvariantCulture))
                .ToList());
        }

        public List<int> RequestedPages { get; } = [];

        public List<string> RequestedUrls { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.ToString();
            var page = int.Parse(
                url[(url.IndexOf("page=", StringComparison.Ordinal) + "page=".Length)..].Split('&')[0],
                CultureInfo.InvariantCulture);

            lock (_gate)
            {
                RequestedUrls.Add(url);
                RequestedPages.Add(page);
            }

            if (page == _failingPage)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }

            var items = _ids(page)
                .Select(id => new RtveProgram
                {
                    Id = id,
                    Title = "Programa " + id,
                    HtmlUrl = "https://www.rtve.es/play/videos/programa-" + id + "/",
                    ProductionDate = "28-10-2001 00:00:00",
                    PublicationDate = "21-10-2026 00:00:00"
                })
                .ToList();

            var payload = JsonSerializer.Serialize(new RtveEnvelope<RtveProgram>
            {
                Page = new RtvePage<RtveProgram>
                {
                    Items = items,
                    Number = page,
                    Size = _pageSize,
                    TotalPages = _totalPages,
                    Total = _totalPages * _pageSize
                }
            });

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            });
        }
    }
}
