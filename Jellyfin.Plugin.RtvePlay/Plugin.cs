using System.Globalization;
using Jellyfin.Plugin.RtvePlay.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.RtvePlay;

/// <summary>
/// The main plugin.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>
    /// The plugin identifier. Must match the <see cref="System.Runtime.InteropServices.GuidAttribute"/>
    /// applied to the assembly and the <c>guid</c> field in <c>build.yaml</c>.
    /// </summary>
    public const string PluginId = "3a7f2d61-9c4b-4e0e-b6f5-7d21c8a4e913";

    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
    /// <param name="xmlSerializer">Instance of the <see cref="IXmlSerializer"/> interface.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
        MigrateConfiguration(Configuration);
    }

    /// <summary>
    /// Gets the current plugin instance.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    /// <inheritdoc />
    public override string Name => "RTVE Play";

    /// <inheritdoc />
    public override string Description =>
        "Genera una biblioteca de series a partir del catálogo a la carta de RTVE Play (.strm + metadatos + subtítulos).";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse(PluginId);

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        return
        [
            new PluginPageInfo
            {
                Name = Name,
                EmbeddedResourcePath = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}.Configuration.configPage.html",
                    GetType().Namespace)
            }
        ];
    }

    /// <summary>
    /// Brings the stored settings forward to the current schema. Version 2 turns the DRM filter off:
    /// RTVE flags many videos as DRM protected but still serves the MP4/HLS unencrypted, so the old
    /// default silently dropped entire programs from the library.
    /// </summary>
    /// <param name="configuration">Stored configuration.</param>
    private void MigrateConfiguration(PluginConfiguration configuration)
    {
        if (configuration.ConfigurationVersion >= PluginConfiguration.CurrentVersion)
        {
            return;
        }

        configuration.FilterDrm = false;
        configuration.ConfigurationVersion = PluginConfiguration.CurrentVersion;
        UpdateConfiguration(configuration);
    }
}
