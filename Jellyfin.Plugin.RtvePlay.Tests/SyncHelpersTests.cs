using Jellyfin.Plugin.RtvePlay.Sync;
using Xunit;

namespace Jellyfin.Plugin.RtvePlay.Tests;

/// <summary>
/// Tests of the helpers shared by the program and episode pipelines.
/// </summary>
public class SyncHelpersTests
{
    [Theory]
    [InlineData("es", "es")]
    [InlineData("ES", "es")]
    [InlineData(" en ", "en")]
    [InlineData("ca", "ca")]
    [InlineData("gl", "gl")]
    [InlineData("eu", "eu")]
    public void NormalizeLanguage_KeepsTheIsoCode(string configured, string expected)
    {
        Assert.Equal(expected, RtveSyncService.NormalizeLanguage(configured));
    }

    [Theory]
    [InlineData("none")]
    [InlineData("NONE")]
    [InlineData("ninguno")]
    [InlineData("disabled")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void NormalizeLanguage_ReturnsNullWhenSubtitlesAreDisabled(string? configured)
    {
        Assert.Null(RtveSyncService.NormalizeLanguage(configured));
    }
}
