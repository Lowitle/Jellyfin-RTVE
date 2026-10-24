using Jellyfin.Plugin.RtvePlay.Configuration;
using Jellyfin.Plugin.RtvePlay.Rtve;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.RtvePlay.Sync;

/// <summary>
/// Mirrors the configured RTVE programs into .strm files.
/// </summary>
public sealed partial class RtveSyncService
{
    private readonly RtveClient _client;
    private readonly ZtnrResolver _resolver;
    private readonly ILibraryMonitor _libraryMonitor;
    private readonly ILogger<RtveSyncService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RtveSyncService"/> class.
    /// </summary>
    /// <param name="client">RTVE API client.</param>
    /// <param name="resolver">ZTNR resolver.</param>
    /// <param name="libraryMonitor">Library monitor, notified after each series is updated.</param>
    /// <param name="logger">Logger.</param>
    public RtveSyncService(
        RtveClient client,
        ZtnrResolver resolver,
        ILibraryMonitor libraryMonitor,
        ILogger<RtveSyncService> logger)
    {
        _client = client;
        _resolver = resolver;
        _libraryMonitor = libraryMonitor;
        _logger = logger;
    }

    /// <summary>
    /// Synchronizes every configured program.
    /// </summary>
    /// <param name="configuration">Plugin configuration.</param>
    /// <param name="progress">Progress (0-100).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the operation.</returns>
    public async Task RunAsync(
        PluginConfiguration configuration,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var references = (configuration.Programs ?? [])
            .Where(p => !string.IsNullOrWhiteSpace(p.Id))
            .ToList();

        if (references.Count == 0)
        {
            _logger.LogWarning("RTVE Play: no hay programas configurados, no se genera nada");
            progress?.Report(100);
            return;
        }

        if (string.IsNullOrWhiteSpace(configuration.DestinationPath))
        {
            throw new InvalidOperationException("RTVE Play: configura la carpeta de destino antes de actualizar.");
        }

        Directory.CreateDirectory(configuration.DestinationPath);

        var totalWritten = 0;
        for (var i = 0; i < references.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var index = i;
            try
            {
                totalWritten += await SyncProgramAsync(
                    configuration,
                    references[index],
                    fraction => progress?.Report(
                        ((index + Math.Clamp(fraction, 0d, 1d)) / references.Count) * 100d),
                    cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RTVE Play: error al sincronizar el programa {Program}", references[index].Id);
            }
        }

        progress?.Report(100);
        _logger.LogInformation("RTVE Play: sincronización completada, {Files} archivos escritos", totalWritten);
    }
}
