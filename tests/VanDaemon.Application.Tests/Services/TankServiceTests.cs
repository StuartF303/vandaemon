using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using VanDaemon.Application.Persistence;
using VanDaemon.Application.Services;
using VanDaemon.Plugins.Abstractions;
using Xunit;

namespace VanDaemon.Application.Tests.Services;

public class TankServiceTests
{
    private readonly Mock<ILogger<TankService>> _loggerMock;
    private readonly Mock<ILogger<JsonFileStore>> _fileStoreLoggerMock;
    private readonly Mock<ISensorPlugin> _sensorPluginMock;
    private readonly JsonFileStore _fileStore;
    private readonly TankService _tankService;

    public TankServiceTests()
    {
        _loggerMock = new Mock<ILogger<TankService>>();
        _fileStoreLoggerMock = new Mock<ILogger<JsonFileStore>>();
        _sensorPluginMock = new Mock<ISensorPlugin>();

        _sensorPluginMock.Setup(x => x.Name).Returns("Simulated Sensor Plugin");
        _sensorPluginMock.Setup(x => x.Version).Returns("1.0.0");

        // Create a temporary directory for test file storage
        var tempPath = Path.Combine(Path.GetTempPath(), $"vandaemon-tests-{Guid.NewGuid()}");
        _fileStore = new JsonFileStore(_fileStoreLoggerMock.Object, tempPath);

        _tankService = new TankService(_loggerMock.Object, _fileStore, new[] { _sensorPluginMock.Object });
    }

    [Fact]
    public async Task GetAllTanksAsync_ShouldReturnAllActiveTanks()
    {
        // Act
        var result = await _tankService.GetAllTanksAsync();

        // Assert
        result.Should().NotBeEmpty();
        result.All(t => t.IsActive).Should().BeTrue();
    }

    [Fact]
    public async Task GetTankByIdAsync_WithValidId_ShouldReturnTank()
    {
        // Arrange
        var tanks = await _tankService.GetAllTanksAsync();
        var firstTank = tanks.First();

        // Act
        var result = await _tankService.GetTankByIdAsync(firstTank.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(firstTank.Id);
    }

    [Fact]
    public async Task GetTankByIdAsync_WithInvalidId_ShouldReturnNull()
    {
        // Arrange
        var invalidId = Guid.NewGuid();

        // Act
        var result = await _tankService.GetTankByIdAsync(invalidId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetTankLevelAsync_ShouldReadFromSensorPlugin()
    {
        // Arrange
        var tanks = await _tankService.GetAllTanksAsync();
        var tank = tanks.First();
        var expectedLevel = 75.5;

        _sensorPluginMock
            .Setup(x => x.ReadValueAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedLevel);

        // Act
        var result = await _tankService.GetTankLevelAsync(tank.Id);

        // Assert
        result.Should().Be(expectedLevel);
        _sensorPluginMock.Verify(x => x.ReadValueAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateTankAsync_ShouldCreateNewTank()
    {
        // Arrange
        var newTank = new VanDaemon.Core.Entities.Tank
        {
            Name = "Test Tank",
            Type = VanDaemon.Core.Enums.TankType.FreshWater,
            Capacity = 50,
            CurrentLevel = 0,
            SensorPlugin = "Simulated Sensor Plugin"
        };

        // Act
        var result = await _tankService.CreateTankAsync(newTank);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().NotBe(Guid.Empty);
        result.Name.Should().Be("Test Tank");

        var allTanks = await _tankService.GetAllTanksAsync();
        allTanks.Should().Contain(t => t.Id == result.Id);
    }

    [Fact]
    public async Task UpdateTankAsync_ShouldUpdateExistingTank()
    {
        // Arrange
        var tanks = await _tankService.GetAllTanksAsync();
        var tank = tanks.First();
        var originalName = tank.Name;
        tank.Name = "Updated Name";

        // Act
        var result = await _tankService.UpdateTankAsync(tank);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("Updated Name");
        result.Name.Should().NotBe(originalName);
    }

    [Fact]
    public async Task DeleteTankAsync_ShouldMarkTankAsInactive()
    {
        // Arrange
        var tanks = await _tankService.GetAllTanksAsync();
        var tankToDelete = tanks.First();

        // Act
        await _tankService.DeleteTankAsync(tankToDelete.Id);

        // Assert
        var activeTanks = await _tankService.GetAllTanksAsync();
        activeTanks.Should().NotContain(t => t.Id == tankToDelete.Id);
    }

    [Fact]
    public async Task RefreshAllTankLevelsAsync_ShouldUpdateAllTankLevels()
    {
        // Arrange
        _sensorPluginMock
            .Setup(x => x.ReadValueAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(50.0);

        // Act
        await _tankService.RefreshAllTankLevelsAsync();

        // Assert
        var tanks = await _tankService.GetAllTanksAsync();
        _sensorPluginMock.Verify(
            x => x.ReadValueAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Exactly(tanks.Count()));
    }
}
