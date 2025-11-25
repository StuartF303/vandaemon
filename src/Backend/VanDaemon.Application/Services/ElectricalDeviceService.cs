using Microsoft.Extensions.Logging;
using VanDaemon.Application.Interfaces;
using VanDaemon.Application.Utilities;
using VanDaemon.Core.Entities;

namespace VanDaemon.Application.Services;

public class ElectricalDeviceService : IElectricalDeviceService
{
    private readonly ILogger<ElectricalDeviceService> _logger;
    private readonly IUnifiedConfigService _configService;

    public ElectricalDeviceService(ILogger<ElectricalDeviceService> logger, IUnifiedConfigService configService)
    {
        _logger = logger;
        _configService = configService;
    }

    // Device management
    public async Task<List<ElectricalDevice>> GetAllDevicesAsync(CancellationToken cancellationToken = default)
    {
        var config = await _configService.LoadConfigAsync(cancellationToken);
        return config.ElectricalDevices.Where(d => d.IsActive).ToList();
    }

    public async Task<ElectricalDevice?> GetDeviceByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var config = await _configService.LoadConfigAsync(cancellationToken);
        return config.ElectricalDevices.FirstOrDefault(d => d.Id == id && d.IsActive);
    }

    public async Task<ElectricalDevice> CreateDeviceAsync(ElectricalDevice device, CancellationToken cancellationToken = default)
    {
        device.Id = Guid.NewGuid();
        device.LastUpdated = DateTime.UtcNow;
        device.IsActive = true;

        var config = await _configService.LoadConfigAsync(cancellationToken);
        config.ElectricalDevices.Add(device);

        // Auto-position the new device to avoid overlap
        var existingPositions = config.DevicePositions
            .Where(p => p.DeviceType == "ElectricalDevice")
            .ToList();
        var (x, y) = DevicePositionHelper.GetNonOverlappingPosition(existingPositions, "ElectricalDevice");

        var position = new DevicePosition
        {
            DeviceId = device.Id.ToString(),
            DeviceType = "ElectricalDevice",
            X = x,
            Y = y,
            LastUpdated = DateTime.UtcNow
        };
        config.DevicePositions.Add(position);

        await _configService.SaveConfigAsync(config, cancellationToken);
        _logger.LogInformation("Created electrical device {DeviceId} - {DeviceName} at position ({X}, {Y})",
            device.Id, device.Name, x, y);

        return device;
    }

    public async Task<ElectricalDevice> UpdateDeviceAsync(ElectricalDevice device, CancellationToken cancellationToken = default)
    {
        var config = await _configService.LoadConfigAsync(cancellationToken);
        var existing = config.ElectricalDevices.FirstOrDefault(d => d.Id == device.Id);

        if (existing == null)
        {
            throw new InvalidOperationException($"Device {device.Id} not found");
        }

        existing.Name = device.Name;
        existing.DeviceType = device.DeviceType;
        existing.Configuration = device.Configuration;
        existing.Ports = device.Ports;
        existing.DataSourcePlugin = device.DataSourcePlugin;
        existing.DataSourceConfiguration = device.DataSourceConfiguration;
        existing.LastUpdated = DateTime.UtcNow;

        await _configService.SaveConfigAsync(config, cancellationToken);
        _logger.LogInformation("Updated electrical device {DeviceId} - {DeviceName}", device.Id, device.Name);

        return existing;
    }

    public async Task<bool> DeleteDeviceAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var config = await _configService.LoadConfigAsync(cancellationToken);
        var device = config.ElectricalDevices.FirstOrDefault(d => d.Id == id);

        if (device == null)
        {
            return false;
        }

        device.IsActive = false;
        device.LastUpdated = DateTime.UtcNow;

        // Also deactivate any connections to this device
        var connectionsToDeactivate = config.ElectricalConnections
            .Where(c => (c.SourceDeviceId == id || c.TargetDeviceId == id) && c.IsActive)
            .ToList();

        foreach (var connection in connectionsToDeactivate)
        {
            connection.IsActive = false;
            connection.LastUpdated = DateTime.UtcNow;
        }

        await _configService.SaveConfigAsync(config, cancellationToken);
        _logger.LogInformation("Deleted electrical device {DeviceId}", id);

        return true;
    }

    // Connection management
    public async Task<List<ElectricalConnection>> GetAllConnectionsAsync(CancellationToken cancellationToken = default)
    {
        var config = await _configService.LoadConfigAsync(cancellationToken);
        return config.ElectricalConnections.Where(c => c.IsActive).ToList();
    }

    public async Task<ElectricalConnection?> GetConnectionByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var config = await _configService.LoadConfigAsync(cancellationToken);
        return config.ElectricalConnections.FirstOrDefault(c => c.Id == id && c.IsActive);
    }

    public async Task<ElectricalConnection> CreateConnectionAsync(ElectricalConnection connection, CancellationToken cancellationToken = default)
    {
        connection.Id = Guid.NewGuid();
        connection.LastUpdated = DateTime.UtcNow;
        connection.IsActive = true;

        var config = await _configService.LoadConfigAsync(cancellationToken);
        config.ElectricalConnections.Add(connection);

        await _configService.SaveConfigAsync(config, cancellationToken);
        _logger.LogInformation("Created electrical connection {ConnectionId} - {ConnectionName}", connection.Id, connection.Name);

        return connection;
    }

    public async Task<ElectricalConnection> UpdateConnectionAsync(ElectricalConnection connection, CancellationToken cancellationToken = default)
    {
        var config = await _configService.LoadConfigAsync(cancellationToken);
        var existing = config.ElectricalConnections.FirstOrDefault(c => c.Id == connection.Id);

        if (existing == null)
        {
            throw new InvalidOperationException($"Connection {connection.Id} not found");
        }

        existing.Name = connection.Name;
        existing.SourceDeviceId = connection.SourceDeviceId;
        existing.SourcePortId = connection.SourcePortId;
        existing.TargetDeviceId = connection.TargetDeviceId;
        existing.TargetPortId = connection.TargetPortId;
        existing.Color = connection.Color;
        existing.LineWidth = connection.LineWidth;
        existing.LastUpdated = DateTime.UtcNow;

        await _configService.SaveConfigAsync(config, cancellationToken);
        _logger.LogInformation("Updated electrical connection {ConnectionId} - {ConnectionName}", connection.Id, connection.Name);

        return existing;
    }

    public async Task<bool> DeleteConnectionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var config = await _configService.LoadConfigAsync(cancellationToken);
        var connection = config.ElectricalConnections.FirstOrDefault(c => c.Id == id);

        if (connection == null)
        {
            return false;
        }

        connection.IsActive = false;
        connection.LastUpdated = DateTime.UtcNow;

        await _configService.SaveConfigAsync(config, cancellationToken);
        _logger.LogInformation("Deleted electrical connection {ConnectionId}", id);

        return true;
    }

    // Real-time metrics
    public Task RefreshDeviceMetricsAsync(CancellationToken cancellationToken = default)
    {
        // TODO: Implement metric refresh from data source plugins
        _logger.LogDebug("Refreshing electrical device metrics");
        return Task.CompletedTask;
    }

    public async Task<Dictionary<Guid, Dictionary<string, double>>> GetAllDeviceMetricsAsync(CancellationToken cancellationToken = default)
    {
        var config = await _configService.LoadConfigAsync(cancellationToken);
        var metrics = new Dictionary<Guid, Dictionary<string, double>>();

        foreach (var device in config.ElectricalDevices.Where(d => d.IsActive))
        {
            metrics[device.Id] = new Dictionary<string, double>(device.CurrentMetrics);
        }

        return metrics;
    }

    public async Task<Dictionary<Guid, double>> GetAllConnectionFlowsAsync(CancellationToken cancellationToken = default)
    {
        var config = await _configService.LoadConfigAsync(cancellationToken);
        var flows = new Dictionary<Guid, double>();

        foreach (var connection in config.ElectricalConnections.Where(c => c.IsActive))
        {
            flows[connection.Id] = connection.CurrentFlow;
        }

        return flows;
    }

    // Device discovery
    public Task<List<ElectricalDevice>> DiscoverDevicesAsync(string pluginName, Dictionary<string, object> configuration, CancellationToken cancellationToken = default)
    {
        // TODO: Implement device discovery logic
        _logger.LogInformation("Device discovery requested for plugin {PluginName}", pluginName);
        return Task.FromResult(new List<ElectricalDevice>());
    }
}
