namespace VanDaemon.Plugins.Abstractions;

/// <summary>
/// Base interface for all hardware plugins
/// </summary>
public interface IHardwarePlugin : IDisposable
{
    /// <summary>
    /// Plugin name
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Plugin version
    /// </summary>
    string Version { get; }

    /// <summary>
    /// Initialize the plugin with configuration
    /// </summary>
    Task InitializeAsync(Dictionary<string, object> configuration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Test the connection to the hardware
    /// </summary>
    Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);
}
