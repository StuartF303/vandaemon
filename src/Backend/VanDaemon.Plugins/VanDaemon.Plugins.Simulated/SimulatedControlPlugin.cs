using Microsoft.Extensions.Logging;
using VanDaemon.Plugins.Abstractions;

namespace VanDaemon.Plugins.Simulated;

/// <summary>
/// Simulated control plugin for testing and development
/// Simulates controlling hardware without requiring actual hardware
/// </summary>
public class SimulatedControlPlugin : IControlPlugin
{
    private readonly ILogger<SimulatedControlPlugin> _logger;
    private readonly Dictionary<string, object> _controlStates = new();
    private bool _disposed;

    public string Name => "Simulated Control Plugin";
    public string Version => "1.0.0";

    public SimulatedControlPlugin(ILogger<SimulatedControlPlugin> logger)
    {
        _logger = logger;
    }

    public Task InitializeAsync(Dictionary<string, object> configuration, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Initializing {PluginName} v{Version}", Name, Version);

        // Initialize some default control states
        _controlStates["light_main"] = false;
        _controlStates["light_dimmer"] = 0;
        _controlStates["water_pump"] = false;
        _controlStates["heater"] = false;

        return Task.CompletedTask;
    }

    public Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Testing connection for {PluginName}", Name);
        return Task.FromResult(true);
    }

    public Task<bool> SetStateAsync(string controlId, object state, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Setting control {ControlId} to {State}", controlId, state);
        _controlStates[controlId] = state;
        return Task.FromResult(true);
    }

    public Task<object> GetStateAsync(string controlId, CancellationToken cancellationToken = default)
    {
        if (!_controlStates.ContainsKey(controlId))
        {
            _logger.LogWarning("Control {ControlId} not found, returning default state", controlId);
            return Task.FromResult<object>(false);
        }

        var state = _controlStates[controlId];
        _logger.LogDebug("Read control {ControlId}: {State}", controlId, state);
        return Task.FromResult(state);
    }

    public void Dispose()
    {
        if (_disposed) return;

        _logger.LogInformation("Disposing {PluginName}", Name);
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
