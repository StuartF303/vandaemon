namespace VanDaemon.Plugins.Ui.Abstractions;

/// <summary>
/// Discovers and enumerates registered <see cref="IUiPlugin"/> instances for the Blazor host.
/// Independent of the loading mechanism: plugins are simply registered, however they were obtained.
/// </summary>
public interface IUiPluginRegistry
{
    /// <summary>All registered plugins, ordered by <see cref="IUiPlugin.Order"/> then <see cref="IUiPlugin.Id"/>.</summary>
    IReadOnlyList<IUiPlugin> Plugins { get; }

    /// <summary>
    /// Register a plugin. Throws <see cref="InvalidOperationException"/> if a plugin with the same
    /// <see cref="IUiPlugin.Id"/> is already registered (no silent overwrite).
    /// </summary>
    void Register(IUiPlugin plugin);
}
