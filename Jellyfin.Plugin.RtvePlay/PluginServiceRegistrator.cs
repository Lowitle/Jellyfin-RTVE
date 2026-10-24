using Jellyfin.Plugin.RtvePlay.Rtve;
using Jellyfin.Plugin.RtvePlay.Sync;
using Jellyfin.Plugin.RtvePlay.Tasks;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.RtvePlay;

/// <summary>
/// Registers the plugin services in the Jellyfin container.
/// </summary>
public sealed class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <summary>
    /// User agent sent to RTVE.
    /// </summary>
    public const string UserAgent = "JellyfinRtvePlay/1.0 (+https://github.com/jellyfin)";

    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddHttpClient<RtveClient>(client => ConfigureClient(client, "https://www.rtve.es/api/"));
        serviceCollection.AddHttpClient<ZtnrResolver>(client => ConfigureClient(client, "https://www.rtve.es/"));

        serviceCollection.AddSingleton<RtveSyncService>();
        serviceCollection.AddSingleton<IScheduledTask, RtveRefreshTask>();
    }

    private static void ConfigureClient(HttpClient client, string baseAddress)
    {
        client.BaseAddress = new Uri(baseAddress, UriKind.Absolute);
        client.Timeout = TimeSpan.FromSeconds(100);
        client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json, text/plain, */*");
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("es-ES,es;q=0.9");
    }
}
