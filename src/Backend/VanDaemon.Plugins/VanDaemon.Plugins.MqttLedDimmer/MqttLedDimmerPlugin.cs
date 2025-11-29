using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using MQTTnet.Packets;
using System.Text;
using System.Text.Json;
using VanDaemon.Plugins.Abstractions;
using VanDaemon.Plugins.MqttLedDimmer.Models;

namespace VanDaemon.Plugins.MqttLedDimmer;

/// <summary>
/// MQTT-based LED Dimmer control plugin for VanDaemon
/// Supports multiple ESP32-based LED dimmer devices on the network
/// </summary>
public class MqttLedDimmerPlugin : IControlPlugin
{
    private readonly ILogger<MqttLedDimmerPlugin> _logger;
    private IManagedMqttClient? _mqttClient;
    private MqttLedDimmerConfiguration _config;
    private readonly Dictionary<string, LedDimmerDeviceInfo> _discoveredDevices = new();
    private bool _isInitialized;

    public string Name => "MQTT LED Dimmer";
    public string Version => "1.0.0";

    public MqttLedDimmerPlugin(ILogger<MqttLedDimmerPlugin> logger)
    {
        _logger = logger;
        _config = new MqttLedDimmerConfiguration();
    }

    public async Task InitializeAsync(Dictionary<string, object> configuration, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Initializing MQTT LED Dimmer plugin...");

            // Deserialize configuration
            var configJson = JsonSerializer.Serialize(configuration);
            _config = JsonSerializer.Deserialize<MqttLedDimmerConfiguration>(configJson)
                      ?? new MqttLedDimmerConfiguration();

            _logger.LogInformation("MQTT Broker: {Broker}:{Port}", _config.MqttBroker, _config.MqttPort);
            _logger.LogInformation("Auto-discovery: {AutoDiscovery}", _config.AutoDiscovery);
            _logger.LogInformation("Configured devices: {DeviceCount}", _config.Devices.Count);

            // Initialize MQTT client
            await InitializeMqttClientAsync();

            _isInitialized = true;
            _logger.LogInformation("MQTT LED Dimmer plugin initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize MQTT LED Dimmer plugin");
            throw;
        }
    }

    private async Task InitializeMqttClientAsync()
    {
        var factory = new MqttFactory();
        _mqttClient = factory.CreateManagedMqttClient();

        // Configure MQTT options
        var clientOptions = new MqttClientOptionsBuilder()
            .WithTcpServer(_config.MqttBroker, _config.MqttPort)
            .WithClientId($"vandaemon-leddimmer-{Guid.NewGuid():N}")
            .WithCleanSession();

        // Add credentials if configured
        if (!string.IsNullOrEmpty(_config.MqttUsername))
        {
            clientOptions.WithCredentials(_config.MqttUsername, _config.MqttPassword);
        }

        var managedOptions = new ManagedMqttClientOptionsBuilder()
            .WithClientOptions(clientOptions.Build())
            .WithAutoReconnectDelay(TimeSpan.FromSeconds(5))
            .Build();

        // Set up event handlers
        _mqttClient.ApplicationMessageReceivedAsync += OnMessageReceivedAsync;
        _mqttClient.ConnectedAsync += OnConnectedAsync;
        _mqttClient.DisconnectedAsync += OnDisconnectedAsync;
        _mqttClient.ConnectingFailedAsync += OnConnectingFailedAsync;

        // Connect
        await _mqttClient.StartAsync(managedOptions);

        _logger.LogInformation("MQTT client started");
    }

    private async Task OnConnectedAsync(MqttClientConnectedEventArgs e)
    {
        _logger.LogInformation("Connected to MQTT broker");

        // Subscribe to all LED dimmer topics
        var subscriptions = new List<MqttTopicFilter>();

        // Subscribe to wildcard topics for discovery and state updates
        subscriptions.Add(new MqttTopicFilterBuilder()
            .WithTopic($"{_config.BaseTopic}/+/status")
            .Build());

        subscriptions.Add(new MqttTopicFilterBuilder()
            .WithTopic($"{_config.BaseTopic}/+/config")
            .Build());

        subscriptions.Add(new MqttTopicFilterBuilder()
            .WithTopic($"{_config.BaseTopic}/+/channel/+/state")
            .Build());

        subscriptions.Add(new MqttTopicFilterBuilder()
            .WithTopic($"{_config.BaseTopic}/+/heartbeat")
            .Build());

        await _mqttClient!.SubscribeAsync(subscriptions);

        _logger.LogInformation("Subscribed to {Count} topics", subscriptions.Count);
    }

    private Task OnDisconnectedAsync(MqttClientDisconnectedEventArgs e)
    {
        _logger.LogWarning("Disconnected from MQTT broker: {Reason}", e.Reason);
        return Task.CompletedTask;
    }

    private Task OnConnectingFailedAsync(ConnectingFailedEventArgs e)
    {
        _logger.LogError(e.Exception, "Failed to connect to MQTT broker");
        return Task.CompletedTask;
    }

    private Task OnMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs e)
    {
        try
        {
            var topic = e.ApplicationMessage.Topic;
            var payload = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);

            _logger.LogDebug("MQTT message received: {Topic} = {Payload}", topic, payload);

            // Parse topic: vandaemon/leddimmer/{deviceId}/{rest}
            var parts = topic.Split('/');
            if (parts.Length < 3) return Task.CompletedTask;

            var deviceId = parts[2];

            // Handle different message types
            if (topic.EndsWith("/status"))
            {
                HandleStatusMessage(deviceId, payload);
            }
            else if (topic.EndsWith("/config"))
            {
                HandleConfigMessage(deviceId, payload);
            }
            else if (topic.Contains("/channel/") && topic.EndsWith("/state"))
            {
                HandleChannelStateMessage(deviceId, topic, payload);
            }
            else if (topic.EndsWith("/heartbeat"))
            {
                HandleHeartbeatMessage(deviceId, payload);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing MQTT message");
        }

        return Task.CompletedTask;
    }

    private void HandleStatusMessage(string deviceId, string payload)
    {
        var isOnline = payload.Equals("online", StringComparison.OrdinalIgnoreCase);

        if (_discoveredDevices.TryGetValue(deviceId, out var device))
        {
            device.IsOnline = isOnline;
            device.LastSeen = DateTime.UtcNow;
            _logger.LogInformation("Device {DeviceId} is now {Status}", deviceId, payload);
        }
        else if (isOnline)
        {
            // New device discovered
            _discoveredDevices[deviceId] = new LedDimmerDeviceInfo
            {
                DeviceId = deviceId,
                DeviceName = deviceId,
                IsOnline = true,
                LastSeen = DateTime.UtcNow
            };
            _logger.LogInformation("New device discovered: {DeviceId}", deviceId);
        }
    }

    private void HandleConfigMessage(string deviceId, string payload)
    {
        try
        {
            var config = JsonSerializer.Deserialize<LedDimmerDeviceInfo>(payload);
            if (config != null)
            {
                config.IsOnline = true;
                config.LastSeen = DateTime.UtcNow;

                _discoveredDevices[deviceId] = config;

                _logger.LogInformation(
                    "Device config received: {DeviceId} ({Name}), {Channels} channels, version {Version}",
                    config.DeviceId, config.DeviceName, config.Channels, config.Version);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse device config for {DeviceId}", deviceId);
        }
    }

    private void HandleChannelStateMessage(string deviceId, string topic, string payload)
    {
        try
        {
            // Extract channel number from topic: .../channel/N/state
            var parts = topic.Split('/');
            var channelIndex = parts.Length >= 5 ? int.Parse(parts[^2]) : -1;

            if (channelIndex >= 0 && int.TryParse(payload, out var brightness))
            {
                if (_discoveredDevices.TryGetValue(deviceId, out var device))
                {
                    device.ChannelStates[channelIndex] = brightness;
                    _logger.LogDebug("Device {DeviceId} channel {Channel} state: {Brightness}",
                        deviceId, channelIndex, brightness);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse channel state for {DeviceId}", deviceId);
        }
    }

    private void HandleHeartbeatMessage(string deviceId, string payload)
    {
        try
        {
            var heartbeat = JsonSerializer.Deserialize<LedDimmerHeartbeat>(payload);
            if (heartbeat != null && _discoveredDevices.TryGetValue(deviceId, out var device))
            {
                device.LastSeen = DateTime.UtcNow;
                _logger.LogDebug("Device {DeviceId} heartbeat: uptime={Uptime}s, rssi={Rssi}dBm",
                    deviceId, heartbeat.Uptime, heartbeat.Rssi);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse heartbeat for {DeviceId}", deviceId);
        }
    }

    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (_mqttClient == null) return false;

        try
        {
            await Task.Delay(100, cancellationToken); // Give MQTT client time to connect
            var isConnected = _mqttClient.IsConnected;
            _logger.LogInformation("MQTT connection test: {Status}", isConnected ? "SUCCESS" : "FAILED");
            return isConnected;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Connection test failed");
            return false;
        }
    }

    public async Task<bool> SetStateAsync(string controlId, object state, CancellationToken cancellationToken = default)
    {
        if (_mqttClient == null || !_mqttClient.IsConnected)
        {
            _logger.LogWarning("Cannot set state: MQTT client not connected");
            return false;
        }

        try
        {
            // Control ID format: "{deviceId}-CH{N}" e.g. "cabin-lights-CH0"
            var parts = controlId.Split('-', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) return false;

            var deviceId = string.Join("-", parts.Take(parts.Length - 1));
            var channelPart = parts[^1]; // Last part

            if (!channelPart.StartsWith("CH")) return false;

            var channelNum = int.Parse(channelPart[2..]);

            // Convert state to percentage (0-100) then to brightness (0-255)
            // VanDaemon uses 0-100 for dimmer controls, hardware uses 0-255
            var percentage = state switch
            {
                int i => i,
                double d => (int)d,
                bool b => b ? 100 : 0,
                _ => 0
            };

            percentage = Math.Clamp(percentage, 0, 100);

            // Convert 0-100 to 0-255
            var brightness = (int)Math.Round(percentage * 2.55);

            // Publish MQTT command
            var topic = $"{_config.BaseTopic}/{deviceId}/channel/{channelNum}/set";
            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(brightness.ToString())
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();

            await _mqttClient.EnqueueAsync(message);

            _logger.LogInformation("Set {ControlId} to {Brightness}", controlId, brightness);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set state for {ControlId}", controlId);
            return false;
        }
    }

    public async Task<object> GetStateAsync(string controlId, CancellationToken cancellationToken = default)
    {
        try
        {
            // Parse control ID
            var parts = controlId.Split('-', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) return 0;

            var deviceId = string.Join("-", parts.Take(parts.Length - 1));
            var channelPart = parts[^1];

            if (!channelPart.StartsWith("CH")) return 0;

            var channelNum = int.Parse(channelPart[2..]);

            // Get from discovered devices
            if (_discoveredDevices.TryGetValue(deviceId, out var device))
            {
                if (device.ChannelStates.TryGetValue(channelNum, out var brightness))
                {
                    // Convert from 0-255 to 0-100 percentage
                    var percentage = (int)Math.Round(brightness / 2.55);
                    return Math.Clamp(percentage, 0, 100);
                }
            }

            return 0; // Default to off
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get state for {ControlId}", controlId);
            return 0; // Return 0 on error
        }
    }

    /// <summary>
    /// Get all discovered devices
    /// </summary>
    public IReadOnlyDictionary<string, LedDimmerDeviceInfo> GetDiscoveredDevices()
    {
        return _discoveredDevices;
    }

    public void Dispose()
    {
        _logger.LogInformation("Disposing MQTT LED Dimmer plugin");

        _mqttClient?.StopAsync().Wait();
        _mqttClient?.Dispose();
    }
}
