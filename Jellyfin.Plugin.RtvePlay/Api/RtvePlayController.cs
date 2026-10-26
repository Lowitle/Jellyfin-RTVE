using Jellyfin.Plugin.RtvePlay.Configuration;
using Jellyfin.Plugin.RtvePlay.Library;
using Jellyfin.Plugin.RtvePlay.Rtve;
using Jellyfin.Plugin.RtvePlay.Tasks;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.RtvePlay.Api;

/// <summary>
/// Endpoints used by the configuration page.
/// </summary>
[ApiController]
[Authorize]
[Route("RtvePlay")]
public sealed class RtvePlayController : ControllerBase
{
    private readonly RtveClient _client;
    private readonly ITaskManager _taskManager;
    private readonly ILibraryManager _libraryManager;
    private readonly ILibraryMonitor _libraryMonitor;
    private readonly ILogger<RtvePlayController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RtvePlayController"/> class.
    /// </summary>
    /// <param name="client">RTVE API client.</param>
    /// <param name="taskManager">Task manager.</param>
    /// <param name="libraryManager">Library manager.</param>
    /// <param name="libraryMonitor">Library monitor, used to refresh the folders added to a library.</param>
    /// <param name="logger">Logger.</param>
    public RtvePlayController(
        RtveClient client,
        ITaskManager taskManager,
        ILibraryManager libraryManager,
        ILibraryMonitor libraryMonitor,
        ILogger<RtvePlayController> logger)
    {
        _client = client;
        _taskManager = taskManager;
        _libraryManager = libraryManager;
        _libraryMonitor = libraryMonitor;
        _logger = logger;
    }

    /// <summary>
    /// Searches the RTVE program catalog by title.
    /// </summary>
    /// <param name="query">Text to search.</param>
    /// <param name="limit">Maximum number of results (1-50).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The matching programs.</returns>
    [HttpGet("Programs/Search")]
    public async Task<ActionResult<IReadOnlyList<RtveProgramSummary>>> SearchAsync(
        [FromQuery] string? query,
        [FromQuery] int? limit,
        CancellationToken cancellationToken)
    {
        var results = await _client
            .SearchProgramsAsync(query ?? string.Empty, Math.Clamp(limit ?? 20, 1, 50), cancellationToken)
            .ConfigureAwait(false);

        return Ok(results);
    }

    /// <summary>
    /// Resolves a program from an id, a slug or a program URL.
    /// </summary>
    /// <param name="value">Program id, slug or URL.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The resolved program.</returns>
    [HttpGet("Programs/Resolve")]
    public async Task<ActionResult<RtveProgramSummary>> ResolveAsync(
        [FromQuery] string? value,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return BadRequest("Indica el id, el slug o la URL del programa.");
        }

