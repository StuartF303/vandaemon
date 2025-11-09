using Microsoft.Extensions.Logging;
using VanDaemon.Application.Interfaces;
using VanDaemon.Application.Persistence;
using VanDaemon.Core.Entities;
using VanDaemon.Plugins.Abstractions;

namespace VanDaemon.Application.Services;

/// <summary>
/// Service implementation for tank monitoring and operations
/// </summary>
public class TankService : ITankService
{
    private readonly ILogger<TankService> _logger;
    private readonly JsonFileStore _fileStore;
    private readonly List<Tank> _tanks;
    private readonly Dictionary<string, ISensorPlugin> _sensorPlugins;
    private const string TanksFileName = "tanks.json";

    public TankService(ILogger<TankService> logger, JsonFileStore fileStore, IEnumerable<ISensorPlugin> sensorPlugins)
    {
        _logger = logger;
        _fileStore = fileStore;
        _tanks = new List<Tank>();
        _sensorPlugins = sensorPlugins.ToDictionary(p => p.Name, p => p);
        _ = LoadTanksAsync(); // Fire and forget to load tanks on startup
    }

    private async Task LoadTanksAsync()
    {
        try
        {
            var tanks = await _fileStore.LoadAsync<List<Tank>>(TanksFileName);
            if (tanks != null && tanks.Any())
            {
                _tanks.Clear();
                _tanks.AddRange(tanks);
                _logger.LogInformation("Loaded {Count} tanks from {FileName}", tanks.Count, TanksFileName);
            }
            else
            {
                InitializeDefaultTanks();
                await SaveTanksAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading tanks from JSON, using defaults");
            InitializeDefaultTanks();
        }
    }

    private async Task SaveTanksAsync()
    {
        try
        {
            await _fileStore.SaveAsync(TanksFileName, _tanks);
            _logger.LogDebug("Saved {Count} tanks to {FileName}", _tanks.Count, TanksFileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving tanks to JSON");
        }
    }

    private void InitializeDefaultTanks()
    {
        // Initialize with some default tanks for demonstration
        _tanks.Add(new Tank
        {
            Id = Guid.NewGuid(),
            Name = "Fresh Water",
            Type = Core.Enums.TankType.FreshWater,
            Capacity = 100,
            CurrentLevel = 75,
            AlertLevel = 20.0,
            AlertWhenOver = false, // Alert when UNDER 20% (empty)
            SensorPlugin = "Simulated Sensor Plugin",
            SensorConfiguration = new Dictionary<string, object> { ["sensorId"] = "fresh_water" },
            LastUpdated = DateTime.UtcNow,
            IsActive = true
        });

        _tanks.Add(new Tank
        {
            Id = Guid.NewGuid(),
            Name = "Waste Water",
            Type = Core.Enums.TankType.WasteWater,
            Capacity = 80,
            CurrentLevel = 25,
            AlertLevel = 80.0,
            AlertWhenOver = true, // Alert when OVER 80% (full)
            SensorPlugin = "Simulated Sensor Plugin",
            SensorConfiguration = new Dictionary<string, object> { ["sensorId"] = "waste_water" },
            LastUpdated = DateTime.UtcNow,
            IsActive = true
        });

        _tanks.Add(new Tank
        {
            Id = Guid.NewGuid(),
            Name = "LPG",
            Type = Core.Enums.TankType.LPG,
            Capacity = 30,
            CurrentLevel = 60,
            AlertLevel = 20.0,
            AlertWhenOver = false, // Alert when UNDER 20% (empty)
            SensorPlugin = "Simulated Sensor Plugin",
            SensorConfiguration = new Dictionary<string, object> { ["sensorId"] = "lpg" },
            LastUpdated = DateTime.UtcNow,
            IsActive = true
        });

        _logger.LogInformation("Initialized {Count} default tanks", _tanks.Count);
    }

    public Task<IEnumerable<Tank>> GetAllTanksAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IEnumerable<Tank>>(_tanks.Where(t => t.IsActive));
    }

    public Task<Tank?> GetTankByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tank = _tanks.FirstOrDefault(t => t.Id == id);
        return Task.FromResult(tank);
    }

    public async Task<double> GetTankLevelAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tank = await GetTankByIdAsync(id, cancellationToken);
        if (tank == null)
        {
            _logger.LogWarning("Tank {TankId} not found", id);
            return 0;
        }

        // Get the sensor plugin and read the current level
        if (_sensorPlugins.TryGetValue(tank.SensorPlugin, out var plugin))
        {
            var sensorId = tank.SensorConfiguration.GetValueOrDefault("sensorId")?.ToString() ?? string.Empty;
            try
            {
                var level = await plugin.ReadValueAsync(sensorId, cancellationToken);
                tank.CurrentLevel = level;
                tank.LastUpdated = DateTime.UtcNow;
                _logger.LogDebug("Updated tank {TankName} level to {Level}%", tank.Name, level);
                return level;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading tank level for {TankName}", tank.Name);
            }
        }

        return tank.CurrentLevel;
    }

    public async Task<Tank> UpdateTankAsync(Tank tank, CancellationToken cancellationToken = default)
    {
        var existingTank = _tanks.FirstOrDefault(t => t.Id == tank.Id);
        if (existingTank != null)
        {
            _tanks.Remove(existingTank);
        }

        tank.LastUpdated = DateTime.UtcNow;
        _tanks.Add(tank);
        _logger.LogInformation("Updated tank {TankName}", tank.Name);
        await SaveTanksAsync();
        return tank;
    }

    public async Task<Tank> CreateTankAsync(Tank tank, CancellationToken cancellationToken = default)
    {
        tank.Id = Guid.NewGuid();
        tank.LastUpdated = DateTime.UtcNow;
        _tanks.Add(tank);
        _logger.LogInformation("Created tank {TankName} with ID {TankId}", tank.Name, tank.Id);
        await SaveTanksAsync();
        return tank;
    }

    public async Task DeleteTankAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tank = _tanks.FirstOrDefault(t => t.Id == id);
        if (tank != null)
        {
            tank.IsActive = false;
            _logger.LogInformation("Deleted tank {TankName}", tank.Name);
            await SaveTanksAsync();
        }
    }

    public async Task RefreshAllTankLevelsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Refreshing all tank levels");
        foreach (var tank in _tanks.Where(t => t.IsActive))
        {
            await GetTankLevelAsync(tank.Id, cancellationToken);
        }
    }
}
