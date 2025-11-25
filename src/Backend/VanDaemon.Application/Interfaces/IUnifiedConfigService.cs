using VanDaemon.Core.Entities;

namespace VanDaemon.Application.Interfaces;

public interface IUnifiedConfigService
{
    Task<UnifiedDeviceConfiguration> LoadConfigAsync(CancellationToken cancellationToken = default);
    Task SaveConfigAsync(UnifiedDeviceConfiguration config, CancellationToken cancellationToken = default);
    Task<DevicePosition?> GetDevicePositionAsync(string deviceId, CancellationToken cancellationToken = default);
    Task SaveDevicePositionAsync(DevicePosition position, CancellationToken cancellationToken = default);
    Task<List<DevicePosition>> GetAllPositionsAsync(CancellationToken cancellationToken = default);
}
