namespace Jellyfin.Plugin.RtvePlay.Rtve;

/// <summary>
/// A playback URL resolved from the ZTNR thumbnail PNG.
/// </summary>
/// <param name="Quality">Quality ("preset") reported by the PNG. May be empty.</param>
/// <param name="Url">Playback URL.</param>
public sealed record ZtnrStream(string Quality, string Url)
{
    /// <summary>
    /// Gets the container implied by the URL (for instance "m3u8", "mpd" or "mp4").
    /// </summary>
    public string Container => ZtnrCrypto.GetContainer(Url);
}
