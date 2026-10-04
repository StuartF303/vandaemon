using MudBlazor;
using VanDaemon.Plugins.Ui.Abstractions;

namespace VanDaemon.Plugins.Ui.ReferencePlugin;

/// <summary>
/// Reference <see cref="IUiPlugin"/> descriptor pointing the host at <see cref="SystemStatusTile"/>.
/// Proves the contract end-to-end against the existing API and the stub bridge.
/// </summary>
public sealed class SystemStatusUiPlugin : IUiPlugin
{
    public string Id => "system-status";
    public string Name => "System Status";
    public Type ComponentType => typeof(SystemStatusTile);
    public string? Icon => Icons.Material.Filled.Dashboard;
    public int Order => 0;
}
