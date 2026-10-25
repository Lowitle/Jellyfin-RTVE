using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.RtvePlay.Rtve;

/// <summary>
/// Root object of every RTVE API response.
/// </summary>
/// <typeparam name="T">Type of the items in the page.</typeparam>
public sealed class RtveEnvelope<T>
{
    /// <summary>
    /// Gets or sets the page of results.
    /// </summary>
    [JsonPropertyName("page")]
    public RtvePage<T>? Page { get; set; }
}

/// <summary>
/// A page of results returned by the RTVE API.
/// </summary>
/// <typeparam name="T">Type of the items in the page.</typeparam>
public sealed class RtvePage<T>
{
    /// <summary>
    /// Gets or sets the items.
    /// </summary>
    [JsonPropertyName("items")]
    public List<T>? Items { get; set; }

    /// <summary>
    /// Gets or sets the total number of items in the collection.
    /// </summary>
    [JsonPropertyName("total")]
    public int Total { get; set; }

    /// <summary>
    /// Gets or sets the total number of pages.
    /// </summary>
    [JsonPropertyName("totalPages")]
    public int TotalPages { get; set; }

    /// <summary>
    /// Gets or sets the current page number (1 based).
    /// </summary>
    [JsonPropertyName("number")]
    public int Number { get; set; }

    /// <summary>
    /// Gets or sets the page size.
    /// </summary>
    [JsonPropertyName("size")]
    public int Size { get; set; }
}

/// <summary>
/// A program (series) of the RTVE catalog.
/// </summary>
public sealed class RtveProgram
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
    /// Gets or sets the URL of the program page on rtve.es.
    /// </summary>
    [JsonPropertyName("htmlUrl")]
    public string? HtmlUrl { get; set; }

    /// <summary>
    /// Gets or sets the program poster image URL.
    /// </summary>
    [JsonPropertyName("imgPoster")]
    public string? PosterImage { get; set; }

    /// <summary>
    /// Gets or sets the program portrait image URL.
    /// </summary>
    [JsonPropertyName("imgPortada")]
    public string? CoverImage { get; set; }

    /// <summary>
    /// Gets or sets the program background image URL.
    /// </summary>
    [JsonPropertyName("imgBackground")]
    public string? BackgroundImage { get; set; }

    /// <summary>
    /// Gets or sets the HTML description of the program.
    /// </summary>
    [JsonPropertyName("longDescription")]
    public string? LongDescription { get; set; }

    /// <summary>
    /// Gets or sets the plain text description of the program.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the number of seasons.
    /// </summary>
    [JsonPropertyName("numSeasons")]
    [JsonConverter(typeof(FlexibleInt32Converter))]
    public int? SeasonCount { get; set; }

    /// <summary>
    /// Gets or sets the program type as published by RTVE ("Magacín semanal", "Serie Documental"...).
    /// </summary>
    [JsonPropertyName("programType")]
    public string? ProgramType { get; set; }

    /// <summary>
    /// Gets or sets the numeric id of <see cref="ProgramType"/>. RTVE keeps it stable when the
    /// label changes, so it is the preferred key to classify the program.
    /// </summary>
    [JsonPropertyName("programTypeId")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? ProgramTypeId { get; set; }

    /// <summary>
    /// Gets or sets the IMDb id ("tt0375434"), when RTVE publishes one.
    /// </summary>
    [JsonPropertyName("idImdb")]
    public string? ImdbId { get; set; }

    /// <summary>
    /// Gets or sets the Wikipedia page of the program, when RTVE publishes one.
    /// </summary>
    [JsonPropertyName("idWiki")]
    public string? WikiUrl { get; set; }

    /// <summary>
    /// Gets or sets the production country ("ES").
    /// </summary>
    [JsonPropertyName("country")]
    public string? Country { get; set; }

    /// <summary>
    /// Gets or sets the age rating ("No recomendable para menores de 7 años"), when RTVE publishes one.
    /// </summary>
    [JsonPropertyName("ageRange")]
    public string? AgeRating { get; set; }

    /// <summary>
    /// Gets or sets the RTVE categories of the program ("Cultura", "Humor", "Información y actualidad"...).
    /// </summary>
    [JsonPropertyName("generos")]
    public List<RtveGenre>? Genres { get; set; }
}
