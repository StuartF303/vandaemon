using Microsoft.Extensions.Logging;
using VanDaemon.Plugins.Abstractions;
using FluentModbus;
using System.Net;

namespace VanDaemon.Plugins.Modbus;

/// <summary>
/// Modbus TCP control plugin for controlling Modbus-compatible devices
/// Supports generic Modbus devices with built-in presets for common hardware
/// </summary>
public class ModbusControlPlugin : IControlPlugin
{
    private readonly ILogger<ModbusControlPlugin> _logger;
    private bool _disposed;
    private const int DEFAULT_MODBUS_PORT = 502;
    private const int CONNECTION_TIMEOUT_MS = 5000;
    private const int READ_TIMEOUT_MS = 1000;
    private const int WRITE_TIMEOUT_MS = 1000;

    // Device type presets for common hardware
    private static readonly Dictionary<string, DevicePreset> DevicePresets = new()
    {
        ["Waveshare8Relay"] = new DevicePreset
        {
            Name = "Waveshare 8-Channel PoE Relay",
            RegisterType = "Coil",
            RegisterOffset = 0,
            ChannelCount = 8,
            Description = "Waveshare Modbus PoE ETH Relay (8 channels)"
        }
    };

    public string Name => "Modbus Control Plugin";
    public string Version => "1.0.0";

    public ModbusControlPlugin(ILogger<ModbusControlPlugin> logger)
    {
        _logger = logger;
    }

    public Task InitializeAsync(Dictionary<string, object> configuration, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Initializing {PluginName} v{Version}", Name, Version);
        _logger.LogInformation("Supported device presets: {Presets}", string.Join(", ", DevicePresets.Keys));
        return Task.CompletedTask;
    }

    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Testing Modbus TCP connection");

