using VanDaemon.Core.Entities;

namespace VanDaemon.Application.Interfaces;

/// <summary>
/// Service for managing control operations (switches, dimmers, etc.)
/// </summary>
public interface IControlService
{
    Task<IEnumerable<Control>> GetAllControlsAsync(CancellationToken cancellationToken = default);
    Task<Control?> GetControlByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<object> GetControlStateAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> SetControlStateAsync(Guid id, object state, CancellationToken cancellationToken = default);
    Task<Control> UpdateControlAsync(Control control, CancellationToken cancellationToken = default);
    Task<Control> CreateControlAsync(Control control, CancellationToken cancellationToken = default);
    Task DeleteControlAsync(Guid id, CancellationToken cancellationToken = default);
}
