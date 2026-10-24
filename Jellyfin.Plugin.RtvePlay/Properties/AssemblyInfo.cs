using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// The plugin id is used by Jellyfin to locate the plugin data folder and the configuration file.
[assembly: Guid("3a7f2d61-9c4b-4e0e-b6f5-7d21c8a4e913")]

// The unit tests exercise the internal helpers (NfoBuilder, HtmlText, FlexibleNumber...).
[assembly: InternalsVisibleTo("Jellyfin.Plugin.RtvePlay.Tests")]
