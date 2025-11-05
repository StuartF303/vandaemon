using Microsoft.Extensions.Logging;
using VanDaemon.Plugins.Abstractions;

namespace VanDaemon.Plugins.Simulated;

/// <summary>
/// Simulated sensor plugin for testing and development
/// Generates realistic-looking sensor data without requiring actual hardware
/// </summary>
public class SimulatedSensorPlugin : ISensorPlugin
{
    private readonly ILogger<SimulatedSensorPlugin> _logger;
    private readonly Dictionary<string, double> _sensorValues = new();
    private readonly Random _random = new();
    private bool _disposed;

    public string Name => "Simulated Sensor Plugin";
    public string Version => "1.0.0";

    public SimulatedSensorPlugin(ILogger<SimulatedSensorPlugin> logger)
    {
        _logger = logger;
    }

    public Task InitializeAsync(Dictionary<string, object> configuration, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Initializing {PluginName} v{Version}", Name, Version);

        // Initialize some default sensor values
        _sensorValues["fresh_water"] = 75.0;
        _sensorValues["waste_water"] = 25.0;
        _sensorValues["lpg"] = 60.0;
        _sensorValues["fuel"] = 80.0;
        _sensorValues["battery"] = 95.0;

        return Task.CompletedTask;
    }

    public Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Testing connection for {PluginName}", Name);
        return Task.FromResult(true);
    }

    public Task<double> ReadValueAsync(string sensorId, CancellationToken cancellationToken = default)
    {
        if (!_sensorValues.ContainsKey(sensorId))
        {
            _logger.LogWarning("Sensor {SensorId} not found, returning 0", sensorId);
            return Task.FromResult(0.0);
        }

        // Add some random variation to simulate real sensor readings
        var baseValue = _sensorValues[sensorId];
        var variation = (_random.NextDouble() - 0.5) * 2; // +/- 1%
        var value = Math.Clamp(baseValue + variation, 0, 100);

        // Simulate gradual changes over time
        if (sensorId == "fresh_water" || sensorId == "lpg" || sensorId == "fuel")
        {
            _sensorValues[sensorId] = Math.Max(0, value - 0.001); // Slowly decrease
        }
        else if (sensorId == "waste_water")
        {
            _sensorValues[sensorId] = Math.Min(100, value + 0.001); // Slowly increase
        }

        _logger.LogDebug("Read sensor {SensorId}: {Value}%", sensorId, value);
        return Task.FromResult(value);
    }

    public Task<IDictionary<string, double>> ReadAllValuesAsync(CancellationToken cancellationToken = default)
    {
        var values = new Dictionary<string, double>();
        foreach (var sensor in _sensorValues.Keys)
        {
            values[sensor] = ReadValueAsync(sensor, cancellationToken).Result;
        }

        _logger.LogDebug("Read all sensor values: {Count} sensors", values.Count);
        return Task.FromResult<IDictionary<string, double>>(values);
    }

    public void Dispose()
    {
        if (_disposed) return;

        _logger.LogInformation("Disposing {PluginName}", Name);
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
