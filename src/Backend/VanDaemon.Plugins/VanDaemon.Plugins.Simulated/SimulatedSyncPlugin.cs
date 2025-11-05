using Microsoft.Extensions.Logging;
using VanDaemon.Plugins.Abstractions;

namespace VanDaemon.Plugins.Simulated;

/// <summary>
/// Simulated sync plugin that just logs value changes
/// </summary>
public class SimulatedSyncPlugin : IControlPlugin
{
    private readonly ILogger<SimulatedSyncPlugin> _logger;
    private bool _isInitialized;

    public SimulatedSyncPlugin(ILogger<SimulatedSyncPlugin> logger)
    {
        _logger = logger;
    }

    public string Name => "Simulated Sync";
    public string Version => "1.0.0";

    public Task InitializeAsync(Dictionary<string, object> configuration, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Simulated Sync Plugin initialized");
        _isInitialized = true;
        return Task.CompletedTask;
    }

    public Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_isInitialized);
    }

    public Task<bool> SetStateAsync(string controlId, object state, CancellationToken cancellationToken = default)
    {
        if (!_isInitialized)
        {
            throw new InvalidOperationException("Plugin not initialized");
        }

        var stateValue = Convert.ToBoolean(state);
        _logger.LogInformation("Control {ControlId} set to {State}", controlId, stateValue ? "ON" : "OFF");
        return Task.FromResult(true);
    }

    public Task<object> GetStateAsync(string controlId, CancellationToken cancellationToken = default)
    {
        if (!_isInitialized)
        {
            throw new InvalidOperationException("Plugin not initialized");
        }

        // Return a random state for demo purposes
        var randomState = Random.Shared.Next(2) == 1;
        return Task.FromResult<object>(randomState);
    }

    public void Dispose()
    {
        _logger.LogInformation("Simulated Sync Plugin disposed");
        _isInitialized = false;
    }
}
