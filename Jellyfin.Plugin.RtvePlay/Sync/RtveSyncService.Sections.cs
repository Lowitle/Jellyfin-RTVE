using Jellyfin.Plugin.RtvePlay.Configuration;
using Jellyfin.Plugin.RtvePlay.Library;
using Jellyfin.Plugin.RtvePlay.Rtve;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.RtvePlay.Sync;

/// <summary>
/// Folder organization of the library (one subfolder per kind of program).
/// </summary>
public sealed partial class RtveSyncService
{
    /// <summary>
    /// Resolves the section (and so the subfolder) of a program.
    /// </summary>
    /// <param name="program">Program metadata, when RTVE returned it.</param>
    /// <param name="seriesTitle">Series title, used to spot the anime of the children's catalog.</param>
    /// <returns>The section of the program.</returns>
    internal static ProgramSection ResolveSection(RtveProgram? program, string seriesTitle)
    {
        var kind = ProgramClassifier.ResolveKind(program?.ProgramTypeId, program?.ProgramType);

        return ProgramSections.Resolve(kind, program?.ProgramType, seriesTitle);
    }

    /// <summary>
    /// Moves the series folder written by plugin versions before 1.0.4.0 (directly inside the
    /// destination folder) into its section folder, once.
    /// </summary>
    /// <param name="configuration">Plugin configuration.</param>
    /// <param name="targetRoot">Section folder the series belongs to.</param>
    /// <param name="seriesTitle">Series title.</param>
    /// <returns>The folder the series was moved from, or <c>null</c> when nothing was moved.</returns>
    /// <remarks>
    /// Nothing is moved when the sections are disabled, when the destination already is the section
    /// folder or when the target folder already exists (a previous sync already migrated it). A
    /// program that RTVE reclassifies is moved from its old section too, so the library does not end up
    /// with the same series in two folders.
    /// </remarks>
    private string? MigrateLegacySeriesFolder(
        PluginConfiguration configuration,
        string targetRoot,
        string seriesTitle)
    {
        var duplicate = SectionMigration.FindDuplicateAtRoot(
            configuration.DestinationPath,
            targetRoot,
            seriesTitle);

        if (duplicate is not null)
        {
            // The plugin does not delete folders the user may want to keep, but Jellyfin indexes the
            // two copies as two different series, so the leftover has to be pointed out.
            _logger.LogWarning(
                "RTVE Play: «{Series}» está en dos carpetas, {From} y {To}; se usa la segunda y la primera "
                + "se puede borrar",
                seriesTitle,
                duplicate,
                StrmLibrary.SeriesFolder(targetRoot, seriesTitle));
            return null;
        }

        var legacy = SectionMigration.FindLegacyFolder(
            configuration.DestinationPath,
            targetRoot,
            seriesTitle,
            configuration.OrganizeSections);

        if (legacy is null)
        {
            return null;
        }

        var target = StrmLibrary.SeriesFolder(targetRoot, seriesTitle);

        try
        {
            Directory.CreateDirectory(targetRoot);
            Directory.Move(legacy, target);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            _logger.LogWarning(
                ex,
                "RTVE Play: no se pudo mover «{Series}» de {From} a {To}; se escribirá de nuevo en la carpeta nueva "
                + "y la antigua se queda como estaba (bórrala a mano para no tenerla dos veces)",
                seriesTitle,
                legacy,
                target);
            return null;
        }

        _logger.LogWarning(
            "RTVE Play: «{Series}» se ha movido de {From} a {To}; Jellyfin lo verá como una serie nueva, "
            + "así que puede perder el estado de visto y los cambios manuales de sus episodios",
            seriesTitle,
            legacy,
            target);

        return legacy;
    }
}
