using Jellyfin.Plugin.RtvePlay.Configuration;
using Jellyfin.Plugin.RtvePlay.Tasks;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.RtvePlay;

/// <summary>
/// Applies the configured refresh interval to the scheduled task.
/// </summary>
public static class RtveTriggerScheduler
{
    /// <summary>
    /// Updates the triggers of <see cref="RtveRefreshTask"/> with the configured interval.
    /// </summary>
    /// <param name="taskManager">Task manager.</param>
    /// <param name="configuration">Plugin configuration.</param>
    /// <param name="logger">Optional logger.</param>
    /// <returns><c>true</c> when the task was found and updated.</returns>
    public static bool Apply(ITaskManager taskManager, PluginConfiguration configuration, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(taskManager);
        ArgumentNullException.ThrowIfNull(configuration);

        var worker = taskManager.ScheduledTasks
            .FirstOrDefault(t => t.ScheduledTask is RtveRefreshTask);

        if (worker is null)
        {
            logger?.LogWarning("RTVE Play: la tarea programada no está registrada todavía; reinicia el servidor");
            return false;
        }

        var triggers = RtveRefreshTask.BuildTriggers(configuration.RefreshIntervalHours);
        worker.Triggers = triggers;
        worker.ReloadTriggerEvents();

        logger?.LogInformation(
            "RTVE Play: intervalo de actualización aplicado ({Hours} h)",
            configuration.RefreshIntervalHours);

        return true;
    }
}
