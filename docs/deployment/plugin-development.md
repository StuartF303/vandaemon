# Plugin Development Guide

This guide explains how to create custom hardware integration plugins for VanDaemon.

## Overview

VanDaemon uses a plugin architecture to support different hardware integration methods. Plugins allow you to connect to various sensor and control systems without modifying the core application.

## Plugin Types

### 1. Sensor Plugins (`ISensorPlugin`)
Used for reading values from sensors (tank levels, temperatures, etc.)

### 2. Control Plugins (`IControlPlugin`)
Used for controlling devices (switches, dimmers, pumps, etc.)

## Creating a Sensor Plugin

### Step 1: Create a New Project

```bash
cd src/Backend/VanDaemon.Plugins
dotnet new classlib -n VanDaemon.Plugins.MyPlugin
cd VanDaemon.Plugins.MyPlugin
dotnet add reference ../VanDaemon.Plugins.Abstractions/VanDaemon.Plugins.Abstractions.csproj
```

### Step 2: Implement the Interface

Create a class that implements `ISensorPlugin`:

```csharp
using Microsoft.Extensions.Logging;
using VanDaemon.Plugins.Abstractions;

namespace VanDaemon.Plugins.MyPlugin;

public class MySensorPlugin : ISensorPlugin
{
    private readonly ILogger<MySensorPlugin> _logger;
    private Dictionary<string, object> _configuration = new();
    private bool _disposed;

    public string Name => "My Custom Sensor Plugin";
    public string Version => "1.0.0";

    public MySensorPlugin(ILogger<MySensorPlugin> logger)
    {
        _logger = logger;
    }

    public Task InitializeAsync(Dictionary<string, object> configuration, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Initializing {PluginName} v{Version}", Name, Version);
        _configuration = configuration;

        // Initialize your hardware connection here
        // Example: Connect to your device, open serial port, etc.

        return Task.CompletedTask;
    }

    public Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Testing connection for {PluginName}", Name);

        // Test your hardware connection
        // Return true if successful, false otherwise

        return Task.FromResult(true);
    }

    public async Task<double> ReadValueAsync(string sensorId, CancellationToken cancellationToken = default)
    {
        // Read the sensor value from your hardware
        // sensorId identifies which sensor to read
        // Return value should be a percentage (0-100) for tank levels

        _logger.LogDebug("Reading sensor {SensorId}", sensorId);

        // Example implementation:
        // var value = await ReadFromHardware(sensorId);
        // return value;

        return 0.0;
    }

    public async Task<IDictionary<string, double>> ReadAllValuesAsync(CancellationToken cancellationToken = default)
    {
        // Read all available sensors at once
        // This can be more efficient than reading individually

        var values = new Dictionary<string, double>();

        // Example implementation:
        // foreach (var sensorId in _configuredSensors)
        // {
        //     values[sensorId] = await ReadValueAsync(sensorId, cancellationToken);
        // }

        return values;
    }

    public void Dispose()
    {
        if (_disposed) return;

        _logger.LogInformation("Disposing {PluginName}", Name);

        // Clean up resources
        // Example: Close connections, dispose hardware interfaces

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
```

### Step 3: Add Configuration Support

Your plugin should support configuration through the dictionary passed to `InitializeAsync`:

```csharp
public Task InitializeAsync(Dictionary<string, object> configuration, CancellationToken cancellationToken = default)
{
    _configuration = configuration;

    // Read configuration values
    var ipAddress = configuration.GetValueOrDefault("IpAddress")?.ToString() ?? "192.168.1.100";
    var port = Convert.ToInt32(configuration.GetValueOrDefault("Port") ?? 502);

    // Use configuration to set up your connection
    _logger.LogInformation("Connecting to {IpAddress}:{Port}", ipAddress, port);

    return Task.CompletedTask;
}
```

## Creating a Control Plugin

Control plugins are similar but implement `IControlPlugin`:

