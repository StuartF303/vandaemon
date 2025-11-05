using Microsoft.Extensions.Logging;
using VanDaemon.Application.Interfaces;
using VanDaemon.Core.Entities;
using VanDaemon.Plugins.Abstractions;

namespace VanDaemon.Application.Services;

/// <summary>
/// Service implementation for control operations
/// </summary>
public class ControlService : IControlService
{
    private readonly ILogger<ControlService> _logger;
    private readonly List<Control> _controls;
    private readonly Dictionary<string, IControlPlugin> _controlPlugins;

    public ControlService(ILogger<ControlService> logger, IEnumerable<IControlPlugin> controlPlugins)
    {
        _logger = logger;
        _controls = new List<Control>();
        _controlPlugins = controlPlugins.ToDictionary(p => p.Name, p => p);
        InitializeDefaultControls();
    }

    private void InitializeDefaultControls()
    {
        // Initialize with some default controls for demonstration
        _controls.Add(new Control
        {
            Id = Guid.NewGuid(),
            Name = "Main Lights",
            Type = Core.Enums.ControlType.Toggle,
            State = false,
            ControlPlugin = "Simulated Control Plugin",
            ControlConfiguration = new Dictionary<string, object> { ["controlId"] = "light_main" },
            LastUpdated = DateTime.UtcNow,
            IsActive = true,
            IconName = "lightbulb"
        });

        _controls.Add(new Control
        {
            Id = Guid.NewGuid(),
            Name = "Dimmer Lights",
            Type = Core.Enums.ControlType.Dimmer,
            State = 0,
            ControlPlugin = "Simulated Control Plugin",
            ControlConfiguration = new Dictionary<string, object> { ["controlId"] = "light_dimmer" },
            LastUpdated = DateTime.UtcNow,
            IsActive = true,
            IconName = "light_mode"
        });

        _controls.Add(new Control
        {
            Id = Guid.NewGuid(),
            Name = "Water Pump",
            Type = Core.Enums.ControlType.Toggle,
            State = false,
            ControlPlugin = "Simulated Control Plugin",
            ControlConfiguration = new Dictionary<string, object> { ["controlId"] = "water_pump" },
            LastUpdated = DateTime.UtcNow,
            IsActive = true,
            IconName = "water_pump"
        });

        _controls.Add(new Control
        {
            Id = Guid.NewGuid(),
            Name = "Heater",
            Type = Core.Enums.ControlType.Toggle,
            State = false,
            ControlPlugin = "Simulated Control Plugin",
            ControlConfiguration = new Dictionary<string, object> { ["controlId"] = "heater" },
            LastUpdated = DateTime.UtcNow,
            IsActive = true,
            IconName = "thermostat"
        });

        _logger.LogInformation("Initialized {Count} default controls", _controls.Count);
    }

    public Task<IEnumerable<Control>> GetAllControlsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IEnumerable<Control>>(_controls.Where(c => c.IsActive));
    }

    public Task<Control?> GetControlByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var control = _controls.FirstOrDefault(c => c.Id == id);
        return Task.FromResult(control);
    }

    public async Task<object> GetControlStateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var control = await GetControlByIdAsync(id, cancellationToken);
        if (control == null)
        {
            _logger.LogWarning("Control {ControlId} not found", id);
            return false;
        }

        // Get the control plugin and read the current state
        if (_controlPlugins.TryGetValue(control.ControlPlugin, out var plugin))
        {
            var controlId = control.ControlConfiguration.GetValueOrDefault("controlId")?.ToString() ?? string.Empty;
            try
            {
                var state = await plugin.GetStateAsync(controlId, cancellationToken);
                control.State = state;
                control.LastUpdated = DateTime.UtcNow;
                _logger.LogDebug("Read control {ControlName} state: {State}", control.Name, state);
                return state;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading control state for {ControlName}", control.Name);
            }
        }

        return control.State;
    }

    public async Task<bool> SetControlStateAsync(Guid id, object state, CancellationToken cancellationToken = default)
    {
        var control = await GetControlByIdAsync(id, cancellationToken);
        if (control == null)
        {
            _logger.LogWarning("Control {ControlId} not found", id);
            return false;
        }

        // Get the control plugin and set the state
        if (_controlPlugins.TryGetValue(control.ControlPlugin, out var plugin))
        {
            var controlId = control.ControlConfiguration.GetValueOrDefault("controlId")?.ToString() ?? string.Empty;
            try
            {
                var success = await plugin.SetStateAsync(controlId, state, cancellationToken);
                if (success)
                {
                    control.State = state;
                    control.LastUpdated = DateTime.UtcNow;
                    _logger.LogInformation("Set control {ControlName} to state {State}", control.Name, state);
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting control state for {ControlName}", control.Name);
            }
        }

        return false;
    }

    public Task<Control> UpdateControlAsync(Control control, CancellationToken cancellationToken = default)
    {
        var existingControl = _controls.FirstOrDefault(c => c.Id == control.Id);
        if (existingControl != null)
        {
            _controls.Remove(existingControl);
        }

        control.LastUpdated = DateTime.UtcNow;
        _controls.Add(control);
        _logger.LogInformation("Updated control {ControlName}", control.Name);
        return Task.FromResult(control);
    }

    public Task<Control> CreateControlAsync(Control control, CancellationToken cancellationToken = default)
    {
        control.Id = Guid.NewGuid();
        control.LastUpdated = DateTime.UtcNow;
        _controls.Add(control);
        _logger.LogInformation("Created control {ControlName} with ID {ControlId}", control.Name, control.Id);
        return Task.FromResult(control);
    }

    public Task DeleteControlAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var control = _controls.FirstOrDefault(c => c.Id == id);
        if (control != null)
        {
            control.IsActive = false;
            _logger.LogInformation("Deleted control {ControlName}", control.Name);
        }

        return Task.CompletedTask;
    }
}
