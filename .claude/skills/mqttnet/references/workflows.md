# MQTTnet Workflows Reference

## Contents
- Plugin Initialization Workflow
- Device Discovery Workflow
- State Synchronization Workflow
- Testing MQTT Integration

## Plugin Initialization Workflow

### Complete Startup Sequence

```csharp
public async Task InitializeAsync(Dictionary<string, object> config, CancellationToken ct)
{
    // 1. Parse configuration
    var broker = config.GetValueOrDefault("MqttBroker", "localhost")?.ToString()!;
    var port = Convert.ToInt32(config.GetValueOrDefault("MqttPort", 1883));
    var baseTopic = config.GetValueOrDefault("BaseTopic", "vandaemon/leddimmer")?.ToString()!;

    // 2. Create client
    var factory = new MqttFactory();
    _mqttClient = factory.CreateMqttClient();

    // 3. Build options with LWT (Last Will and Testament)
    _options = new MqttClientOptionsBuilder()
        .WithTcpServer(broker, port)
        .WithClientId($"vandaemon-{Guid.NewGuid():N}")
        .WithWillTopic($"{baseTopic}/api/status")
        .WithWillPayload("offline")
        .WithWillRetain(true)
        .Build();

    // 4. Register handlers BEFORE connecting
    _mqttClient.ApplicationMessageReceivedAsync += HandleMessageAsync;
    _mqttClient.ConnectedAsync += OnConnectedAsync;
    _mqttClient.DisconnectedAsync += OnDisconnectedAsync;

    // 5. Connect
    await _mqttClient.ConnectAsync(_options, ct);

    // 6. Subscribe to discovery topics
    await SubscribeToDiscoveryTopicsAsync(ct);

    // 7. Announce online status
    await PublishStatusAsync("online", ct);
}
```

Copy this checklist and track progress:
- [ ] Step 1: Parse and validate configuration
- [ ] Step 2: Create MqttFactory and client
- [ ] Step 3: Build options with credentials and LWT
- [ ] Step 4: Register event handlers
- [ ] Step 5: Connect to broker
- [ ] Step 6: Subscribe to required topics
- [ ] Step 7: Publish online status

## Device Discovery Workflow

### Auto-Discovery Pattern

The MqttLedDimmerService discovers devices by listening for config messages:

```csharp
public async Task DiscoverDevicesAsync(CancellationToken ct)
{
    // Devices announce themselves via config topic
    // vandaemon/leddimmer/{deviceId}/config → JSON
    
    foreach (var device in _discoveredDevices.Values)
    {
        if (!_registeredDevices.Contains(device.DeviceId))
        {
            await RegisterDeviceControlsAsync(device, ct);
            _registeredDevices.Add(device.DeviceId);
            _logger.LogInformation("Device discovered: {DeviceId}", device.DeviceId);
        }
    }
}

private async Task RegisterDeviceControlsAsync(DeviceInfo device, CancellationToken ct)
{
    for (int i = 0; i < device.Channels; i++)
    {
        var control = new Control
        {
            Id = Guid.NewGuid(),
            Name = $"{device.DeviceName} - Channel {i}",
            Type = ControlType.Dimmer,
            State = 0,
            ControlPlugin = "MqttLedDimmer",
            ControlConfiguration = new Dictionary<string, object>
            {
                ["deviceId"] = device.DeviceId,
                ["channel"] = i
            },
            IsActive = true
        };
        
        await _controlService.CreateControlAsync(control, ct);
    }
}
```

### Discovery Message Format

```json
{
  "deviceId": "cabin-lights",
  "deviceName": "Cabin LED Dimmer",
  "channels": 8,
  "version": "1.0.0",
  "variant": "8CH"
}
```

## State Synchronization Workflow

### Bidirectional State Sync