```csharp
using Microsoft.Extensions.Logging;
using VanDaemon.Plugins.Abstractions;

namespace VanDaemon.Plugins.MyPlugin;

public class MyControlPlugin : IControlPlugin
{
    private readonly ILogger<MyControlPlugin> _logger;
    private Dictionary<string, object> _configuration = new();
    private bool _disposed;

    public string Name => "My Custom Control Plugin";
    public string Version => "1.0.0";

    public MyControlPlugin(ILogger<MyControlPlugin> logger)
    {
        _logger = logger;
    }

    public Task InitializeAsync(Dictionary<string, object> configuration, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Initializing {PluginName} v{Version}", Name, Version);
        _configuration = configuration;
        return Task.CompletedTask;
    }

    public Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    public async Task<bool> SetStateAsync(string controlId, object state, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Setting control {ControlId} to state {State}", controlId, state);

        // Send command to hardware
        // state could be bool (on/off), int (dimmer level), etc.

        // Return true if successful
        return true;
    }

    public async Task<object> GetStateAsync(string controlId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Reading control {ControlId} state", controlId);

        // Read current state from hardware
        // Return the current state (bool, int, etc.)

        return false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _logger.LogInformation("Disposing {PluginName}", Name);
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
```

## Registering Your Plugin

### Option 1: Modify Program.cs

Add your plugin to the dependency injection container in `VanDaemon.Api/Program.cs`:

```csharp
using VanDaemon.Plugins.MyPlugin;

// Register plugins
builder.Services.AddSingleton<ISensorPlugin, MySensorPlugin>();
builder.Services.AddSingleton<IControlPlugin, MyControlPlugin>();
```

### Option 2: Create a Plugin Discovery System

For more advanced scenarios, you can implement plugin discovery to load plugins dynamically from assemblies.

## Best Practices

### 1. Error Handling

Always handle errors gracefully:

```csharp
public async Task<double> ReadValueAsync(string sensorId, CancellationToken cancellationToken = default)
{
    try
    {
        var value = await ReadFromHardware(sensorId);
        return value;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error reading sensor {SensorId}", sensorId);
        // Return last known value or 0
        return 0.0;
    }
}
```

### 2. Logging

Use structured logging for better diagnostics:

```csharp
_logger.LogInformation("Sensor {SensorId} read value {Value}", sensorId, value);
_logger.LogWarning("Failed to read sensor {SensorId}, attempt {Attempt}", sensorId, attempt);
_logger.LogError(ex, "Critical error in plugin {PluginName}", Name);
```

### 3. Configuration Validation

Validate configuration early:

```csharp
public Task InitializeAsync(Dictionary<string, object> configuration, CancellationToken cancellationToken = default)
{
    if (!configuration.ContainsKey("IpAddress"))
    {
        throw new ArgumentException("IpAddress is required in configuration");
    }

    // Continue with initialization...
}
```

### 4. Resource Management

Always dispose of resources properly:

```csharp
private SerialPort? _serialPort;

public void Dispose()
{
    if (_disposed) return;

    _serialPort?.Close();
    _serialPort?.Dispose();
    _serialPort = null;

    _disposed = true;
    GC.SuppressFinalize(this);
}
```

### 5. Cancellation Support

Support cancellation tokens for long-running operations:

```csharp
public async Task<double> ReadValueAsync(string sensorId, CancellationToken cancellationToken = default)
{
    cancellationToken.ThrowIfCancellationRequested();

    var result = await _device.ReadAsync(sensorId, cancellationToken);
    return result;
}
```

### 6. Thread Safety

Ensure your plugin is thread-safe if it maintains state:

```csharp
private readonly SemaphoreSlim _lock = new(1, 1);

public async Task<double> ReadValueAsync(string sensorId, CancellationToken cancellationToken = default)
{
    await _lock.WaitAsync(cancellationToken);
    try
    {
        return await ReadFromHardware(sensorId);
    }
    finally
    {
        _lock.Release();
    }
}
```

## Example: Modbus Plugin

Here's a simplified example of a Modbus plugin:

