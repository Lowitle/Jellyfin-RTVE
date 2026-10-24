using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.RtvePlay.Rtve;

/// <summary>
/// A video (episode) of the RTVE catalog.
/// </summary>
public sealed class RtveVideo
{
    /// <summary>
    /// Gets or sets the video id.
    /// </summary>
    [JsonPropertyName("id")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? Id { get; set; }

    /// <summary>
    /// Gets or sets the title.
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    /// <summary>
    /// Gets or sets the long title.
    /// </summary>
    [JsonPropertyName("longTitle")]
    public string? LongTitle { get; set; }

    /// <summary>
    /// Gets or sets the short description.
    /// </summary>
    [JsonPropertyName("shortDescription")]
    public string? ShortDescription { get; set; }

    /// <summary>
    /// Gets or sets the HTML description.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the thumbnail image URL.
    /// </summary>
    [JsonPropertyName("thumbnail")]
    public string? Thumbnail { get; set; }

    /// <summary>
    /// Gets or sets the duration, in milliseconds.
    /// </summary>
    [JsonPropertyName("duration")]
    [JsonConverter(typeof(FlexibleInt64Converter))]
    public long? Duration { get; set; }

    /// <summary>
    /// Gets or sets the publication timestamp, in milliseconds.
    /// </summary>
    [JsonPropertyName("publicationDateTimestamp")]
    [JsonConverter(typeof(FlexibleInt64Converter))]
    public long? PublicationDateTimestamp { get; set; }

    /// <summary>
    /// Gets or sets the episode number inside its season (0 or null when RTVE does not provide it).
    /// </summary>
    [JsonPropertyName("episode")]
    [JsonConverter(typeof(FlexibleInt32Converter))]
    public int? Episode { get; set; }

    /// <summary>
    /// Gets or sets the season number (usually empty for RTVE Play programs).
    /// </summary>
    [JsonPropertyName("temporadaOrden")]
    [JsonConverter(typeof(FlexibleInt32Converter))]
    public int? SeasonNumber { get; set; }

    /// <summary>
    /// Gets or sets the season name.
    /// </summary>
    [JsonPropertyName("temporada")]
    public string? SeasonName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the video is protected with DRM.
    /// </summary>
    [JsonPropertyName("hasDRM")]
    public bool HasDrm { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a login is required.
    /// </summary>
    [JsonPropertyName("requireLogged")]
    public bool RequireLogged { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the video is geolocated (blocked outside Spain).
    /// </summary>
    [JsonPropertyName("geolocalizado")]
    public bool GeoBlocked { get; set; }

    /// <summary>
    /// Gets or sets the program this video belongs to.
    /// </summary>
    [JsonPropertyName("programInfo")]
    public RtveProgramInfo? ProgramInfo { get; set; }

    /// <summary>
    /// Gets or sets the available streams.
    /// </summary>
    [JsonPropertyName("qualities")]
    public List<RtveQuality>? Qualities { get; set; }

    /// <summary>
    /// Gets or sets the genres.
    /// </summary>
    [JsonPropertyName("generos")]
    public List<RtveGenre>? Genres { get; set; }

    /// <summary>
    /// Gets or sets the director names.
    /// </summary>
    [JsonPropertyName("director")]
    public string? Director { get; set; }
}
