using System.Text.Json;
using System.Text.Json.Serialization;
using Jellyfin.Plugin.RtvePlay.Rtve;
using Xunit;

namespace Jellyfin.Plugin.RtvePlay.Tests;

/// <summary>
/// Tests of the converters that tolerate the numeric fields RTVE serializes as strings or as empty values.
/// </summary>
public class FlexibleNumberTests
{
    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData("{\"value\": 12}", 12)]
    [InlineData("{\"value\": \"12\"}", 12)]
    [InlineData("{\"value\": \" 12 \"}", 12)]
    [InlineData("{\"value\": 3.7}", 3)]
    [InlineData("{\"value\": \"3.9\"}", 3)]
    [InlineData("{\"value\": -4}", -4)]
    public void Int_ReadsNumbersAndNumericStrings(string json, int expected)
    {
        Assert.Equal(expected, JsonSerializer.Deserialize<IntModel>(json, WebOptions)!.Value);
    }

    [Theory]
    [InlineData("{\"value\": \"\"}")]
    [InlineData("{\"value\": \"   \"}")]
    [InlineData("{\"value\": null}")]
    [InlineData("{}")]
    [InlineData("{\"value\": \"abc\"}")]
    [InlineData("{\"value\": {\"nested\": 1}}")]
    [InlineData("{\"value\": [1, 2]}")]
    public void Int_ReturnsNullWhenThereIsNoNumber(string json)
    {
        Assert.Null(JsonSerializer.Deserialize<IntModel>(json, WebOptions)!.Value);
    }

    [Theory]
    [InlineData("{\"value\": 1769340733512}", 1769340733512L)]
    [InlineData("{\"value\": \"1769340733512\"}", 1769340733512L)]
    [InlineData("{\"value\": \"\"}", null)]
    public void Long_ReadsNumbersAndNumericStrings(string json, long? expected)
    {
        Assert.Equal(expected, JsonSerializer.Deserialize<LongModel>(json, WebOptions)!.Value);
    }

    private sealed class IntModel
    {
        [JsonConverter(typeof(FlexibleInt32Converter))]
        public int? Value { get; set; }
    }

    private sealed class LongModel
    {
        [JsonConverter(typeof(FlexibleInt64Converter))]
        public long? Value { get; set; }
    }
}