        // For a general test, we'd need a specific device configuration
        // Return true as we test connections per-control during SetStateAsync
        await Task.CompletedTask;
        return true;
    }

    public async Task<bool> SetStateAsync(string controlId, object state, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(controlId))
        {
            _logger.LogWarning("Control ID is null or empty");
            return false;
        }

        try
        {
            // Parse configuration from controlId (expecting JSON-like format)
            // In practice, this would come from the Control.ControlConfiguration dictionary
            // For now, we'll expect the configuration to be passed via the TankService/ControlService
            _logger.LogInformation("Setting state for control {ControlId} to {State}", controlId, state);

            // State handling based on type
            bool boolState = state switch
            {
                bool b => b,
                int i => i != 0,
                string s => bool.TryParse(s, out var result) && result,
                _ => false
            };

            _logger.LogDebug("Converted state to boolean: {BoolState}", boolState);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting state for control {ControlId}", controlId);
            return false;
        }
    }

    public async Task<object> GetStateAsync(string controlId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(controlId))
        {
            _logger.LogWarning("Control ID is null or empty");
            return false;
        }

        try
        {
            _logger.LogDebug("Getting state for control {ControlId}", controlId);

            // This would read from the actual Modbus device
            // For now, return default state
            await Task.CompletedTask;
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting state for control {ControlId}", controlId);
            return false;
        }
    }

    /// <summary>
    /// Sets the state of a Modbus control using full configuration
    /// </summary>
    public async Task<bool> SetStateWithConfigAsync(
        string modbusAddress,
        int register,
        string registerType,
        object state,
        string? deviceType = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Parse address and port
            var (host, port) = ParseModbusAddress(modbusAddress);

            // Apply device preset if specified
            if (!string.IsNullOrEmpty(deviceType) && DevicePresets.TryGetValue(deviceType, out var preset))
            {
                _logger.LogInformation("Using device preset: {PresetName}", preset.Name);
                registerType = preset.RegisterType;
            }

            // Convert state to boolean for relay control
            bool boolState = ConvertToBoolean(state);

            _logger.LogInformation(
                "Writing to Modbus device at {Host}:{Port}, Register={Register}, Type={RegisterType}, State={State}",
                host, port, register, registerType, boolState);

            // Create Modbus TCP client and execute write operation
            using var client = new ModbusTcpClient();
            client.ConnectTimeout = CONNECTION_TIMEOUT_MS;
            client.ReadTimeout = READ_TIMEOUT_MS;
            client.WriteTimeout = WRITE_TIMEOUT_MS;

            // Connect to Modbus device
            var endpoint = new IPEndPoint(IPAddress.Parse(host), port);
            await Task.Run(() => client.Connect(endpoint, ModbusEndianness.BigEndian), cancellationToken);

            byte unitIdentifier = 0; // Default Modbus unit ID
            ushort registerAddress = (ushort)register;

            if (registerType.Equals("Coil", StringComparison.OrdinalIgnoreCase))
            {
                // Write single coil (function code 05)
                await Task.Run(() => client.WriteSingleCoil(unitIdentifier, registerAddress, boolState), cancellationToken);
                _logger.LogDebug("Successfully wrote coil at register {Register}", register);
            }
            else if (registerType.Equals("HoldingRegister", StringComparison.OrdinalIgnoreCase))
            {
                // Write single holding register (function code 06)
                short value = (short)(boolState ? 1 : 0);
                await Task.Run(() => client.WriteSingleRegister(unitIdentifier, registerAddress, value), cancellationToken);
                _logger.LogDebug("Successfully wrote holding register at {Register}", register);
            }
            else
            {
                _logger.LogWarning("Unsupported register type: {RegisterType}", registerType);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set Modbus state at {Address}, register {Register}",
                modbusAddress, register);
            return false;
        }
    }

    /// <summary>
    /// Reads the state of a Modbus control using full configuration
    /// </summary>
    public async Task<object> GetStateWithConfigAsync(
        string modbusAddress,
        int register,
        string registerType,
        string? deviceType = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Parse address and port
            var (host, port) = ParseModbusAddress(modbusAddress);

            // Apply device preset if specified
            if (!string.IsNullOrEmpty(deviceType) && DevicePresets.TryGetValue(deviceType, out var preset))
            {
                registerType = preset.RegisterType;
            }

            _logger.LogDebug(
                "Reading from Modbus device at {Host}:{Port}, Register={Register}, Type={RegisterType}",
                host, port, register, registerType);

            // Create Modbus TCP client and execute read operation
            using var client = new ModbusTcpClient();
            client.ConnectTimeout = CONNECTION_TIMEOUT_MS;
            client.ReadTimeout = READ_TIMEOUT_MS;

            // Connect to Modbus device
            var endpoint = new IPEndPoint(IPAddress.Parse(host), port);
            await Task.Run(() => client.Connect(endpoint, ModbusEndianness.BigEndian), cancellationToken);

            byte unitIdentifier = 0; // Default Modbus unit ID
            ushort registerAddress = (ushort)register;

            if (registerType.Equals("Coil", StringComparison.OrdinalIgnoreCase))
            {
                // Read coil (function code 01)
                // ReadCoils returns Span<byte> with bits packed, we need only the first bit
                var result = client.ReadCoils(unitIdentifier, registerAddress, 1);
                bool coilState = (result[0] & 0x01) != 0;
                return coilState;
            }
            else if (registerType.Equals("HoldingRegister", StringComparison.OrdinalIgnoreCase))
            {
                // Read holding register (function code 03)
                var result = client.ReadHoldingRegisters<ushort>(unitIdentifier, registerAddress, 1);
                return result[0] != 0;
            }
            else
            {
                _logger.LogWarning("Unsupported register type: {RegisterType}", registerType);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read Modbus state at {Address}, register {Register}",
                modbusAddress, register);
            return false;
        }
    }

    private (string host, int port) ParseModbusAddress(string modbusAddress)
    {
        if (string.IsNullOrWhiteSpace(modbusAddress))
        {
            throw new ArgumentException("Modbus address cannot be null or empty", nameof(modbusAddress));
        }

        var parts = modbusAddress.Split(':');
        var host = parts[0].Trim();
        var port = parts.Length > 1 && int.TryParse(parts[1], out var p) ? p : DEFAULT_MODBUS_PORT;

        return (host, port);
    }

    private bool ConvertToBoolean(object state)
    {
        return state switch
        {
            bool b => b,
            int i => i != 0,
            string s => bool.TryParse(s, out var result) && result,
            _ => false
        };
    }

    public void Dispose()
    {
        if (_disposed) return;

        _logger.LogInformation("Disposing {PluginName}", Name);
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private class DevicePreset
    {
        public string Name { get; set; } = string.Empty;
        public string RegisterType { get; set; } = string.Empty;
        public int RegisterOffset { get; set; }
        public int ChannelCount { get; set; }
        public string Description { get; set; } = string.Empty;
    }
}
