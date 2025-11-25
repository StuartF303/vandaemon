using Microsoft.Extensions.Logging;
using VanDaemon.Application.Interfaces;
using VanDaemon.Application.Persistence;
using VanDaemon.Core.Entities;

namespace VanDaemon.Application.Services;

public class UnifiedConfigService : IUnifiedConfigService
{
    private readonly ILogger<UnifiedConfigService> _logger;
    private readonly JsonFileStore _fileStore;
    private const string ConfigFileName = "unified-device-config.json";
    private UnifiedDeviceConfiguration? _config;
    private readonly object _configLock = new();

    public UnifiedConfigService(ILogger<UnifiedConfigService> logger, JsonFileStore fileStore)
    {
        _logger = logger;
        _fileStore = fileStore;
        LoadConfigAsync().Wait();
    }

    public async Task<UnifiedDeviceConfiguration> LoadConfigAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var config = await _fileStore.LoadAsync<UnifiedDeviceConfiguration>(ConfigFileName);
            lock (_configLock)
            {
                _config = config ?? new UnifiedDeviceConfiguration();
            }
            _logger.LogInformation("Loaded unified device configuration with {TankCount} tanks, {ControlCount} controls, {DeviceCount} electrical devices",
                _config.Tanks.Count, _config.Controls.Count, _config.ElectricalDevices.Count);
            return _config;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load unified configuration, starting with empty config");
            lock (_configLock)
            {
                _config = new UnifiedDeviceConfiguration();
            }
            return _config;
        }
    }

    public async Task SaveConfigAsync(UnifiedDeviceConfiguration config, CancellationToken cancellationToken = default)
    {
        try
        {
            config.LastUpdated = DateTime.UtcNow;
            lock (_configLock)
            {
                _config = config;
            }
            await _fileStore.SaveAsync(ConfigFileName, config);
            _logger.LogInformation("Saved unified device configuration");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save unified configuration");
            throw;
        }
    }

    public async Task<DevicePosition?> GetDevicePositionAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        var config = await LoadConfigAsync(cancellationToken);
        lock (_configLock)
        {
            return config.DevicePositions.FirstOrDefault(p => p.DeviceId == deviceId);
        }
    }

    public async Task SaveDevicePositionAsync(DevicePosition position, CancellationToken cancellationToken = default)
    {
        var config = await LoadConfigAsync(cancellationToken);
        lock (_configLock)
        {
            var existing = config.DevicePositions.FirstOrDefault(p => p.DeviceId == position.DeviceId);
            if (existing != null)
            {
                existing.X = position.X;
                existing.Y = position.Y;
                existing.LastUpdated = DateTime.UtcNow;
            }
            else
            {
                position.LastUpdated = DateTime.UtcNow;
                config.DevicePositions.Add(position);
            }
        }
        await SaveConfigAsync(config, cancellationToken);
    }

    public async Task<List<DevicePosition>> GetAllPositionsAsync(CancellationToken cancellationToken = default)
    {
        var config = await LoadConfigAsync(cancellationToken);
        lock (_configLock)
        {
            return new List<DevicePosition>(config.DevicePositions);
        }
    }
}
