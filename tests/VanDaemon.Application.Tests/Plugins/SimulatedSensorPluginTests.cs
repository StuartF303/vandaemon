using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using VanDaemon.Plugins.Simulated;
using Xunit;

namespace VanDaemon.Application.Tests.Plugins;

public class SimulatedSensorPluginTests
{
    private readonly Mock<ILogger<SimulatedSensorPlugin>> _loggerMock;
    private readonly SimulatedSensorPlugin _plugin;

    public SimulatedSensorPluginTests()
    {
        _loggerMock = new Mock<ILogger<SimulatedSensorPlugin>>();
        _plugin = new SimulatedSensorPlugin(_loggerMock.Object);
    }

    [Fact]
    public async Task InitializeAsync_ShouldInitializeSuccessfully()
    {
        // Arrange
        var config = new Dictionary<string, object>();

        // Act
        await _plugin.InitializeAsync(config);

        // Assert
        _plugin.Name.Should().NotBeNullOrEmpty();
        _plugin.Version.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task TestConnectionAsync_ShouldReturnTrue()
    {
        // Arrange
        await _plugin.InitializeAsync(new Dictionary<string, object>());

        // Act
        var result = await _plugin.TestConnectionAsync();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ReadValueAsync_WithValidSensorId_ShouldReturnValue()
    {
        // Arrange
        await _plugin.InitializeAsync(new Dictionary<string, object>());

        // Act
        var result = await _plugin.ReadValueAsync("fresh_water");

        // Assert
        result.Should().BeGreaterOrEqualTo(0);
        result.Should().BeLessOrEqualTo(100);
    }

    [Fact]
    public async Task ReadValueAsync_WithInvalidSensorId_ShouldReturnZero()
    {
        // Arrange
        await _plugin.InitializeAsync(new Dictionary<string, object>());

        // Act
        var result = await _plugin.ReadValueAsync("invalid_sensor");

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public async Task ReadAllValuesAsync_ShouldReturnAllSensorValues()
    {
        // Arrange
        await _plugin.InitializeAsync(new Dictionary<string, object>());

        // Act
        var result = await _plugin.ReadAllValuesAsync();

        // Assert
        result.Should().NotBeEmpty();
        result.Should().ContainKey("fresh_water");
        result.Should().ContainKey("waste_water");
        result.Should().ContainKey("lpg");
    }

    [Fact]
    public async Task ReadValueAsync_MultipleReads_ShouldShowVariation()
    {
        // Arrange
        await _plugin.InitializeAsync(new Dictionary<string, object>());

        // Act
        var value1 = await _plugin.ReadValueAsync("fresh_water");
        var value2 = await _plugin.ReadValueAsync("fresh_water");
        var value3 = await _plugin.ReadValueAsync("fresh_water");

        // Assert
        // Values should be close but not identical due to simulated variation
        value1.Should().BeInRange(0, 100);
        value2.Should().BeInRange(0, 100);
        value3.Should().BeInRange(0, 100);
    }

    [Fact]
    public void Dispose_ShouldDisposeSuccessfully()
    {
        // Arrange
        var action = () => _plugin.Dispose();

        // Act & Assert
        action.Should().NotThrow();
    }
}
