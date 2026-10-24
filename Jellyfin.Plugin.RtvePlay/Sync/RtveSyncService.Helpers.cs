namespace Jellyfin.Plugin.RtvePlay.Sync;

/// <summary>
/// Small text / date helpers.
/// </summary>
public sealed partial class RtveSyncService
{
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
}
