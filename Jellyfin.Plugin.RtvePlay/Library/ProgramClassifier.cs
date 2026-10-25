using Jellyfin.Plugin.RtvePlay.Rtve;

namespace Jellyfin.Plugin.RtvePlay.Library;

/// <summary>
/// Turns the RTVE program type into a Jellyfin genre so the library can be filtered by kind
/// («Serie», «Documental», «Infantil»...) with the native filters, without extra plugins.
/// </summary>
internal static class ProgramClassifier
{
    /// <summary>
    /// Program type ids seen in the RTVE catalog, mapped to the genre written to the NFO.
    /// </summary>
    /// <remarks>
    /// RTVE keeps the id stable when the label changes, so it is the preferred key. The name is
    /// only used as a fallback for types added after this build.
    /// </remarks>
    private static readonly Dictionary<string, string> GenreByTypeId = new(StringComparer.Ordinal)
    {
        ["132531"] = "Magacín",
        ["132532"] = "Entretenimiento",
        ["132533"] = "Magacín",
        ["132534"] = "Informativo",
        ["132535"] = "Serie",
        ["132536"] = "Documental",
        ["132537"] = "Informativo",
        ["132870"] = "Serie",
        ["132871"] = "Concurso",
        ["136519"] = "Serie",
        ["136520"] = "Serie",
        ["136521"] = "Documental",
        ["136522"] = "Especial",
        ["136523"] = "Serie",
        ["136524"] = "Concurso",
        ["136525"] = "Entrevistas",
        ["136526"] = "Entretenimiento",
        ["136527"] = "Cocina",
        ["136611"] = "Serie",
        ["136612"] = "Magacín",
        ["136613"] = "Música",
        ["137310"] = "Cine",
        ["137650"] = "Documental",
        ["141550"] = "Documental",
        ["145350"] = "Infantil",
        ["1001560"] = "Podcast"
    };

    /// <summary>
    /// Keywords of the type name, in matching order: "Serie Documental" has to end up as
    /// "Documental", so the specific kinds come before the generic "serie".
    /// </summary>
    private static readonly (string Keyword, string Genre)[] GenreByKeyword =
    [
        ("documental", "Documental"),
        ("factual", "Documental"),
        ("reportaje", "Documental"),
        ("infantil", "Infantil"),
        ("concurso", "Concurso"),
        ("talent", "Concurso"),
        ("informativo", "Informativo"),
        ("noticias", "Informativo"),
        ("magac", "Magacín"),
        ("pelicula", "Cine"),
        ("cine", "Cine"),
        ("podcast", "Podcast"),
        ("concierto", "Música"),
        ("musica", "Música"),
        ("receta", "Cocina"),
        ("entrevista", "Entrevistas"),
        ("late night", "Entretenimiento"),
        ("entretenimiento", "Entretenimiento"),
        ("especial", "Especial"),
        ("evento", "Especial"),
        ("serie", "Serie")
    ];

    /// <summary>
    /// Classifies a program with the data RTVE publishes.
    /// </summary>
    /// <param name="programTypeId">Value of <c>programTypeId</c>.</param>
    /// <param name="programType">Value of <c>programType</c>.</param>
    /// <param name="genres">RTVE categories («Cultura», «Humor»...).</param>
    /// <returns>The genres and tags to write to the <c>tvshow.nfo</c>.</returns>
    internal static ProgramClassification Classify(
        string? programTypeId,
        string? programType,
        IEnumerable<string?>? genres)
    {
        var kind = ResolveKind(programTypeId, programType);
        var values = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (kind is not null && seen.Add(kind))
        {
            values.Add(kind);
        }

        foreach (var candidate in genres ?? [])
        {
            var genre = HtmlText.FromHtml(candidate);
            if (genre is not null && seen.Add(genre))
            {
                values.Add(genre);
            }
        }

        var tags = new List<string>();
        var type = HtmlText.FromHtml(programType);
        if (type is not null)
        {
            tags.Add(type);
        }

        return new ProgramClassification(kind, values, tags);
    }

    /// <summary>
    /// Resolves the kind of program from its type id, falling back to the type name.
    /// </summary>
    /// <param name="programTypeId">Value of <c>programTypeId</c>.</param>
    /// <param name="programType">Value of <c>programType</c>.</param>
    /// <returns>The normalized kind, or <c>null</c> when RTVE publishes no known type.</returns>
    internal static string? ResolveKind(string? programTypeId, string? programType)
    {
        var id = programTypeId?.Trim();
        if (!string.IsNullOrEmpty(id) && GenreByTypeId.TryGetValue(id, out var kind))
        {
            return kind;
        }

        var name = Normalize(programType);
        if (name.Length == 0)
        {
            return null;
        }

        foreach (var (keyword, genre) in GenreByKeyword)
        {
            if (name.Contains(keyword, StringComparison.Ordinal))
            {
                return genre;
            }
        }

        return null;
    }

    /// <summary>
    /// Lower cases the text and strips the diacritics so «Magacín» matches the «magacin» keyword.
    /// </summary>
    /// <param name="value">Text to normalize.</param>
    /// <returns>The normalized text, or an empty string when there is no text.</returns>
    private static string Normalize(string? value)
        => TextFolding.Fold(HtmlText.FromHtml(value));
}

/// <summary>
/// Classification of an RTVE program derived from its type and categories.
/// </summary>
/// <param name="Kind">
/// Normalized kind of program («Serie», «Documental», «Infantil»...), or <c>null</c> when RTVE
/// publishes no known type for it.
/// </param>
/// <param name="Genres">Genres to write to the NFO: the kind first, then the RTVE categories.</param>
/// <param name="Tags">Tags to write to the NFO: the raw RTVE program type.</param>
internal sealed record ProgramClassification(string? Kind, IReadOnlyList<string> Genres, IReadOnlyList<string> Tags);
