using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.RtvePlay.Rtve;

/// <summary>
/// Reads an integer that RTVE sometimes serializes as a number, as a string or as an empty value.
/// </summary>
public sealed class FlexibleInt32Converter : JsonConverter<int?>
{
    /// <inheritdoc />
    public override int? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = FlexibleNumber.ReadInt64(ref reader);
        return value.HasValue ? (int)value.Value : null;
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, int? value, JsonSerializerOptions options)
    {
        if (value.HasValue)
        {
            writer.WriteNumberValue(value.Value);
        }
        else
        {
            writer.WriteNullValue();
        }
    }
}

/// <summary>
/// Reads a long that RTVE sometimes serializes as a number, as a string or as an empty value.
/// </summary>
public sealed class FlexibleInt64Converter : JsonConverter<long?>
{
    /// <inheritdoc />
    public override long? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => FlexibleNumber.ReadInt64(ref reader);

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, long? value, JsonSerializerOptions options)
    {
        if (value.HasValue)
        {
            writer.WriteNumberValue(value.Value);
        }
        else
        {
            writer.WriteNullValue();
        }
    }
}

/// <summary>
/// Reads a text field that RTVE sometimes serializes as a number (for instance the
/// <c>identifier</c> of a stream quality) and treats empty strings as <c>null</c>.
/// </summary>
public sealed class FlexibleStringConverter : JsonConverter<string?>
{
    /// <inheritdoc />
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return null;
            case JsonTokenType.String:
                var text = reader.GetString();
                return string.IsNullOrWhiteSpace(text) ? null : text;
            case JsonTokenType.Number:
                if (reader.TryGetInt64(out var integer))
                {
                    return integer.ToString(CultureInfo.InvariantCulture);
                }

                return reader.TryGetDecimal(out var real)
                    ? real.ToString(CultureInfo.InvariantCulture)
                    : reader.GetDouble().ToString(CultureInfo.InvariantCulture);
            default:
                reader.Skip();
                return null;
        }
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteStringValue(value);
        }
    }
}

internal static class FlexibleNumber
{
    internal static long? ReadInt64(ref Utf8JsonReader reader)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return null;
            case JsonTokenType.Number:
                return reader.TryGetInt64(out var number) ? number : (long)reader.GetDouble();
            case JsonTokenType.String:
                var text = reader.GetString();
                if (string.IsNullOrWhiteSpace(text))
                {
                    return null;
                }

                if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                {
                    return parsed;
                }

                return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var real)
                    ? (long)real
                    : null;
            default:
                reader.Skip();
                return null;
        }
    }
}
