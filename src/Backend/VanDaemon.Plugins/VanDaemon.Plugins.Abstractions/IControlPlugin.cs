namespace VanDaemon.Plugins.Abstractions;

/// <summary>
/// Interface for control plugins that can actuate hardware
/// </summary>
public interface IControlPlugin : IHardwarePlugin
{
    /// <summary>
    /// Set the state of a control
    /// </summary>
    /// <param name="controlId">The identifier for the control</param>
    /// <param name="state">The desired state (bool for on/off, int for dimmer level, etc.)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the state was successfully set</returns>
    Task<bool> SetStateAsync(string controlId, object state, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the current state of a control
    /// </summary>
    Task<object> GetStateAsync(string controlId, CancellationToken cancellationToken = default);
}
