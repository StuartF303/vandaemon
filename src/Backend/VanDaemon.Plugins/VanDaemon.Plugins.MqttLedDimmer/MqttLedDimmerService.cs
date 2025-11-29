using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VanDaemon.Application.Interfaces;
using VanDaemon.Core.Entities;
using VanDaemon.Core.Enums;
using VanDaemon.Plugins.MqttLedDimmer.Models;

namespace VanDaemon.Plugins.MqttLedDimmer;

/// <summary>
/// Background service that integrates MQTT LED Dimmer devices with VanDaemon Control system
/// Handles device discovery, control entity creation, and state synchronization
/// </summary>
public class MqttLedDimmerService : BackgroundService
{
    private readonly ILogger<MqttLedDimmerService> _logger;
    private readonly MqttLedDimmerPlugin _plugin;
    private readonly IControlService _controlService;
    private readonly HashSet<string> _registeredControls = new();
    private readonly PeriodicTimer _discoveryTimer = new(TimeSpan.FromSeconds(10));
    private readonly PeriodicTimer _stateRefreshTimer = new(TimeSpan.FromSeconds(5));

    public MqttLedDimmerService(
        ILogger<MqttLedDimmerService> logger,
        MqttLedDimmerPlugin plugin,
        IControlService controlService)
    {
        _logger = logger;
        _plugin = plugin;
        _controlService = controlService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MQTT LED Dimmer Service starting...");

        // Wait a bit for plugin to initialize and discover devices
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        // Start discovery and state refresh loops
        var discoveryTask = RunDiscoveryLoopAsync(stoppingToken);
        var stateRefreshTask = RunStateRefreshLoopAsync(stoppingToken);

        await Task.WhenAll(discoveryTask, stateRefreshTask);
    }

    private async Task RunDiscoveryLoopAsync(CancellationToken stoppingToken)
    {
        while (await _discoveryTimer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await DiscoverAndRegisterDevicesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in device discovery loop");
            }
        }
    }

    private async Task RunStateRefreshLoopAsync(CancellationToken stoppingToken)
    {
        while (await _stateRefreshTimer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await RefreshControlStatesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in state refresh loop");
            }
        }
    }

    private async Task DiscoverAndRegisterDevicesAsync(CancellationToken cancellationToken)
    {
        var devices = _plugin.GetDiscoveredDevices();

        foreach (var (deviceId, deviceInfo) in devices)
        {
            if (!deviceInfo.IsOnline)
            {
                continue; // Skip offline devices
            }

            // Register each channel as a Control entity
            for (int channel = 0; channel < deviceInfo.Channels; channel++)
            {
                var controlId = $"{deviceId}-CH{channel}";

                if (_registeredControls.Contains(controlId))
                {
                    continue; // Already registered
                }

                try
                {
                    // Get channel brightness (0-255) and convert to percentage (0-100)
                    var brightness = deviceInfo.ChannelStates.GetValueOrDefault(channel, 0);
                    var percentage = (int)Math.Round(brightness / 2.55);
                    percentage = Math.Clamp(percentage, 0, 100);

                    // Create Control entity
                    var control = new Control
                    {
                        Name = $"{deviceInfo.DeviceName} - Channel {channel + 1}",
                        Type = ControlType.Dimmer,
                        State = percentage,
                        IconName = "mdi-lightbulb-outline",
                        IsActive = true,
                        ControlPlugin = "MqttLedDimmer",
                        ControlConfiguration = new Dictionary<string, object>
                        {
                            ["DeviceId"] = deviceId,
                            ["Channel"] = channel,
                            ["ControlId"] = controlId
                        }
                    };

                    // Register with ControlService
                    var created = await _controlService.CreateControlAsync(control, cancellationToken);

                    _registeredControls.Add(controlId);

                    _logger.LogInformation(
                        "Registered control: {ControlName} (ID: {ControlId})",
                        control.Name, created.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to register control {ControlId}", controlId);
                }
            }
        }
    }

    private async Task RefreshControlStatesAsync(CancellationToken cancellationToken)
    {
        // Get all controls managed by this plugin
        var controls = await _controlService.GetAllControlsAsync(cancellationToken);
        var ledDimmerControls = controls.Where(c => c.ControlPlugin == "MqttLedDimmer").ToList();

        foreach (var control in ledDimmerControls)
        {
            if (!control.ControlConfiguration.TryGetValue("ControlId", out var controlIdObj))
            {
                continue;
            }

            var controlId = controlIdObj.ToString()!;

            try
            {
                // Get current state from plugin
                var currentState = await _plugin.GetStateAsync(controlId, cancellationToken);

                if (currentState != null && currentState != control.State)
                {
                    // Update control state
                    control.State = currentState;
                    await _controlService.UpdateControlAsync(control, cancellationToken);

                    _logger.LogDebug("Updated control {ControlId} state to {State}", controlId, currentState);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to refresh state for control {ControlId}", controlId);
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("MQTT LED Dimmer Service stopping...");

        _discoveryTimer.Dispose();
        _stateRefreshTimer.Dispose();

        await base.StopAsync(cancellationToken);
    }
}
