using Microsoft.Extensions.Logging;
using VanDaemon.Application.Interfaces;
using VanDaemon.Application.Persistence;
using VanDaemon.Core.Entities;
using VanDaemon.Plugins.Abstractions;

namespace VanDaemon.Application.Services;

/// <summary>
/// Service implementation for electrical system monitoring
/// </summary>
public class ElectricalService : IElectricalService
{
    private readonly ILogger<ElectricalService> _logger;
    private readonly JsonFileStore _fileStore;
    private ElectricalSystem? _electricalSystem;
    private readonly Dictionary<string, ISensorPlugin> _sensorPlugins;
    private const string ElectricalSystemFileName = "electrical_system.json";

    public ElectricalService(ILogger<ElectricalService> logger, JsonFileStore fileStore, IEnumerable<ISensorPlugin> sensorPlugins)
    {
        _logger = logger;
        _fileStore = fileStore;
        _sensorPlugins = sensorPlugins.ToDictionary(p => p.Name, p => p);
        _ = LoadElectricalSystemAsync(); // Fire and forget to load on startup
    }

    private async Task LoadElectricalSystemAsync()
    {
        try
        {
            var system = await _fileStore.LoadAsync<ElectricalSystem>(ElectricalSystemFileName);
            if (system != null)
            {
                _electricalSystem = system;
                _logger.LogInformation("Loaded electrical system from {FileName}", ElectricalSystemFileName);
            }
            else
            {
                InitializeDefaultElectricalSystem();
                await SaveElectricalSystemAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading electrical system from JSON, using defaults");
            InitializeDefaultElectricalSystem();
        }
    }

    private async Task SaveElectricalSystemAsync()
    {
        try
        {
            await _fileStore.SaveAsync(ElectricalSystemFileName, _electricalSystem);
            _logger.LogDebug("Saved electrical system to {FileName}", ElectricalSystemFileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving electrical system to JSON");
        }
    }

    private void InitializeDefaultElectricalSystem()
    {
        _electricalSystem = new ElectricalSystem
        {
            Id = Guid.NewGuid(),
            Name = "Main Battery System",
            Voltage = 12.6,
            Current = -5.0,
            Power = -63.0,
            StateOfCharge = 75.0,
            Temperature = 22.0,
            ConsumedAmpHours = 25.0,
            TimeToGo = 36000, // 10 hours
            SolarPower = 0.0,
            SolarVoltage = 0.0,
            SolarCurrent = 0.0,
            AcInputPower = 0.0,
            AcOutputPower = 0.0,
            SensorPlugin = "Simulated Sensor Plugin",
            SensorConfiguration = new Dictionary<string, object>
            {
                ["voltage_sensor"] = "battery_voltage",
                ["current_sensor"] = "battery_current",
                ["soc_sensor"] = "battery_soc",
                ["temperature_sensor"] = "battery_temperature",
                ["solar_power_sensor"] = "solar_power"
            },
            LastUpdated = DateTime.UtcNow,
            IsActive = true
        };

        _logger.LogInformation("Initialized default electrical system");
    }

    public Task<ElectricalSystem?> GetElectricalSystemAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_electricalSystem);
    }

    public async Task<ElectricalSystem> UpdateElectricalSystemAsync(ElectricalSystem system, CancellationToken cancellationToken = default)
    {
        system.LastUpdated = DateTime.UtcNow;
        _electricalSystem = system;
        _logger.LogInformation("Updated electrical system configuration");
        await SaveElectricalSystemAsync();
        return system;
    }

    public async Task RefreshElectricalDataAsync(CancellationToken cancellationToken = default)
    {
        if (_electricalSystem == null)
        {
            _logger.LogWarning("Electrical system not initialized");
            return;
        }

        if (!_sensorPlugins.TryGetValue(_electricalSystem.SensorPlugin, out var plugin))
        {
            _logger.LogWarning("Sensor plugin {PluginName} not found", _electricalSystem.SensorPlugin);
            return;
        }

        try
        {
            // Read battery voltage
            var voltageSensor = _electricalSystem.SensorConfiguration.GetValueOrDefault("voltage_sensor")?.ToString() ?? "battery_voltage";
            _electricalSystem.Voltage = await plugin.ReadValueAsync(voltageSensor, cancellationToken);

            // Read battery current
            var currentSensor = _electricalSystem.SensorConfiguration.GetValueOrDefault("current_sensor")?.ToString() ?? "battery_current";
            _electricalSystem.Current = await plugin.ReadValueAsync(currentSensor, cancellationToken);

            // Calculate power (P = V * I)
            _electricalSystem.Power = _electricalSystem.Voltage * _electricalSystem.Current;

            // Read state of charge
            var socSensor = _electricalSystem.SensorConfiguration.GetValueOrDefault("soc_sensor")?.ToString() ?? "battery_soc";
            _electricalSystem.StateOfCharge = await plugin.ReadValueAsync(socSensor, cancellationToken);

            // Read temperature
            var tempSensor = _electricalSystem.SensorConfiguration.GetValueOrDefault("temperature_sensor")?.ToString() ?? "battery_temperature";
            _electricalSystem.Temperature = await plugin.ReadValueAsync(tempSensor, cancellationToken);

            // Read solar power
            var solarSensor = _electricalSystem.SensorConfiguration.GetValueOrDefault("solar_power_sensor")?.ToString() ?? "solar_power";
            _electricalSystem.SolarPower = await plugin.ReadValueAsync(solarSensor, cancellationToken);

            // Calculate solar voltage and current (simplified estimation)
            if (_electricalSystem.SolarPower > 0)
            {
                _electricalSystem.SolarVoltage = 18.0 + (_random.NextDouble() - 0.5) * 2.0; // ~18V for 12V system
                _electricalSystem.SolarCurrent = _electricalSystem.SolarPower / _electricalSystem.SolarVoltage;
            }
            else
            {
                _electricalSystem.SolarVoltage = 0.0;
                _electricalSystem.SolarCurrent = 0.0;
            }

            // Calculate consumed amp hours (simplified)
            if (_electricalSystem.Current < 0) // Discharging
            {
                var hoursElapsed = 1.0 / 3600.0; // 1 second in hours (called every 5 seconds)
                _electricalSystem.ConsumedAmpHours += Math.Abs(_electricalSystem.Current) * hoursElapsed;
            }

            // Calculate time to go (simplified: based on current draw and remaining capacity)
            // Assuming 200Ah battery capacity
            var batteryCapacity = 200.0;
            var remainingAh = (batteryCapacity * _electricalSystem.StateOfCharge / 100.0);
            if (_electricalSystem.Current < 0) // Discharging
            {
                var hoursToEmpty = remainingAh / Math.Abs(_electricalSystem.Current);
                _electricalSystem.TimeToGo = (int)(hoursToEmpty * 3600); // Convert to seconds
            }
            else
            {
                _electricalSystem.TimeToGo = 0; // Infinite (charging or idle)
            }

            _electricalSystem.LastUpdated = DateTime.UtcNow;

            _logger.LogDebug(
                "Refreshed electrical data: {Voltage:F2}V, {Current:F2}A, {Power:F2}W, {SOC:F1}%, {Temp:F1}°C, Solar: {SolarPower:F1}W",
                _electricalSystem.Voltage,
                _electricalSystem.Current,
                _electricalSystem.Power,
                _electricalSystem.StateOfCharge,
                _electricalSystem.Temperature,
                _electricalSystem.SolarPower
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing electrical system data");
        }
    }

    private readonly Random _random = new();
}