```csharp
using NModbus;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using VanDaemon.Plugins.Abstractions;

namespace VanDaemon.Plugins.Modbus;

public class ModbusSensorPlugin : ISensorPlugin
{
    private readonly ILogger<ModbusSensorPlugin> _logger;
    private TcpClient? _tcpClient;
    private IModbusMaster? _modbusMaster;
    private string _ipAddress = string.Empty;
    private int _port = 502;

    public string Name => "Modbus Sensor Plugin";
    public string Version => "1.0.0";

    public ModbusSensorPlugin(ILogger<ModbusSensorPlugin> logger)
    {
        _logger = logger;
    }

    public async Task InitializeAsync(Dictionary<string, object> configuration, CancellationToken cancellationToken = default)
    {
        _ipAddress = configuration.GetValueOrDefault("IpAddress")?.ToString() ?? throw new ArgumentException("IpAddress required");
        _port = Convert.ToInt32(configuration.GetValueOrDefault("Port") ?? 502);

        _tcpClient = new TcpClient();
        await _tcpClient.ConnectAsync(_ipAddress, _port);

        var factory = new ModbusFactory();
        _modbusMaster = factory.CreateMaster(_tcpClient);

        _logger.LogInformation("Connected to Modbus device at {IpAddress}:{Port}", _ipAddress, _port);
    }

    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (_modbusMaster == null) return false;

        try
        {
            // Try to read a register to test connection
            await _modbusMaster.ReadHoldingRegistersAsync(1, 0, 1);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<double> ReadValueAsync(string sensorId, CancellationToken cancellationToken = default)
    {
        if (_modbusMaster == null)
        {
            throw new InvalidOperationException("Plugin not initialized");
        }

        // Parse sensorId to get register address
        // Format: "holding:1000" or "input:2000"
        var parts = sensorId.Split(':');
        var registerType = parts[0];
        var address = ushort.Parse(parts[1]);

        ushort[] registers;
        if (registerType == "holding")
        {
            registers = await _modbusMaster.ReadHoldingRegistersAsync(1, address, 1);
        }
        else
        {
            registers = await _modbusMaster.ReadInputRegistersAsync(1, address, 1);
        }

        // Convert register value to percentage (0-100)
        // Assuming register value is 0-10000 representing 0-100%
        return registers[0] / 100.0;
    }

    public async Task<IDictionary<string, double>> ReadAllValuesAsync(CancellationToken cancellationToken = default)
    {
        var values = new Dictionary<string, double>();
        // Read all configured sensors
        // This would need to be configured per installation
        return values;
    }

    public void Dispose()
    {
        _modbusMaster?.Dispose();
        _tcpClient?.Close();
        _tcpClient?.Dispose();
    }
}
```

## Testing Your Plugin

Create unit tests for your plugin:

```csharp
using Xunit;
using Moq;
using Microsoft.Extensions.Logging;

namespace VanDaemon.Plugins.MyPlugin.Tests;

public class MySensorPluginTests
{
    [Fact]
    public async Task ReadValueAsync_ShouldReturnValidValue()
    {
        // Arrange
        var logger = new Mock<ILogger<MySensorPlugin>>();
        var plugin = new MySensorPlugin(logger.Object);

        await plugin.InitializeAsync(new Dictionary<string, object>
        {
            ["IpAddress"] = "192.168.1.100",
            ["Port"] = 502
        });

        // Act
        var value = await plugin.ReadValueAsync("tank1");

        // Assert
        Assert.InRange(value, 0, 100);
    }
}
```

## Debugging Tips

1. **Use Logging Extensively**: Log all operations for easier debugging
2. **Test with Simulated Data**: Create mock hardware responses
3. **Use Breakpoints**: Debug step-by-step through your code
4. **Monitor Network Traffic**: Use Wireshark for network-based plugins
5. **Check Hardware Documentation**: Ensure you're following the protocol correctly

## Publishing Your Plugin

If you want to share your plugin:

1. Create a NuGet package
2. Add documentation
3. Include configuration examples
4. Provide sample code
5. Submit a pull request to the main repository

## Support

For help with plugin development:
- Check the [VanDaemon documentation](../../README.md)
- Review existing plugin implementations
- Open an issue on GitHub
- Ask in community discussions