```csharp
// Receiving state from device
private async Task HandleChannelStateAsync(string deviceId, int channel, string payload)
{
    if (!int.TryParse(payload, out var brightness))
    {
        _logger.LogWarning("Invalid brightness value: {Payload}", payload);
        return;
    }
    
    // Update local state
    _deviceStates[$"{deviceId}-CH{channel}"] = brightness;
    
    // Notify VanDaemon via SignalR
    await _hubContext.Clients.Group("controls").SendAsync(
        "ControlStateChanged", 
        GetControlId(deviceId, channel), 
        brightness,
        $"{deviceId} Channel {channel}");
}

// Sending command to device
public async Task<bool> SetStateAsync(string controlId, object state, CancellationToken ct)
{
    var (deviceId, channel) = ParseControlId(controlId);
    var brightness = Convert.ToInt32(state);
    
    var message = new MqttApplicationMessageBuilder()
        .WithTopic($"{_baseTopic}/{deviceId}/channel/{channel}/set")
        .WithPayload(brightness.ToString())
        .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
        .Build();
    
    var result = await _mqttClient.PublishAsync(message, ct);
    return result.IsSuccess;
}
```

### State Refresh Loop

```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    await _plugin.InitializeAsync(_config, stoppingToken);
    
    while (!stoppingToken.IsCancellationRequested)
    {
        try
        {
            // 1. Discover new devices
            await _plugin.DiscoverDevicesAsync(stoppingToken);
            
            // 2. Refresh states from discovered devices
            await _plugin.RefreshStatesAsync(stoppingToken);
            
            // 3. Wait before next cycle
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            break;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in MQTT service loop");
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
```

## Testing MQTT Integration

### Manual Testing with mosquitto_sub/pub

```bash
# Terminal 1: Subscribe to all LED dimmer topics
mosquitto_sub -h localhost -t "vandaemon/leddimmer/#" -v

# Terminal 2: Simulate device coming online
mosquitto_pub -h localhost -t "vandaemon/leddimmer/test-device/status" -m "online"

# Simulate config announcement
mosquitto_pub -h localhost -t "vandaemon/leddimmer/test-device/config" \
  -m '{"deviceId":"test-device","deviceName":"Test Dimmer","channels":8,"version":"1.0.0"}'

# Simulate channel state
mosquitto_pub -h localhost -t "vandaemon/leddimmer/test-device/channel/0/state" -m "128"

# Send brightness command
mosquitto_pub -h localhost -t "vandaemon/leddimmer/test-device/channel/0/set" -m "255"
```

### Unit Testing with Mock Client

```csharp
[Fact]
public async Task SetStateAsync_PublishesCorrectMessage()
{
    // Arrange
    var mockClient = new Mock<IMqttClient>();
    mockClient.Setup(x => x.PublishAsync(It.IsAny<MqttApplicationMessage>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(new MqttClientPublishResult(null, MqttClientPublishReasonCode.Success, null, null));
    
    var plugin = new MqttLedDimmerPlugin(_logger.Object);
    plugin.SetClient(mockClient.Object);  // Test seam
    
    // Act
    var result = await plugin.SetStateAsync("cabin-lights-CH0", 128, CancellationToken.None);
    
    // Assert
    result.Should().BeTrue();
    mockClient.Verify(x => x.PublishAsync(
        It.Is<MqttApplicationMessage>(m => 
            m.Topic == "vandaemon/leddimmer/cabin-lights/channel/0/set" &&
            Encoding.UTF8.GetString(m.PayloadSegment) == "128"),
        It.IsAny<CancellationToken>()),
        Times.Once);
}
```

### Integration Testing Checklist

Copy this checklist and track progress:
- [ ] Step 1: Start MQTT broker (Docker: `docker run -p 1883:1883 eclipse-mosquitto:2`)
- [ ] Step 2: Run VanDaemon API with MQTT plugin enabled
- [ ] Step 3: Verify API connects: check logs for "MQTT connected"
- [ ] Step 4: Simulate device: publish to `{base}/test-device/config`
- [ ] Step 5: Verify discovery: check Controls API for new dimmer controls
- [ ] Step 6: Test command: POST to `/api/controls/{id}/state` with brightness value
- [ ] Step 7: Verify MQTT message: mosquitto_sub should show `/set` topic
- [ ] Step 8: Simulate state change: publish to `/state` topic
- [ ] Step 9: Verify SignalR broadcast: frontend should update

### Validation Loop

1. Make configuration changes
2. Validate: Check API logs and `mosquitto_sub` output
3. If connection fails, verify broker address and credentials
4. If messages don't arrive, check topic spelling and QoS settings
5. Only proceed when bidirectional communication works