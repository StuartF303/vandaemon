using Microsoft.Extensions.Logging;
using VanDaemon.Application.Interfaces;
using VanDaemon.Application.Persistence;
using VanDaemon.Core.Entities;
using VanDaemon.Plugins.Abstractions;

namespace VanDaemon.Application.Services;

/// <summary>
/// Service implementation for control operations
/// </summary>
public class ControlService : IControlService
{
    private readonly ILogger<ControlService> _logger;
    private readonly JsonFileStore _fileStore;
    private readonly List<Control> _controls;
    private readonly Dictionary<string, IControlPlugin> _controlPlugins;
    private const string ControlsFileName = "controls.json";

    public ControlService(ILogger<ControlService> logger, JsonFileStore fileStore, IEnumerable<IControlPlugin> controlPlugins)
    {
        _logger = logger;
        _fileStore = fileStore;
        _controls = new List<Control>();
        _controlPlugins = controlPlugins.ToDictionary(p => p.Name, p => p);
        _ = LoadControlsAsync(); // Fire and forget to load controls on startup
    }

    private async Task LoadControlsAsync()
    {
        try
        {
            var controls = await _fileStore.LoadAsync<List<Control>>(ControlsFileName);
            if (controls != null && controls.Any())
            {
                _controls.Clear();
                _controls.AddRange(controls);
                _logger.LogInformation("Loaded {Count} controls from {FileName}", controls.Count, ControlsFileName);
            }
            else
            {
                InitializeDefaultControls();
                await SaveControlsAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading controls from JSON, using defaults");
            InitializeDefaultControls();
        }
    }

    private async Task SaveControlsAsync()
    {
        try
        {
            await _fileStore.SaveAsync(ControlsFileName, _controls);
            _logger.LogDebug("Saved {Count} controls to {FileName}", _controls.Count, ControlsFileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving controls to JSON");
        }
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
            IconName = "water_drop"
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

    public async Task<Control> UpdateControlAsync(Control control, CancellationToken cancellationToken = default)
    {
        var existingControl = _controls.FirstOrDefault(c => c.Id == control.Id);
        if (existingControl != null)
        {
            _controls.Remove(existingControl);
        }

        control.LastUpdated = DateTime.UtcNow;
        _controls.Add(control);
        _logger.LogInformation("Updated control {ControlName}", control.Name);
        await SaveControlsAsync();
        return control;
    }

    public async Task<Control> CreateControlAsync(Control control, CancellationToken cancellationToken = default)
    {
        control.Id = Guid.NewGuid();
        control.LastUpdated = DateTime.UtcNow;
        _controls.Add(control);
        _logger.LogInformation("Created control {ControlName} with ID {ControlId}", control.Name, control.Id);
        await SaveControlsAsync();
        return control;
    }

    public async Task DeleteControlAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var control = _controls.FirstOrDefault(c => c.Id == id);
        if (control != null)
        {
            control.IsActive = false;
            _logger.LogInformation("Deleted control {ControlName}", control.Name);
            await SaveControlsAsync();
        }
    }
}
