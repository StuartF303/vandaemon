using VanDaemon.Core.Entities;

namespace VanDaemon.Application.Interfaces;

/// <summary>
/// Service for managing tank monitoring and operations
/// </summary>
public interface ITankService
{
    Task<IEnumerable<Tank>> GetAllTanksAsync(CancellationToken cancellationToken = default);
    Task<Tank?> GetTankByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<double> GetTankLevelAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Tank> UpdateTankAsync(Tank tank, CancellationToken cancellationToken = default);
    Task<Tank> CreateTankAsync(Tank tank, CancellationToken cancellationToken = default);
    Task DeleteTankAsync(Guid id, CancellationToken cancellationToken = default);
    Task RefreshAllTankLevelsAsync(CancellationToken cancellationToken = default);
}
