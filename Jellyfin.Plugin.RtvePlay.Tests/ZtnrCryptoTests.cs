using Jellyfin.Plugin.RtvePlay.Rtve;
using Xunit;

namespace Jellyfin.Plugin.RtvePlay.Tests;

/// <summary>
/// Tests of the ZTNR decryption, using the PNG downloaded from a real RTVE video (16909076).
/// </summary>
public class ZtnrCryptoTests
{
    private const string HlsUrl =
        "https://rtvehlsvodlote7.rtve.es/mediavodv2/resources/TE_SVVR876/mp4/2/1/1769340733512.mp4/video.m3u8?hls_no_audio_only=true&hls_client_manifest_version=3&idasset=16909076";

    private const string DashUrl =
        "https://rtvehlsvodlote7.rtve.es/mediavodv2/resources/TE_SVVR876/mp4/2/1/1769340733512.mp4/video.mpd?idasset=16909076";

    private const string Mp4Url =
        "http://rtve-mediavod-lote3.rtve.es/resources/TE_SVVR876/mp4/2/1/1769340733512.mp4?idasset=16909076";

    [Fact]
    public void Decrypt_RealPng_ReturnsTheThreeUrls()
    {
        var streams = ZtnrCrypto.Decrypt(LoadFixture());

        Assert.Equal(3, streams.Count);
        Assert.Equal([HlsUrl, DashUrl, Mp4Url], streams.Select(s => s.Url));
        Assert.All(streams, s => Assert.Equal(string.Empty, s.Quality));
    }

    [Fact]
    public void Decrypt_RealPng_ReportsTheContainerOfEachUrl()
    {
        var containers = ZtnrCrypto.Decrypt(LoadFixture()).Select(s => s.Container).ToList();

        Assert.Equal(["m3u8", "mpd", "mp4"], containers);
    }

    [Fact]
    public void Decrypt_InvalidBase64_Throws()
    {
        Assert.Throws<FormatException>(() => ZtnrCrypto.Decrypt("esto no es base64!"));
    }

    [Fact]
    public void Decrypt_Base64WithoutChunks_ReturnsNoStreams()
    {
        Assert.Empty(ZtnrCrypto.Decrypt(Convert.ToBase64String([])));
    }

    [Fact]
    public void BuildAlphabet_RebuildsTheInterleavedAlphabet()
    {
        // The alphabet is stored with 0, 1, 2 or 3 filler characters between the real ones.
        Assert.Equal("ACFJK", ZtnrCrypto.BuildAlphabet("ABCDEFGHIJK"));
        Assert.Equal(string.Empty, ZtnrCrypto.BuildAlphabet(string.Empty));
    }

    [Fact]
    public void DecryptUrl_RejectsNonDigitPayloads()
    {
        Assert.Equal(string.Empty, ZtnrCrypto.DecryptUrl("0123456789", "12ab"));
    }

    [Theory]
    [InlineData("https://host/path/video.m3u8?idasset=1", "m3u8")]
    [InlineData("https://host/path/video.MP4", "mp4")]
    [InlineData("https://host/path/video", "")]
    [InlineData("https://host/path/video.", "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    public void GetContainer_ReturnsTheExtensionWithoutQueryString(string url, string expected)
    {
        Assert.Equal(expected, ZtnrCrypto.GetContainer(url));
    }

    private static string LoadFixture()
        => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "ztnr-16909076.png.b64"));
}
