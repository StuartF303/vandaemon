using VanDaemon.Core.Entities;

namespace VanDaemon.Application.Interfaces;

public interface IElectricalDeviceService
{
    // Device management
    Task<List<ElectricalDevice>> GetAllDevicesAsync(CancellationToken cancellationToken = default);
    Task<ElectricalDevice?> GetDeviceByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ElectricalDevice> CreateDeviceAsync(ElectricalDevice device, CancellationToken cancellationToken = default);
    Task<ElectricalDevice> UpdateDeviceAsync(ElectricalDevice device, CancellationToken cancellationToken = default);
    Task<bool> DeleteDeviceAsync(Guid id, CancellationToken cancellationToken = default);

    // Connection management
    Task<List<ElectricalConnection>> GetAllConnectionsAsync(CancellationToken cancellationToken = default);
    Task<ElectricalConnection?> GetConnectionByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ElectricalConnection> CreateConnectionAsync(ElectricalConnection connection, CancellationToken cancellationToken = default);
    Task<ElectricalConnection> UpdateConnectionAsync(ElectricalConnection connection, CancellationToken cancellationToken = default);
    Task<bool> DeleteConnectionAsync(Guid id, CancellationToken cancellationToken = default);

    // Real-time metrics
    Task RefreshDeviceMetricsAsync(CancellationToken cancellationToken = default);
    Task<Dictionary<Guid, Dictionary<string, double>>> GetAllDeviceMetricsAsync(CancellationToken cancellationToken = default);
    Task<Dictionary<Guid, double>> GetAllConnectionFlowsAsync(CancellationToken cancellationToken = default);

    // Device discovery
    Task<List<ElectricalDevice>> DiscoverDevicesAsync(string pluginName, Dictionary<string, object> configuration, CancellationToken cancellationToken = default);
}
