namespace VanDaemon.Plugins.Abstractions;

/// <summary>
/// Interface for sensor plugins that read values from hardware
/// </summary>
public interface ISensorPlugin : IHardwarePlugin
{
    /// <summary>
    /// Read a single sensor value
    /// </summary>
    /// <param name="sensorId">The identifier for the sensor</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The sensor value (typically 0-100 for percentage readings)</returns>
    Task<double> ReadValueAsync(string sensorId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Read all available sensor values
    /// </summary>
    Task<IDictionary<string, double>> ReadAllValuesAsync(CancellationToken cancellationToken = default);
}
