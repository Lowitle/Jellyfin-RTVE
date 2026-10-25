using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.RtvePlay.Configuration;

/// <summary>
/// A reference to an RTVE Play program, either by numeric id ("111570") or by page slug ("saber-vivir").
/// </summary>
public class RtveProgramReference
{
    /// <summary>
    /// Gets or sets the program id or slug as typed by the user.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the cached program title (optional, filled in by the refresh task).
    /// </summary>
    public string Title { get; set; } = string.Empty;
}

/// <summary>
/// Plugin configuration.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PluginConfiguration"/> class.
    /// </summary>
    public PluginConfiguration()
    {
        Programs = new List<RtveProgramReference>();
        DestinationPath = string.Empty;
        RefreshIntervalHours = 12;
        FilterDrm = true;
        PreferMp4 = true;
        WriteNfo = true;
        WriteSubtitles = true;
        DeleteObsolete = false;
        SubtitleLanguages = new List<string>();
        DownloadAllSubtitleLanguages = false;
        SubtitleLanguage = string.Empty;
        MaxItemsPerProgram = 0;
    }

    /// <summary>
    /// Gets or sets the programs to mirror.
    /// </summary>
    public List<RtveProgramReference> Programs { get; set; }

    /// <summary>
    /// Gets or sets the library root folder where the series folders are written.
    /// </summary>
    public string DestinationPath { get; set; }

    /// <summary>
    /// Gets or sets the refresh interval, in hours.
    /// </summary>
    public int RefreshIntervalHours { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether items flagged with DRM are skipped.
    /// </summary>
    public bool FilterDrm { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the progressive MP4 stream is preferred over HLS when available.
    /// </summary>
    public bool PreferMp4 { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether .nfo files are written.
    /// </summary>
    public bool WriteNfo { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether WebVTT subtitles are downloaded and converted to SRT.
    /// </summary>
    public bool WriteSubtitles { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether .strm files that no longer exist in the catalog are removed.
    /// </summary>
    public bool DeleteObsolete { get; set; }

    /// <summary>
    /// Gets or sets the subtitle languages to download ("es", "en", "ca", "gl", "eu").
    /// </summary>
    /// <remarks>
    /// The list is empty by default: subtitles are only downloaded when the user ticks at least one
    /// language. When <see cref="DownloadAllSubtitleLanguages"/> is enabled this list is ignored.
    /// </remarks>
    public List<string> SubtitleLanguages { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether every subtitle track published by RTVE is downloaded,
    /// regardless of <see cref="SubtitleLanguages"/>.
    /// </summary>
    public bool DownloadAllSubtitleLanguages { get; set; }

    /// <summary>
    /// Gets or sets the subtitle language of plugin versions before 1.0.1.0 ("es", "en", "gl", "ca", "eu" or "none").
    /// </summary>
    /// <remarks>
    /// Kept only to migrate existing configurations into <see cref="SubtitleLanguages"/>; the
    /// configuration page clears it as soon as the settings are saved.
    /// </remarks>
    public string SubtitleLanguage { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of videos per program (0 means no limit).
    /// </summary>
    public int MaxItemsPerProgram { get; set; }
}
