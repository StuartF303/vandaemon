using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using VanDaemon.Plugins.Ui.Abstractions;
using VanDaemon.Plugins.Ui.Api;
using VanDaemon.Plugins.Ui.Bridge;
using VanDaemon.Plugins.Ui.Hosting;
using VanDaemon.Plugins.Ui.ReferencePlugin;

namespace VanDaemon.Plugins.Ui;

/// <summary>
/// DI wiring for the Tier-2 UI plugin seam: registry, off-device stub bridge, the typed API client,
/// and the compiled-in reference plugin.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the UI plugin registry, the no-op native bridge stub, the API client, and the
    /// reference UI plugin. Additional compiled-in plugins can be registered as
    /// <see cref="IUiPlugin"/> after calling this.
    /// </summary>
    public static IServiceCollection AddVanDaemonUiPlugins(this IServiceCollection services)
    {
        services.AddScoped<IVanDaemonApiClient, HttpVanDaemonApiClient>();
        // Resolved lazily (first injection into a component), by which point the Blazor WASM JS
        // runtime is up — so NativeBridgeFactory can synchronously probe for the native transport and
        // log which implementation it selected. Off-device this yields the no-op StubNativeBridge.
        services.AddSingleton<INativeBridge>(sp => NativeBridgeFactory.Create(
            sp.GetRequiredService<IJSRuntime>(),
            sp.GetRequiredService<ILoggerFactory>()));
        services.AddSingleton<IUiPlugin, SystemStatusUiPlugin>();
        services.AddSingleton<IUiPluginRegistry, UiPluginRegistry>();
        return services;
    }
}