        try
        {
            var id = await _client.ResolveProgramIdAsync(value, cancellationToken).ConfigureAwait(false);
            var program = await _client.GetProgramAsync(id, cancellationToken).ConfigureAwait(false);

            // The kind and the genres come from the summary projection, so the entry added by hand
            // shows the same columns as the ones picked from the catalog table.
            return Ok(program is null
                ? new RtveProgramSummary(id, value.Trim(), null, null)
                : RtveClient.ToSummary(program) with { Id = id });
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            _logger.LogWarning(ex, "RTVE Play: no se pudo resolver el programa {Value}", value);
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Lists the programs of the RTVE catalog, one page at a time.
    /// </summary>
    /// <param name="query">Title filter (empty to list the whole catalog).</param>
    /// <param name="page">Page number (1 based).</param>
    /// <param name="size">Page size (1-100).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The page of programs. The radio programs are included and flagged with
    /// <see cref="RtveProgramSummary.HasVideo"/> = <c>false</c>, so the page always returns the same
    /// number of programs and the configuration page is the one that hides them.
    /// </returns>
    [HttpGet("Programs/List")]
    public async Task<ActionResult<RtveProgramPage>> ListAsync(
        [FromQuery] string? query,
        [FromQuery] int? page,
        [FromQuery] int? size,
        CancellationToken cancellationToken)
    {
        var result = await _client
            .ListProgramsAsync(query, page ?? 1, size ?? 40, cancellationToken)
            .ConfigureAwait(false);

        return Ok(result);
    }

    /// <summary>
    /// Checks that the destination folder exists and is writable by the Jellyfin process.
    /// </summary>
    /// <param name="path">Folder to check.</param>
    /// <returns>The result of the check.</returns>
    [HttpGet("Path/Check")]
    public ActionResult<LibraryPathStatus> CheckPath([FromQuery] string? path)
        => Ok(LibraryPath.Check(path));

    /// <summary>
    /// Creates a «Programas de TV» library that points at the folders of the destination, or updates
    /// the folders of the library that already exists.
    /// </summary>
    /// <remarks>
    /// The status is always returned with HTTP 200 and a <c>success</c> flag, so the configuration
    /// page can show the reason of a rejection instead of a generic error.
    /// </remarks>
    /// <param name="path">Destination folder; the configured one when omitted.</param>
    /// <param name="name">Library name; «RTVE Play» when omitted.</param>
    /// <param name="sections">
    /// Whether the library has to point at the section folders; the configured value when omitted.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the operation.</returns>
    [HttpPost("Library/Create")]
    public async Task<ActionResult<LibraryProvisionStatus>> CreateLibraryAsync(
        [FromQuery] string? path,
        [FromQuery] string? name,
        [FromQuery] bool? sections,
        CancellationToken cancellationToken)
    {
        var configuration = Plugin.Instance?.Configuration ?? new PluginConfiguration();
        var target = string.IsNullOrWhiteSpace(path) ? configuration.DestinationPath : path;

        var status = await LibraryProvisioner
            .ProvisionAsync(
                _libraryManager,
                target,
                name,
                sections ?? configuration.OrganizeSections,
                _logger,
                cancellationToken,
                _libraryMonitor)
            .ConfigureAwait(false);

        if (status.Success)
        {
            _logger.LogInformation(
                "RTVE Play: biblioteca {Outcome} ({Name} → {Path}, {Folders} carpetas)",
                status.Outcome,
                status.LibraryName,
                status.Path,
                status.LibraryPaths.Count);
        }
        else
        {
            _logger.LogWarning("RTVE Play: no se pudo crear la biblioteca: {Message}", status.Message);
        }

        return Ok(status);
    }

    /// <summary>
    /// Lists the section folders of the destination and the libraries that index them.
    /// </summary>
    /// <remarks>
    /// The page uses it for the «Carpetas y bibliotecas» table: Jellyfin can index the same folder in
    /// several libraries, so a section can be added to a library of the user and still be part of
    /// «RTVE Play».
    /// </remarks>
    /// <param name="path">Destination folder; the configured one when omitted.</param>
    /// <param name="name">Library name; «RTVE Play» when omitted.</param>
    /// <param name="sections">Whether the sync writes each program into the subfolder of its kind.</param>
    /// <returns>The section folders and the libraries of the server.</returns>
    [HttpGet("Library/Folders")]
    public ActionResult<LibrarySectionMatrix> GetLibraryFolders(
        [FromQuery] string? path,
        [FromQuery] string? name,
        [FromQuery] bool? sections)
    {
        var configuration = Plugin.Instance?.Configuration ?? new PluginConfiguration();
        var target = string.IsNullOrWhiteSpace(path) ? configuration.DestinationPath : path;

        if (string.IsNullOrWhiteSpace(target))
        {
            return BadRequest("Escribe la carpeta de destino antes de ver las carpetas.");
        }

        return Ok(LibraryProvisioner.Describe(
            target,
            name,
            sections ?? configuration.OrganizeSections,
            LibraryProvisioner.ToFolders(_libraryManager.GetVirtualFolders() ?? [])));
    }

    /// <summary>
    /// Adds a folder of the destination to a library of the server.
    /// </summary>
    /// <remarks>
    /// The folder is not removed from «RTVE Play»: Jellyfin indexes it in both libraries. The status is
    /// always returned with HTTP 200 and a <c>success</c> flag, so the page can show the reason of a
    /// rejection instead of a generic error.
    /// </remarks>
    /// <param name="folder">Folder to associate.</param>
    /// <param name="library">Library that has to index the folder.</param>
    /// <returns>The result of the operation.</returns>
    [HttpPost("Library/Associate")]
    public ActionResult<LibraryAssociateStatus> AssociateFolder(
        [FromQuery] string? folder,
        [FromQuery] string? library)
    {
        var status = LibraryProvisioner.Associate(_libraryManager, folder, library, _logger, _libraryMonitor);

        if (status.Success)
        {
            _logger.LogInformation(
                "RTVE Play: carpeta {Folder} → biblioteca {Library} ({Outcome})",
                status.Folder,
                status.LibraryName,
                status.Outcome);
        }
        else
        {
            _logger.LogWarning("RTVE Play: no se pudo asociar la carpeta: {Message}", status.Message);
        }

        return Ok(status);
    }

    /// <summary>
    /// Applies the configured refresh interval to the scheduled task.
    /// </summary>
    /// <returns>No content.</returns>
    [HttpPost("Triggers")]
    public ActionResult ApplyTriggers()
    {
        var configuration = Plugin.Instance?.Configuration ?? new PluginConfiguration();
        RtveTriggerScheduler.Apply(_taskManager, configuration, _logger);
        return NoContent();
    }

    /// <summary>
    /// Starts the refresh task immediately.
    /// </summary>
    /// <remarks>
    /// The stock <c>ScheduledTasks/Running/{id}</c> endpoint looks the task up by its GUID, not by
    /// its key, so the configuration page cannot use it directly without a round trip.
    /// </remarks>
    /// <returns>No content when the task was queued; 404 when it is not registered yet.</returns>
    [HttpPost("Refresh")]
    public ActionResult RunNow()
    {
        var worker = _taskManager.ScheduledTasks
            .FirstOrDefault(task => task.ScheduledTask is RtveRefreshTask);

        if (worker is null)
        {
            _logger.LogWarning("RTVE Play: la tarea programada no está registrada; no se puede ejecutar");
            return NotFound("La tarea programada no está registrada todavía. Reinicia Jellyfin.");
        }

        _ = _taskManager.Execute(worker, new TaskOptions());
        return NoContent();
    }
}
