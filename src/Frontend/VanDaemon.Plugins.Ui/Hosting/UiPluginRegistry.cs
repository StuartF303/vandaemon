using Microsoft.Extensions.Logging;
using VanDaemon.Plugins.Ui.Abstractions;

namespace VanDaemon.Plugins.Ui.Hosting;

/// <summary>
/// In-memory <see cref="IUiPluginRegistry"/>. Plugins registered (compiled-in) via DI are enumerated
/// ordered by <see cref="IUiPlugin.Order"/> then <see cref="IUiPlugin.Id"/>. Duplicate ids are rejected.
/// </summary>
public sealed class UiPluginRegistry : IUiPluginRegistry
{
    private readonly List<IUiPlugin> _plugins = new();
    private readonly HashSet<string> _ids = new(StringComparer.Ordinal);
    private readonly ILogger<UiPluginRegistry> _logger;

    /// <summary>
    /// Creates the registry and registers any compiled-in plugins supplied through DI.
    /// </summary>
    public UiPluginRegistry(IEnumerable<IUiPlugin> plugins, ILogger<UiPluginRegistry> logger)
    {
        _logger = logger;
        foreach (var plugin in plugins)
        {
            Register(plugin);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<IUiPlugin> Plugins =>
        _plugins
            .OrderBy(p => p.Order)
            .ThenBy(p => p.Id, StringComparer.Ordinal)
            .ToList();

    /// <inheritdoc />
    public void Register(IUiPlugin plugin)
    {
        ArgumentNullException.ThrowIfNull(plugin);

        if (string.IsNullOrWhiteSpace(plugin.Id))
        {
            throw new InvalidOperationException("UI plugin Id must be non-empty.");
        }

        if (!_ids.Add(plugin.Id))
        {
            _logger.LogError("Duplicate UI plugin id {PluginId} rejected", plugin.Id);
            throw new InvalidOperationException($"A UI plugin with id '{plugin.Id}' is already registered.");
        }

        _plugins.Add(plugin);
        _logger.LogInformation("Registered UI plugin {PluginId} ({PluginName})", plugin.Id, plugin.Name);
    }
}
