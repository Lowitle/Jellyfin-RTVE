using Jellyfin.Plugin.RtvePlay.Rtve;
using Xunit;

namespace Jellyfin.Plugin.RtvePlay.Tests;

/// <summary>
/// Tests of the WebVTT to SubRip conversion.
/// </summary>
public class VttToSrtTests
{
    private const string Sample = """
        WEBVTT
        X-TIMESTAMP-MAP=MPEGTS:900000,LOCAL:00:00:00.000

        NOTE Este bloque se ignora

        1
        00:00:01.000 --> 00:00:03.500 align:start position:0%
        <c>Hola</c> <00:00:02.000>mundo

        00:00:04.5 --> 00:00:06.000
        Segunda l&iacute;nea
        con <b>negrita</b> y <i>cursiva</i>

        """;

    [Fact]
    public void Convert_RealWorldDocument_ReturnsCuesInSubRipFormat()
    {
        var expected = string.Join(
            '\n',
            "1",
            "00:00:01,000 --> 00:00:03,500",
            "Hola mundo",
            string.Empty,
            "2",
            "00:00:04,500 --> 00:00:06,000",
            "Segunda línea",
            "con negrita y cursiva",
            string.Empty,
            string.Empty);

        Assert.Equal(expected, VttToSrt.Convert(Sample));
    }

    [Theory]
    [InlineData("WEBVTT\n\n")]
    [InlineData("WEBVTT\n\nNOTE solo un comentario\n\n")]
    [InlineData("WEBVTT\n\n00:00:01.000 --> 00:00:02.000\n\n")]
    public void Convert_DocumentWithoutText_ReturnsEmpty(string vtt)
    {
        Assert.Equal(string.Empty, VttToSrt.Convert(vtt));
    }

    [Theory]
    [InlineData("00:05.5 --> 00:07.25", "00:00:05,500 --> 00:00:07,250")]
    [InlineData("01:02:03.25 --> 01:02:04.5", "01:02:03,250 --> 01:02:04,500")]
    [InlineData("00:00:01.000 --> 00:00:02.000", "00:00:01,000 --> 00:00:02,000")]
    public void Convert_NormalizesTimestamps(string timing, string expectedTiming)
    {
        var srt = VttToSrt.Convert($"WEBVTT\n\n{timing}\nTexto\n");

        Assert.Contains(expectedTiming, srt, StringComparison.Ordinal);
    }

    [Fact]
    public void Convert_DecodesEntitiesAndKeepsPlainText()
    {
        var srt = VttToSrt.Convert("WEBVTT\n\n00:00:01.000 --> 00:00:02.000\nA &amp; B &quot;C&quot; &gt; D\n");

        Assert.Equal("1\n00:00:01,000 --> 00:00:02,000\nA & B \"C\" > D\n\n", srt);
    }

    [Fact]
    public void ConvertToFile_WritesUtf8WithoutBomAndCrlf()
    {
        var path = Path.Combine(Path.GetTempPath(), $"rtve-{Guid.NewGuid():N}.srt");
        try
        {
            Assert.True(VttToSrt.ConvertToFile("WEBVTT\n\n00:00:01.000 --> 00:00:02.000\nHola\n", path));

            var bytes = File.ReadAllBytes(path);
            Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
            Assert.Equal("1\r\n00:00:01,000 --> 00:00:02,000\r\nHola\r\n\r\n", File.ReadAllText(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ConvertToFile_WithoutCues_DoesNotCreateTheFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"rtve-{Guid.NewGuid():N}.srt");
        Assert.False(VttToSrt.ConvertToFile("WEBVTT\n\n", path));
        Assert.False(File.Exists(path));
    }
}
