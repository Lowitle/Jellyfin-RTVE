using Jellyfin.Plugin.RtvePlay.Configuration;
using Jellyfin.Plugin.RtvePlay.Library;
using Jellyfin.Plugin.RtvePlay.Rtve;

namespace Jellyfin.Plugin.RtvePlay.Sync;

/// <summary>
/// Small text / date helpers.
/// </summary>
public sealed partial class RtveSyncService
{
    /// <summary>
    /// Builds the series metadata Jellyfin uses to classify the program. The RTVE type becomes a
    /// genre («Serie», «Documental», «Infantil»...) so the library can be filtered by kind, and the
    /// raw type is kept as a tag.
    /// </summary>
    /// <param name="configuration">Plugin configuration.</param>
    /// <param name="program">Program metadata, when RTVE returned it.</param>
    /// <param name="programId">RTVE program id.</param>
    /// <param name="seriesTitle">Series title.</param>
    /// <param name="seriesPlot">Series plot.</param>
    /// <returns>The series metadata.</returns>
    private static LibrarySeries BuildSeriesMetadata(
        PluginConfiguration configuration,
        RtveProgram? program,
        string programId,
        string seriesTitle,
        string? seriesPlot)
    {
        var classification = ProgramClassifier.Classify(
            program?.ProgramTypeId,
            program?.ProgramType,
            program?.Genres?.Select(genre => genre.Name));

        return new LibrarySeries
        {
            Title = seriesTitle,
            ProgramId = programId,
            Plot = seriesPlot,
            Genres = classification.Genres,
            Tags = classification.Tags,
            ImdbId = configuration.WriteImdbIds ? NormalizeImdbId(program?.ImdbId) : null,
            Country = program?.Country
        };
    }

    /// <summary>
    /// Keeps the IMDb id only when RTVE publishes a well formed one ("tt0375434").
    /// </summary>
    /// <param name="value">Value of <c>idImdb</c>.</param>
    /// <returns>The id, or <c>null</c> when it cannot be used as a Jellyfin provider id.</returns>
    internal static string? NormalizeImdbId(string? value)
    {
        var id = value?.Trim();
        if (string.IsNullOrEmpty(id)
            || id.Length < 3
            || !id.StartsWith("tt", StringComparison.OrdinalIgnoreCase)
            || !id.All(char.IsAsciiLetterOrDigit))
        {
            return null;
        }

        return id.ToLowerInvariant();
    }

    private static string FirstNonEmpty(params string?[] values)
        => FirstNonEmptyOrNull(values) ?? string.Empty;

    private static string? FirstNonEmptyOrNull(params string?[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();

    /// <summary>
    /// Removes the repeated series name from an episode title ("Saber vivir - 12/10/26" → "12/10/26").
    /// </summary>
    /// <param name="title">Episode title.</param>
    /// <param name="seriesTitle">Series title.</param>
    /// <returns>The title without the series name.</returns>
    private static string StripSeriesPrefix(string title, string seriesTitle)
    {
        if (seriesTitle.Length > 0
            && title.Length > seriesTitle.Length
            && title.StartsWith(seriesTitle, StringComparison.OrdinalIgnoreCase))
        {
            var rest = title[seriesTitle.Length..].TrimStart(' ', '-', ':', '|', '–', '—');
            if (rest.Length > 0)
            {
                return rest;
            }
        }

        return title;
    }

    /// <summary>
    /// Converts a Unix timestamp in milliseconds to a local date.
    /// </summary>
    /// <param name="unixMilliseconds">Timestamp.</param>
    /// <returns>The date, or <c>null</c> when the value is missing or out of range.</returns>
    private static DateTimeOffset? ToDate(long? unixMilliseconds)
    {
        if (unixMilliseconds is not > 0)
        {
            return null;
        }

        try
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(unixMilliseconds.Value).ToLocalTime();
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    /// <summary>
    /// Normalizes a subtitle language configuration value.
    /// </summary>
    /// <param name="value">Configured value.</param>
    /// <returns>An ISO language code, or <c>null</c> when subtitles are disabled.</returns>
    internal static string? NormalizeLanguage(string? value)
    {
        var language = value?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(language))
        {
            return null;
        }

        return language switch
        {
            "none" or "ninguno" or "disabled" => null,
            _ => language
        };
    }

    /// <summary>
    /// Normalizes the list of subtitle languages selected in the configuration.
    /// </summary>
    /// <param name="configured">Languages selected with the check boxes.</param>
    /// <param name="legacy">Single language of plugin versions before 1.0.1.0 (migration fallback).</param>
    /// <returns>The distinct ISO codes to download, in the configured order.</returns>
    internal static IReadOnlyList<string> NormalizeLanguages(IEnumerable<string>? configured, string? legacy = null)
    {
        var languages = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var value in configured ?? [])
        {
            var language = NormalizeLanguage(value);
            if (language is not null && seen.Add(language))
            {
                languages.Add(language);
            }
        }

        if (languages.Count == 0)
        {
            var language = NormalizeLanguage(legacy);
            if (language is not null)
            {
                languages.Add(language);
            }
        }

        return languages;
    }
}
