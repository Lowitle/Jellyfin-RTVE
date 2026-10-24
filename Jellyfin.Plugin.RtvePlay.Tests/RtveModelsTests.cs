using System.Text.Json;
using Jellyfin.Plugin.RtvePlay.Rtve;
using Xunit;

namespace Jellyfin.Plugin.RtvePlay.Tests;

/// <summary>
/// Tests that the RTVE models survive the real payload shapes of the public API.
/// </summary>
public class RtveModelsTests
{
    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);

    // Trimmed copy of https://www.rtve.es/api/programas/111570/videos?type=39816&page=1&size=1.
    // The qualities[].identifier is a JSON number and used to break the whole page deserialization.
    private const string VideoJson = """
        {
          "page": {
            "items": [
              {
                "id": "16909076",
                "title": "Saber vivir - 25/01/26",
                "longTitle": "Saber vivir - 25/01/26",
                "thumbnail": "https://img.rtve.es/imagenes/saber-vivir-250126/01769340848350.jpg",
                "duration": 2095560,
                "publicationDateTimestamp": 1769338800000,
                "episode": 0,
                "temporada": null,
                "temporadaOrden": null,
                "hasDRM": false,
                "requireLogged": true,
                "geolocalizado": false,
                "director": "Alfonso García | Jero Fernández",
                "programInfo": { "id": "111570", "title": "Saber vivir" },
                "generos": [ { "generoInf": "Ciencia y futuro", "generoInfUid": "GE_CIENFU" } ],
                "qualities": [
                  {
                    "identifier": 8124123,
                    "filePath": "https://ztnr.rtve.es/ztnr/res/_PROVIDER_/video/hd_full/_TOKEN_",
                    "preset": "HD_FULL",
                    "filesize": 1105780306,
                    "type": "application/mp4",
                    "duration": 2095560,
                    "height": 1080,
                    "width": 1920
                  }
                ]
              }
            ],
            "total": 1,
            "totalPages": 1,
            "number": 1,
            "size": 100
          }
        }
        """;

    [Fact]
    public void VideoPage_WithNumericQualityIdentifier_Deserializes()
    {
        var envelope = JsonSerializer.Deserialize<RtveEnvelope<RtveVideo>>(VideoJson, WebOptions);

        var video = Assert.Single(envelope!.Page!.Items!);
        Assert.Equal("16909076", video.Id);
        Assert.Equal("Saber vivir - 25/01/26", video.Title);
        Assert.Equal(0, video.Episode);
        Assert.Null(video.SeasonNumber);
        Assert.False(video.HasDrm);
        Assert.True(video.RequireLogged);
        Assert.Equal(2095560, video.Duration);
        Assert.Equal(1769338800000L, video.PublicationDateTimestamp);
        Assert.Equal("111570", video.ProgramInfo!.Id);

        var quality = Assert.Single(video.Qualities!);
        Assert.Equal("8124123", quality.Identifier);
        Assert.Equal("HD_FULL", quality.Preset);
        Assert.Equal(1105780306L, quality.FileSize);

        var genre = Assert.Single(video.Genres!);
        Assert.Equal("Ciencia y futuro", genre.Name);
    }

    [Fact]
    public void ProgramPage_WithStringIds_Deserializes()
    {
        const string json = """
            {
              "page": {
                "items": [
                  {
                    "id": "111570",
                    "title": "Saber vivir",
                    "htmlUrl": "https://www.rtve.es/play/videos/saber-vivir/",
                    "imgPoster": "https://img.rtve.es/imagenes/saber-vivir/01759842836347.jpg",
                    "imgPortada": "https://img.rtve.es/imagenes/saber-vivir/01759842856982.jpg",
                    "imgBackground": "https://img.rtve.es/imagenes/saber-vivir/01759842813768.jpg",
                    "longDescription": "<p>Saber Vivir es el programa matinal sobre salud.</p>"
                  }
                ],
                "total": 1,
                "totalPages": 1,
                "number": 1,
                "size": 100
              }
            }
            """;

        var program = Assert.Single(JsonSerializer.Deserialize<RtveEnvelope<RtveProgram>>(json, WebOptions)!.Page!.Items!);

        Assert.Equal("111570", program.Id);
        Assert.Equal("Saber vivir", program.Title);
        Assert.EndsWith("01759842836347.jpg", program.PosterImage, StringComparison.Ordinal);
        Assert.EndsWith("01759842856982.jpg", program.CoverImage, StringComparison.Ordinal);
        Assert.EndsWith("01759842813768.jpg", program.BackgroundImage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{\"value\": 8124123}", "8124123")]
    [InlineData("{\"value\": \"8124123\"}", "8124123")]
    [InlineData("{\"value\": 12.5}", "12.5")]
    [InlineData("{\"value\": null}", null)]
    [InlineData("{\"value\": \"  \"}", null)]
    [InlineData("{\"value\": {\"nested\": 1}}", null)]
    public void FlexibleString_ReadsNumbersAndStrings(string json, string? expected)
    {
        Assert.Equal(expected, JsonSerializer.Deserialize<StringModel>(json, WebOptions)!.Value);
    }

    private sealed class StringModel
    {
        [System.Text.Json.Serialization.JsonConverter(typeof(FlexibleStringConverter))]
        public string? Value { get; set; }
    }
}
