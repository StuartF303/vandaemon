using Microsoft.Extensions.Logging;
using VanDaemon.Plugins.Abstractions;

namespace VanDaemon.Plugins.Simulated;

/// <summary>
/// Simulated sensor plugin for testing and development
/// Generates realistic-looking sensor data without requiring actual hardware
/// Tanks drain or fill over a 2-minute period then reset
/// </summary>
public class SimulatedSensorPlugin : ISensorPlugin
{
    private readonly ILogger<SimulatedSensorPlugin> _logger;
    private readonly Dictionary<string, DateTime> _sensorStartTimes = new();
    private readonly Dictionary<string, bool> _sensorDirection = new(); // true = draining, false = filling
    private readonly Random _random = new();
    private bool _disposed;
    private const double CYCLE_DURATION_SECONDS = 120.0; // 2 minutes

    public string Name => "Simulated Sensor Plugin";
    public string Version => "1.0.0";

    public SimulatedSensorPlugin(ILogger<SimulatedSensorPlugin> logger)
    {
        _logger = logger;
    }

    public Task InitializeAsync(Dictionary<string, object> configuration, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Initializing {PluginName} v{Version} - Tanks will drain/fill over 2-minute cycles", Name, Version);

        var now = DateTime.UtcNow;

        // Initialize sensor start times and directions
        // Draining tanks: fresh_water, lpg, fuel, battery
        _sensorStartTimes["fresh_water"] = now;
        _sensorDirection["fresh_water"] = true; // draining

        _sensorStartTimes["lpg"] = now;
        _sensorDirection["lpg"] = true; // draining

        _sensorStartTimes["fuel"] = now;
        _sensorDirection["fuel"] = true; // draining

        _sensorStartTimes["battery"] = now;
        _sensorDirection["battery"] = true; // draining

        // Filling tank: waste_water
        _sensorStartTimes["waste_water"] = now;
        _sensorDirection["waste_water"] = false; // filling

        return Task.CompletedTask;
    }

    public Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Testing connection for {PluginName}", Name);
        return Task.FromResult(true);
    }

    public Task<double> ReadValueAsync(string sensorId, CancellationToken cancellationToken = default)
    {
        if (!_sensorStartTimes.ContainsKey(sensorId))
        {
            _logger.LogWarning("Sensor {SensorId} not found, returning 0", sensorId);
            return Task.FromResult(0.0);
        }

        var now = DateTime.UtcNow;
        var startTime = _sensorStartTimes[sensorId];
        var elapsedSeconds = (now - startTime).TotalSeconds;
        var isDraining = _sensorDirection[sensorId];

        // Calculate progress through the 2-minute cycle (0.0 to 1.0)
        var progress = (elapsedSeconds % CYCLE_DURATION_SECONDS) / CYCLE_DURATION_SECONDS;

        // Calculate base value based on direction
        double baseValue;
        if (isDraining)
        {
            // Draining: starts at 100%, goes to 0%, then resets to 100%
            baseValue = 100.0 - (progress * 100.0);
        }
        else
        {
            // Filling: starts at 0%, goes to 100%, then resets to 0%
            baseValue = progress * 100.0;
        }

        // Add small random variation to simulate real sensor readings (+/- 0.5%)
        var variation = (_random.NextDouble() - 0.5) * 1.0;
        var value = Math.Clamp(baseValue + variation, 0, 100);

        // Reset cycle if complete
        if (elapsedSeconds >= CYCLE_DURATION_SECONDS)
        {
            var cyclesCompleted = (int)(elapsedSeconds / CYCLE_DURATION_SECONDS);
            _sensorStartTimes[sensorId] = startTime.AddSeconds(cyclesCompleted * CYCLE_DURATION_SECONDS);
            _logger.LogInformation("Sensor {SensorId} completed cycle - resetting to {StartValue}%",
                sensorId, isDraining ? "100" : "0");
        }

        _logger.LogDebug("Read sensor {SensorId}: {Value}% (cycle progress: {Progress:P0})", sensorId, value, progress);
        return Task.FromResult(value);
    }

    public Task<IDictionary<string, double>> ReadAllValuesAsync(CancellationToken cancellationToken = default)
    {
        var values = new Dictionary<string, double>();
        foreach (var sensor in _sensorStartTimes.Keys)
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
