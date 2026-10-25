using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.RtvePlay.Rtve;

/// <summary>
/// Minimal program information attached to a video.
/// </summary>
public sealed class RtveProgramInfo
{
    /// <summary>
    /// Gets or sets the program id.
    /// </summary>
    [JsonPropertyName("id")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? Id { get; set; }

    /// <summary>
    /// Gets or sets the program title.
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    /// <summary>
    /// Gets or sets the program page URL.
    /// </summary>
    [JsonPropertyName("htmlUrl")]
    public string? HtmlUrl { get; set; }
}

/// <summary>
/// A stream (quality) of a video.
/// </summary>
public sealed class RtveQuality
{
    /// <summary>
    /// Gets or sets the quality identifier (a number in the RTVE payload, for instance <c>8124123</c>).
    /// </summary>
    [JsonPropertyName("identifier")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? Identifier { get; set; }

    /// <summary>
    /// Gets or sets the quality preset name (for instance <c>HD_FULL</c>).
    /// </summary>
    [JsonPropertyName("preset")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? Preset { get; set; }

    /// <summary>
    /// Gets or sets the encrypted (PNG) path that has to be resolved to a playback URL.
    /// </summary>
    [JsonPropertyName("filePath")]
    public string? FilePath { get; set; }

    /// <summary>
    /// Gets or sets the file size, in bytes.
    /// </summary>
    [JsonPropertyName("filesize")]
    [JsonConverter(typeof(FlexibleInt64Converter))]
    public long? FileSize { get; set; }
}

/// <summary>
/// A genre of a video.
/// </summary>
public sealed class RtveGenre
{
    /// <summary>
    /// Gets or sets the genre name.
    /// </summary>
    [JsonPropertyName("generoInf")]
    public string? Name { get; set; }
}

/// <summary>
/// A subtitle track of a video.
/// </summary>
public sealed class RtveSubtitle
{
    /// <summary>
    /// Gets or sets the URL of the WebVTT file.
    /// </summary>
    [JsonPropertyName("src")]
    public string? Source { get; set; }

    /// <summary>
    /// Gets or sets the language code.
    /// </summary>
    [JsonPropertyName("lang")]
    public string? Language { get; set; }
}

/// <summary>
/// A program search result exposed by the plugin API.
/// </summary>
/// <param name="Id">Program id.</param>
/// <param name="Title">Program title.</param>
/// <param name="Url">Program page URL.</param>
/// <param name="ImageUrl">Program poster URL.</param>
public sealed record RtveProgramSummary(string Id, string Title, string? Url, string? ImageUrl);

/// <summary>
/// A page of the RTVE program catalog.
/// </summary>
/// <param name="Items">Programs of the page.</param>
/// <param name="Page">Current page (1 based).</param>
/// <param name="TotalPages">Pages reported by RTVE (best effort when a title filter is used).</param>
/// <param name="TotalItems">Programs reported by RTVE (best effort when a title filter is used).</param>
/// <param name="HasMore">Whether another page can be requested.</param>
public sealed record RtveProgramPage(
    IReadOnlyList<RtveProgramSummary> Items,
    int Page,
    int TotalPages,
    int TotalItems,
    bool HasMore);
