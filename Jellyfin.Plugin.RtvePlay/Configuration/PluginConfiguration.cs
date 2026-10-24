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
        SubtitleLanguage = "none";
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
    /// Gets or sets the preferred subtitle language ("es", "en", "gl", "ca", "eu" or "none").
    /// </summary>
    /// <remarks>
    /// The default is <c>none</c>: subtitles are only downloaded when the user picks a language.
    /// </remarks>
    public string SubtitleLanguage { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of videos per program (0 means no limit).
    /// </summary>
    public int MaxItemsPerProgram { get; set; }
}
