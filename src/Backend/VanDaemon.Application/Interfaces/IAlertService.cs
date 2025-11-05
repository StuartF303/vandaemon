using VanDaemon.Core.Entities;
using VanDaemon.Core.Enums;

namespace VanDaemon.Application.Interfaces;

/// <summary>
/// Service for managing system alerts and notifications
/// </summary>
public interface IAlertService
{
    Task<IEnumerable<Alert>> GetAlertsAsync(bool includeAcknowledged = false, CancellationToken cancellationToken = default);
    Task<Alert?> GetAlertByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Alert> CreateAlertAsync(AlertSeverity severity, string source, string message, CancellationToken cancellationToken = default);
    Task AcknowledgeAlertAsync(Guid id, CancellationToken cancellationToken = default);
    Task DeleteAlertAsync(Guid id, CancellationToken cancellationToken = default);
    Task CheckTankAlertsAsync(CancellationToken cancellationToken = default);
}
