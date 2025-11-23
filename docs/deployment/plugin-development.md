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

## Example: Modbus Control Plugin (Waveshare Relay)

VanDaemon includes a production-ready Modbus control plugin that supports the **Waveshare 8-Channel PoE ETH Relay** and generic Modbus devices.

### Waveshare 8-Channel PoE ETH Relay

**Device Specifications:**
- 8 relay channels (10A @ 250VAC/30VDC)
- Modbus TCP protocol support
- PoE powered (IEEE 802.3af) or external DC power
- Default Modbus port: 502
- Register type: Coils (FC05 - Write Single Coil)
- Register addresses: 0-7 for relays 1-8

**Configuration Example:**
When adding a control in the VanDaemon web UI:
1. Select "Control Provider" → "Modbus Device"
2. Choose "Device Type" → "Waveshare 8-Channel PoE Relay"
3. Enter "Modbus Address": `192.168.1.100:502`
4. Select "Relay Channel": e.g., "Relay 1 (Register 0)"
5. The plugin automatically uses Coil register type

**Manual Configuration (Generic Modbus):**
If not using the Waveshare preset, configure manually:
- Modbus Address: `192.168.1.100:502`
- Register Address: `0` (or custom)
- Register Type: `Coil` (for on/off) or `HoldingRegister`

### Implementation Details

The Modbus plugin uses **FluentModbus** library and supports:

- **Connection:** Per-operation (connect, write/read, disconnect)
- **Protocol:** Modbus TCP over Ethernet
- **Function Codes:** FC01 (Read Coils), FC03 (Read Holding Registers), FC05 (Write Single Coil), FC06 (Write Single Register)
- **Endianness:** Big Endian (Modbus standard)
- **Timeouts:** 5s connection, 1s read/write

### Code Example

Here's a simplified example based on the actual implementation:

```csharp
using FluentModbus;
using System.Net;
using Microsoft.Extensions.Logging;
using VanDaemon.Plugins.Abstractions;

namespace VanDaemon.Plugins.Modbus;

public class ModbusControlPlugin : IControlPlugin
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

    public async Task<bool> SetStateAsync(string modbusAddress, int register,
        string registerType, object state, CancellationToken cancellationToken = default)
    {
        try
        {
            // Parse address and port
            var parts = modbusAddress.Split(':');
            var host = parts[0].Trim();
            var port = parts.Length > 1 && int.TryParse(parts[1], out var p) ? p : 502;

            // Convert state to boolean
            bool boolState = state switch
            {
                bool b => b,
                int i => i != 0,
                string s => bool.TryParse(s, out var result) && result,
                _ => false
            };

            _logger.LogInformation("Writing to {Host}:{Port}, Register={Register}, State={State}",
                host, port, register, boolState);

            // Create Modbus TCP client
            using var client = new ModbusTcpClient();
            client.ConnectTimeout = 5000;
            client.WriteTimeout = 1000;

            // Connect to device
            var endpoint = new IPEndPoint(IPAddress.Parse(host), port);
            await Task.Run(() => client.Connect(endpoint, ModbusEndianness.BigEndian), cancellationToken);

            byte unitIdentifier = 0; // Modbus unit ID
            ushort registerAddress = (ushort)register;

            if (registerType.Equals("Coil", StringComparison.OrdinalIgnoreCase))
            {
                // Write single coil (FC05) - for relay on/off
                await Task.Run(() => client.WriteSingleCoil(unitIdentifier, registerAddress, boolState),
                    cancellationToken);
            }
            else if (registerType.Equals("HoldingRegister", StringComparison.OrdinalIgnoreCase))
            {
                // Write single holding register (FC06)
                short value = (short)(boolState ? 1 : 0);
                await Task.Run(() => client.WriteSingleRegister(unitIdentifier, registerAddress, value),
                    cancellationToken);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set Modbus state");
            return false;
        }
    }

    public void Dispose()
    {
        // FluentModbus client is disposed per-operation
        _logger.LogInformation("Disposing {PluginName}", Name);
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
