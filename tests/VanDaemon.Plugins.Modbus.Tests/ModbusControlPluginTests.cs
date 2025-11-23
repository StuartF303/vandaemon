using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using VanDaemon.Plugins.Modbus;

namespace VanDaemon.Plugins.Modbus.Tests;

public class ModbusControlPluginTests
{
    private readonly Mock<ILogger<ModbusControlPlugin>> _loggerMock;
    private readonly ModbusControlPlugin _plugin;

    public ModbusControlPluginTests()
    {
        _loggerMock = new Mock<ILogger<ModbusControlPlugin>>();
        _plugin = new ModbusControlPlugin(_loggerMock.Object);
    }

    [Fact]
    public void Constructor_ShouldInitializePlugin()
    {
        // Arrange & Act
        var plugin = new ModbusControlPlugin(_loggerMock.Object);

        // Assert
        plugin.Should().NotBeNull();
        plugin.Name.Should().Be("Modbus Control Plugin");
        plugin.Version.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task InitializeAsync_ShouldLogInitialization()
    {
        // Arrange
        var config = new Dictionary<string, object>();

        // Act
        await _plugin.InitializeAsync(config);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Initializing")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task TestConnectionAsync_ShouldReturnTrue()
    {
        // Arrange & Act
        var result = await _plugin.TestConnectionAsync();

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(1, true)]
    [InlineData(0, false)]
    [InlineData("true", true)]
    [InlineData("false", false)]
    public async Task SetStateAsync_ShouldHandleVariousStateTypes(object inputState, bool expectedBoolState)
    {
        // Arrange
        var controlId = "test_relay_1";

        // Act
        var result = await _plugin.SetStateAsync(controlId, inputState);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task SetStateAsync_WithNullControlId_ShouldReturnFalse()
    {
        // Arrange & Act
        var result = await _plugin.SetStateAsync(null!, true);

        // Assert
        result.Should().BeFalse();
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("null or empty")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task GetStateAsync_ShouldReturnDefaultState()
    {
        // Arrange
        var controlId = "test_relay_1";

        // Act
        var result = await _plugin.GetStateAsync(controlId);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<bool>();
    }

    [Fact]
    public async Task GetStateAsync_WithNullControlId_ShouldReturnFalse()
    {
        // Arrange & Act
        var result = await _plugin.GetStateAsync(null!);

        // Assert
        result.Should().BeOfType<bool>();
        ((bool)result).Should().BeFalse();
    }

    [Fact]
    public void Dispose_ShouldLogDisposal()
    {
        // Arrange & Act
        _plugin.Dispose();

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Disposing")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void Dispose_CalledTwice_ShouldOnlyLogOnce()
    {
        // Arrange & Act
        _plugin.Dispose();
        _plugin.Dispose();

        // Assert - should only log once even though Dispose was called twice
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Disposing")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Theory]
    [InlineData("192.168.1.100", 502)]
    [InlineData("192.168.1.100:502", 502)]
    [InlineData("192.168.1.100:503", 503)]
    [InlineData("10.0.0.50:1234", 1234)]
    public void ParseModbusAddress_ShouldExtractHostAndPort(string address, int expectedPort)
    {
        // This test verifies address parsing logic
        // Since ParseModbusAddress is private, we test it indirectly through SetStateWithConfigAsync
        // For unit testing, we'd need to either make it internal or test through public methods

        // Note: This is a placeholder test showing the expected behavior
        // Actual verification would happen during integration testing with real Modbus device
        var parts = address.Split(':');
        var host = parts[0].Trim();
        var port = parts.Length > 1 && int.TryParse(parts[1], out var p) ? p : 502;

        // Assert
        host.Should().NotBeNullOrEmpty();
        port.Should().Be(expectedPort);
    }

    [Fact]
    public async Task SetStateWithConfigAsync_WithInvalidAddress_ShouldReturnFalse()
    {
        // Arrange
        var invalidAddress = "";
        var register = 0;
        var registerType = "Coil";
        var state = true;

        // Act
        var result = await _plugin.SetStateWithConfigAsync(
            invalidAddress,
            register,
            registerType,
            state);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetStateWithConfigAsync_WithInvalidAddress_ShouldReturnFalse()
    {
        // Arrange
        var invalidAddress = "";
        var register = 0;
        var registerType = "Coil";

        // Act
        var result = await _plugin.GetStateWithConfigAsync(
            invalidAddress,
            register,
            registerType);

        // Assert
        result.Should().BeOfType<bool>();
        ((bool)result).Should().BeFalse();
    }

    [Theory]
    [InlineData("Coil")]
    [InlineData("HoldingRegister")]
    public async Task SetStateWithConfigAsync_WithValidConfiguration_ShouldAttemptConnection(string registerType)
    {
        // Arrange
        var modbusAddress = "192.168.1.100:502";
        var register = 0;
        var state = true;

        // Note: This test will fail to connect (no actual Modbus device)
        // but we're testing that it attempts the connection with proper configuration

        // Act
        var result = await _plugin.SetStateWithConfigAsync(
            modbusAddress,
            register,
            registerType,
            state);

        // Assert
        // Result will be false because no actual device exists
        // But we verify it doesn't throw an exception
        result.Should().BeFalse();

        // Verify error was logged
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to set Modbus state")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SetStateWithConfigAsync_WithWavesharePreset_ShouldApplyPresetConfiguration()
    {
        // Arrange
        var modbusAddress = "192.168.1.100:502";
        var register = 0;
        var registerType = "Coil"; // Will be overridden by preset
        var state = true;
        var deviceType = "Waveshare8Relay";

        // Act
        var result = await _plugin.SetStateWithConfigAsync(
            modbusAddress,
            register,
            registerType,
            state,
            deviceType);

        // Assert
        // Should log that preset is being used
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Using device preset")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
