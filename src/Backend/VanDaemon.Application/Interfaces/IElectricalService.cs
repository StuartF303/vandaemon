using VanDaemon.Core.Entities;

namespace VanDaemon.Application.Interfaces;

/// <summary>
/// Service for managing electrical system monitoring (battery, solar, AC power)
/// </summary>
public interface IElectricalService
{
    Task<ElectricalSystem?> GetElectricalSystemAsync(CancellationToken cancellationToken = default);
    Task<ElectricalSystem> UpdateElectricalSystemAsync(ElectricalSystem system, CancellationToken cancellationToken = default);
    Task RefreshElectricalDataAsync(CancellationToken cancellationToken = default);
}
