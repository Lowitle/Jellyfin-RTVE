using Jellyfin.Plugin.RtvePlay.Configuration;
using Jellyfin.Plugin.RtvePlay.Library;
using Jellyfin.Plugin.RtvePlay.Rtve;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.RtvePlay.Sync;

/// <summary>
/// Program level synchronization.
/// </summary>
public sealed partial class RtveSyncService
{
    private async Task<int> SyncProgramAsync(
        PluginConfiguration configuration,
        RtveProgramReference reference,
        Action<double> reportProgress,
        CancellationToken cancellationToken)
    {
        var programId = await _client.ResolveProgramIdAsync(reference.Id, cancellationToken).ConfigureAwait(false);
        var program = await _client.GetProgramAsync(programId, cancellationToken).ConfigureAwait(false);
        var videos = await _client.GetProgramVideosAsync(
            programId,
            configuration.MaxItemsPerProgram,
            cancellationToken).ConfigureAwait(false);

        if (videos.Count == 0)
        {
            _logger.LogWarning("RTVE Play: el programa {ProgramId} no tiene vídeos publicados", programId);
            reportProgress(1d);
            return 0;
        }

        var seriesTitle = FirstNonEmpty(
            program?.Title,
            videos.Select(v => v.ProgramInfo?.Title).FirstOrDefault(t => !string.IsNullOrWhiteSpace(t)),
            reference.Title,
            programId);

        var seriesPlot = HtmlText.FromHtml(program?.LongDescription) ?? HtmlText.FromHtml(program?.Description);

        var usable = videos.Where(v => !string.IsNullOrWhiteSpace(v.Id)).ToList();
        var numbered = usable.Count > 0 && usable.All(v => v.Episode is > 0);
        var ordered = usable
            .OrderBy(v => v.PublicationDateTimestamp ?? 0L)
            .ThenBy(v => v.Id, StringComparer.Ordinal)
            .ToList();

        var episodes = new List<LibraryEpisode>();
        var skipped = 0;

        for (var index = 0; index < ordered.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var video = ordered[index];
            var season = numbered ? Math.Max(1, video.SeasonNumber ?? 1) : 1;
            var number = numbered ? video.Episode!.Value : index + 1;

            var episode = await BuildEpisodeAsync(
                configuration,
                seriesTitle,
                video,
                season,
                number,
                cancellationToken).ConfigureAwait(false);

            if (episode is null)
            {
                skipped++;
            }
            else
            {
                episodes.Add(episode);
            }

            reportProgress((index + 1d) / ordered.Count);
        }

        if (episodes.Count == 0)
        {
            _logger.LogWarning("RTVE Play: no se pudo resolver ningún vídeo del programa {ProgramId}", programId);
            return 0;
        }

        var result = StrmLibrary.Write(
            configuration.DestinationPath,
            seriesTitle,
            programId,
            seriesPlot,
            episodes,
            new LibraryWriteOptions
            {
                WriteNfo = configuration.WriteNfo,
                WriteSubtitles = configuration.WriteSubtitles,
                DeleteObsolete = configuration.DeleteObsolete
            });

        if (program is not null)
        {
            await SaveArtworkAsync(program, result.SeriesPath, cancellationToken).ConfigureAwait(false);
        }

        _libraryMonitor.ReportFileSystemChanged(result.SeriesPath);

        _logger.LogInformation(
            "RTVE Play: {Series} → {Episodes} episodios ({Skipped} descartados), {Files} archivos escritos, {Removed} obsoletos eliminados",
            seriesTitle,
            result.EpisodesWritten,
            skipped,
            result.FilesWritten,
            result.FilesRemoved);

        return result.FilesWritten;
    }
}
