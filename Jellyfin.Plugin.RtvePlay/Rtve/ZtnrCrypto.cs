using System.Buffers.Binary;
using System.Text;

namespace Jellyfin.Plugin.RtvePlay.Rtve;

/// <summary>
/// Decryption of the "encrypted PNG" published by ZTNR.
/// Port of the <c>_decrypt_url</c>, <c>_get_alphabet</c> and <c>_get_url</c> functions of yt-dlp's RTVE
/// extractor, which is a reimplementation of <c>https://js2.rtve.es/pages/app-player/3.5.1/js/pf_video.js</c>.
/// </summary>
public static class ZtnrCrypto
{
    /// <summary>
    /// Extracts the playback URLs from a base64 encoded PNG.
    /// </summary>
    /// <param name="pngBase64">Content of the PNG, as base64 text.</param>
    /// <returns>The decrypted streams.</returns>
    /// <exception cref="FormatException">Thrown when the payload is not valid base64.</exception>
    public static IReadOnlyList<ZtnrStream> Decrypt(string pngBase64)
    {
        ArgumentNullException.ThrowIfNull(pngBase64);

        var bytes = Convert.FromBase64String(pngBase64.Trim());
        var result = new List<ZtnrStream>();

        foreach (var chunk in ReadTextChunks(bytes))
        {
            // bytes(filter(None, data)): the chunk is NUL padded.
            var data = Array.FindAll(chunk, b => b != 0);
            var hash = Array.IndexOf(data, (byte)'#');
            if (hash < 0)
            {
                continue;
            }

            var alphabet = BuildAlphabet(Encoding.Latin1.GetString(data, 0, hash));
            if (alphabet.Length == 0)
            {
                continue;
            }

            var quality = string.Empty;
            var urlData = Encoding.Latin1.GetString(data, hash + 1, data.Length - hash - 1);

            // rpartition(b'%%'): when the separator is missing, the whole text is the URL data.
            var separator = urlData.LastIndexOf("%%", StringComparison.Ordinal);
            if (separator >= 0)
            {
                quality = urlData[..separator];
                urlData = urlData[(separator + 2)..];
            }

            var url = DecryptUrl(alphabet, urlData);
            if (!string.IsNullOrWhiteSpace(url))
            {
                result.Add(new ZtnrStream(quality, url));
            }
        }

        return result;
    }

    /// <summary>
    /// Rebuilds the shuffled alphabet from its interleaved representation.
    /// </summary>
    /// <param name="data">Raw alphabet data.</param>
    /// <returns>The alphabet.</returns>
    public static string BuildAlphabet(string data)
    {
        ArgumentNullException.ThrowIfNull(data);

        var builder = new StringBuilder(data.Length);
        var e = 0;
        var d = 0;
        foreach (var character in data)
        {
            if (d == 0)
            {
                builder.Append(character);
                e = (e + 1) % 4;
                d = e;
            }
            else
            {
                d--;
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// Decrypts the URL data using the alphabet.
    /// </summary>
    /// <param name="alphabet">The alphabet.</param>
    /// <param name="data">The encrypted digits.</param>
    /// <returns>The URL, or an empty string when the payload is not well formed.</returns>
    public static string DecryptUrl(string alphabet, string data)
    {
        ArgumentNullException.ThrowIfNull(alphabet);
        ArgumentNullException.ThrowIfNull(data);

        var builder = new StringBuilder(data.Length / 2);
        var inTens = false;
        var e = 3;
        var b = 1;
        var index = 0;

        foreach (var character in data)
        {
            if (character is < '0' or > '9')
            {
                // Corrupted payload: never return a half decrypted URL.
                return string.Empty;
            }

            var digit = character - '0';
            if (!inTens)
            {
                index = digit * 10;
                inTens = true;
            }
            else if (e == 0)
            {
                index += digit;
                if (index >= alphabet.Length)
                {
                    return string.Empty;
                }

                builder.Append(alphabet[index]);
                e = (b + 3) % 4;
                inTens = false;
                b++;
            }
            else
            {
                e--;
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// Gets the container (extension) of a URL, ignoring the query string.
    /// </summary>
    /// <param name="url">The URL.</param>
    /// <returns>The lower case extension without the dot, or an empty string.</returns>
    public static string GetContainer(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return string.Empty;
        }

        var end = url.IndexOfAny(['?', '#']);
        var path = end >= 0 ? url[..end] : url;
        var dot = path.LastIndexOf('.');
        return dot < 0 || dot == path.Length - 1
            ? string.Empty
            : path[(dot + 1)..].ToLowerInvariant();
    }

    internal static IEnumerable<byte[]> ReadTextChunks(byte[] png)
    {
        if (png.Length < 8)
        {
            yield break;
        }

        var offset = 8;
        while (offset + 8 <= png.Length)
        {
            var length = BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(offset, 4));
            var type = Encoding.ASCII.GetString(png, offset + 4, 4);
            var dataStart = offset + 8;
            if (length > int.MaxValue || dataStart + (long)length > png.Length)
            {
                yield break;
            }

            if (type == "tEXt")
            {
                yield return png[dataStart..(dataStart + (int)length)];
            }
            else if (type == "IEND")
            {
                yield break;
            }

            offset = dataStart + (int)length + 4;
        }
    }
}
