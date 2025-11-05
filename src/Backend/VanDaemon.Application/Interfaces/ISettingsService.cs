using VanDaemon.Core.Entities;

namespace VanDaemon.Application.Interfaces;

/// <summary>
/// Service for managing system configuration and settings
/// </summary>
public interface ISettingsService
{
    Task<SystemConfiguration> GetConfigurationAsync(CancellationToken cancellationToken = default);
    Task<SystemConfiguration> UpdateConfigurationAsync(SystemConfiguration configuration, CancellationToken cancellationToken = default);
    Task<IEnumerable<string>> GetAvailableVanDiagramsAsync(CancellationToken cancellationToken = default);
}
