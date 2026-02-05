# MQTTnet Patterns Reference

## Contents
- Connection Management
- Topic Design Patterns
- Message Handling
- Error Handling and Reconnection
- Anti-Patterns

## Connection Management

### Managed Client with Auto-Reconnect

```csharp
public class MqttConnectionManager : IDisposable
{
    private readonly IMqttClient _client;
    private readonly MqttClientOptions _options;
    private readonly ILogger<MqttConnectionManager> _logger;
    private bool _isConnecting;

    public async Task ConnectWithRetryAsync(CancellationToken ct)
    {
        _client.DisconnectedAsync += async e =>
        {
            if (ct.IsCancellationRequested) return;
            
            _logger.LogWarning("MQTT disconnected: {Reason}", e.Reason);
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
            await TryReconnectAsync(ct);
        };

        await TryReconnectAsync(ct);
    }

    private async Task TryReconnectAsync(CancellationToken ct)
    {
        if (_isConnecting) return;
        _isConnecting = true;
        
        try
        {
            await _client.ConnectAsync(_options, ct);
            _logger.LogInformation("MQTT connected to {Broker}", _options.ChannelOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MQTT connection failed");
        }
        finally
        {
            _isConnecting = false;
        }
    }
}
```

### Connection Options Builder

```csharp
// GOOD - Complete options with credentials and timeouts
var options = new MqttClientOptionsBuilder()
    .WithTcpServer(broker, port)
    .WithClientId($"vandaemon-{Environment.MachineName}")
    .WithCredentials(username, password)
    .WithCleanSession(true)
    .WithKeepAlivePeriod(TimeSpan.FromSeconds(30))
    .WithTimeout(TimeSpan.FromSeconds(10))
    .WithWillQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
    .WithWillTopic($"vandaemon/api/status")
    .WithWillPayload("offline")
    .WithWillRetain(true)
    .Build();
```

## Topic Design Patterns

### VanDaemon Topic Structure

```
vandaemon/leddimmer/{deviceId}/status      → "online" | "offline"
vandaemon/leddimmer/{deviceId}/config      → JSON device info
vandaemon/leddimmer/{deviceId}/heartbeat   → JSON health data
vandaemon/leddimmer/{deviceId}/channel/{N}/state  → current value (0-255)
vandaemon/leddimmer/{deviceId}/channel/{N}/set    → command target
```

### Wildcard Subscriptions

```csharp
// Subscribe to all devices, single topic level
await _client.SubscribeAsync("vandaemon/leddimmer/+/status");

// Subscribe to all channels on specific device
await _client.SubscribeAsync("vandaemon/leddimmer/cabin-lights/channel/+/state");

// Subscribe to everything under a device (use sparingly)
await _client.SubscribeAsync("vandaemon/leddimmer/cabin-lights/#");
```

## Message Handling

### JSON Payload Parsing

```csharp
private async Task HandleConfigMessage(string deviceId, string payload)
{
    try
    {
        var config = JsonSerializer.Deserialize<DeviceConfig>(payload, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        
        if (config is null)
        {
            _logger.LogWarning("Invalid config from {DeviceId}", deviceId);
            return;
        }
        
        await RegisterDeviceAsync(deviceId, config);
    }
    catch (JsonException ex)
    {
        _logger.LogError(ex, "Failed to parse config from {DeviceId}", deviceId);
    }
}
```

### State Publishing with Retain

```csharp
// GOOD - Use retain for state that new subscribers need
var statusMessage = new MqttApplicationMessageBuilder()
    .WithTopic($"vandaemon/api/status")
    .WithPayload("online")
    .WithRetainFlag(true)  // New subscribers get current state
    .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
    .Build();

// GOOD - No retain for commands
var commandMessage = new MqttApplicationMessageBuilder()
    .WithTopic($"vandaemon/leddimmer/{deviceId}/channel/0/set")
    .WithPayload("128")
    .WithRetainFlag(false)  // Commands should not be replayed
    .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
    .Build();
```

## Error Handling and Reconnection

### Robust Message Handler

```csharp
private async Task OnMessageReceived(MqttApplicationMessageReceivedEventArgs e)
{
    try
    {
        var topic = e.ApplicationMessage.Topic;
        var payload = e.ApplicationMessage.PayloadSegment.Count > 0
            ? Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment)
            : string.Empty;

        _logger.LogDebug("MQTT received: {Topic} = {Payload}", topic, payload);

        await ProcessMessageAsync(topic, payload);
    }
    catch (Exception ex)
    {
        // NEVER let exceptions escape message handlers
        _logger.LogError(ex, "Error processing MQTT message on {Topic}", 
            e.ApplicationMessage.Topic);
    }
}
```

## Anti-Patterns

### WARNING: Blocking in Message Handlers

**The Problem:**

```csharp
// BAD - Blocking call in async handler
private Task HandleMessage(MqttApplicationMessageReceivedEventArgs e)
{
    var result = _httpClient.GetAsync(url).Result;  // BLOCKS!
    ProcessResult(result);
    return Task.CompletedTask;
}
```

**Why This Breaks:**
1. Blocks the MQTT client's internal thread pool
2. Can cause message queue backlog and timeouts
3. May trigger disconnect due to missed keep-alive

**The Fix:**

```csharp
// GOOD - Proper async handling
private async Task HandleMessage(MqttApplicationMessageReceivedEventArgs e)
{
    var result = await _httpClient.GetAsync(url);
    ProcessResult(result);
}
```

### WARNING: Missing Disposal

**The Problem:**

```csharp
// BAD - Client not disposed
public class MqttPlugin
{
    private IMqttClient _client;
    
    // No IDisposable, no cleanup
}
```

**Why This Breaks:**
1. Connection stays open after service stops
2. Resource leak on repeated restarts
3. Broker may reject new connections (max clients)

**The Fix:**

```csharp
// GOOD - Proper cleanup
public class MqttPlugin : IDisposable
{
    private IMqttClient? _client;
    private bool _disposed;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        _client?.DisconnectAsync().GetAwaiter().GetResult();
        _client?.Dispose();
    }
}
```

### WARNING: Hardcoded Topic Strings

**The Problem:**

```csharp
// BAD - Duplicated strings everywhere
await _client.SubscribeAsync("vandaemon/leddimmer/+/status");
// ... elsewhere ...
if (topic.StartsWith("vandaemon/leddimmer/"))
```

**The Fix:**

```csharp
// GOOD - Centralized topic builder
public static class MqttTopics
{
    public const string BaseTopic = "vandaemon/leddimmer";
    
    public static string DeviceStatus(string deviceId) => $"{BaseTopic}/{deviceId}/status";
    public static string ChannelState(string deviceId, int channel) => 
        $"{BaseTopic}/{deviceId}/channel/{channel}/state";
    public static string AllDeviceStatus() => $"{BaseTopic}/+/status";
}