using System.Globalization;
using System.Text;

namespace Jellyfin.Plugin.RtvePlay.Library;

/// <summary>
/// Folds text to a comparable form: lower case, no diacritics, no extra spaces. Used to match the
/// RTVE labels («Magacín», «PELÍCULA») against the keywords of this plugin without caring about the
/// accents RTVE adds or drops between releases.
/// </summary>
internal static class TextFolding
{
    /// <summary>
    /// Folds a value.
    /// </summary>
    /// <param name="value">Text to fold.</param>
    /// <returns>The folded text, or an empty string when there is no text.</returns>
    internal static string Fold(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        foreach (var character in value.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.ToString().Trim();
    }
}
