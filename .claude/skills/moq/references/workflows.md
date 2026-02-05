# Moq Workflows Reference

## Contents
- Test Setup Workflow
- Controller Testing Workflow
- Service Testing Workflow
- Plugin Testing Workflow
- SignalR Hub Testing

## Test Setup Workflow

### Standard Test Class Structure

```csharp
public class TankServiceTests : IDisposable
{
    private readonly Mock<ILogger<TankService>> _mockLogger;
    private readonly Mock<IJsonFileStore> _mockFileStore;
    private readonly TankService _sut;

    public TankServiceTests()
    {
        _mockLogger = new Mock<ILogger<TankService>>();
        _mockFileStore = new Mock<IJsonFileStore>();
        _sut = new TankService(_mockLogger.Object, _mockFileStore.Object);
    }

    public void Dispose()
    {
        // Cleanup if needed
    }
}
```

Copy this checklist:
- [ ] Create mocks for all dependencies
- [ ] Initialize SUT (System Under Test) with mock objects
- [ ] Implement IDisposable if cleanup needed
- [ ] Use `_sut` naming convention for test subject

## Controller Testing Workflow

### Testing API Controllers

```csharp
[Fact]
public async Task GetAllTanks_ReturnsOkResult_WithTanks()
{
    // Arrange
    var tanks = new List<Tank>
    {
        new Tank { Id = Guid.NewGuid(), Name = "Fresh Water", IsActive = true }
    };
    
    var mockService = new Mock<ITankService>();
    mockService
        .Setup(x => x.GetAllTanksAsync(It.IsAny<CancellationToken>()))
        .ReturnsAsync(tanks);
    
    var controller = new TanksController(mockService.Object);

    // Act
    var result = await controller.GetAll();

    // Assert
    result.Result.Should().BeOfType<OkObjectResult>();
    var okResult = result.Result as OkObjectResult;
    okResult.Value.Should().BeEquivalentTo(tanks);
}
```

### Testing Error Scenarios

```csharp
[Fact]
public async Task GetTank_ReturnsNotFound_WhenTankDoesNotExist()
{
    // Arrange
    var mockService = new Mock<ITankService>();
    mockService
        .Setup(x => x.GetTankByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync((Tank?)null);
    
    var controller = new TanksController(mockService.Object);

    // Act
    var result = await controller.GetById(Guid.NewGuid());

    // Assert
    result.Result.Should().BeOfType<NotFoundResult>();
}
```

## Service Testing Workflow

### Testing Business Logic

```csharp
[Fact]
public async Task CheckTankAlerts_GeneratesAlert_WhenLevelBelowThreshold()
{
    // Arrange
    var tank = new Tank
    {
        Id = Guid.NewGuid(),
        Name = "Fresh Water",
        CurrentLevel = 5.0,
        AlertLevel = 10.0,
        AlertWhenOver = false // Alert when BELOW threshold
    };

    var mockAlertService = new Mock<IAlertService>();
    mockAlertService
        .Setup(x => x.CreateAlertAsync(It.IsAny<Alert>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync((Alert a, CancellationToken _) => a);

    var sut = new TankMonitorService(mockAlertService.Object);

    // Act
    await sut.CheckTankAlerts(tank);

    // Assert
    mockAlertService.Verify(
        x => x.CreateAlertAsync(
            It.Is<Alert>(a => a.Severity == AlertSeverity.Warning),
            It.IsAny<CancellationToken>()),
        Times.Once);
}
```

Workflow:
1. Arrange: Create entity with test state
2. Setup: Configure mock to capture or verify
3. Act: Call method under test
4. Assert: Verify mock interactions

## Plugin Testing Workflow

### Testing Plugin Implementations

```csharp
[Fact]
public async Task SimulatedSensorPlugin_ReturnsValueInRange()
{
    // Arrange
    var mockLogger = new Mock<ILogger<SimulatedSensorPlugin>>();
    var plugin = new SimulatedSensorPlugin(mockLogger.Object);
    await plugin.InitializeAsync(new Dictionary<string, object>());

    // Act
    var value = await plugin.ReadValueAsync("tank-1");

    // Assert
    value.Should().BeInRange(0.0, 100.0);
}
```

### Testing Plugin Error Handling

```csharp
[Fact]
public async Task ModbusPlugin_ThrowsOnConnectionFailure()
{
    // Arrange
    var mockLogger = new Mock<ILogger<ModbusControlPlugin>>();
    var plugin = new ModbusControlPlugin(mockLogger.Object);
    
    var config = new Dictionary<string, object>
    {
        ["IpAddress"] = "192.168.1.999", // Invalid
        ["Port"] = 502
    };

    // Act & Assert
    await Assert.ThrowsAsync<InvalidOperationException>(
        () => plugin.InitializeAsync(config));
}
```

## SignalR Hub Testing

### Mocking Hub Context for Broadcasting

```csharp
[Fact]
public async Task TelemetryService_BroadcastsTankUpdate()
{
    // Arrange
    var mockHubContext = new Mock<IHubContext<TelemetryHub>>();
    var mockClients = new Mock<IHubClients>();
    var mockGroup = new Mock<IClientProxy>();

    mockHubContext.Setup(x => x.Clients).Returns(mockClients.Object);
    mockClients.Setup(x => x.Group("tanks")).Returns(mockGroup.Object);
    
    mockGroup
        .Setup(x => x.SendCoreAsync(
            It.IsAny<string>(),
            It.IsAny<object[]>(),
            It.IsAny<CancellationToken>()))
        .Returns(Task.CompletedTask);

    var service = new TelemetryService(mockHubContext.Object);

    // Act
    await service.BroadcastTankLevel(Guid.NewGuid(), 75.0, "Fresh Water");

    // Assert
    mockGroup.Verify(
        x => x.SendCoreAsync(
            "TankLevelUpdated",
            It.Is<object[]>(args => args.Length == 3),
            It.IsAny<CancellationToken>()),
        Times.Once);
}
```

### WARNING: Forgetting to Setup Clients Chain

**The Problem:**

```csharp
// BAD - NullReferenceException at runtime
var mockHubContext = new Mock<IHubContext<TelemetryHub>>();
// Missing: mockHubContext.Setup(x => x.Clients)...

var service = new TelemetryService(mockHubContext.Object);
await service.Broadcast(); // Throws NullReferenceException
```

**The Fix:**

```csharp
// GOOD - Complete mock chain
var mockHubContext = new Mock<IHubContext<TelemetryHub>>();
var mockClients = new Mock<IHubClients>();
var mockGroup = new Mock<IClientProxy>();

mockHubContext.Setup(x => x.Clients).Returns(mockClients.Object);
mockClients.Setup(x => x.Group(It.IsAny<string>())).Returns(mockGroup.Object);
```

## Test Data Builders

### Factory Pattern for Test Data

```csharp
public static class TestDataFactory
{
    public static Tank CreateTank(
        string name = "Test Tank",
        TankType type = TankType.FreshWater,
        double currentLevel = 50.0)
    {
        return new Tank
        {
            Id = Guid.NewGuid(),
            Name = name,
            Type = type,
            CurrentLevel = currentLevel,
            Capacity = 100.0,
            AlertLevel = 10.0,
            AlertWhenOver = false,
            IsActive = true,
            LastUpdated = DateTime.UtcNow
        };
    }
}

// Usage in tests
var tank = TestDataFactory.CreateTank(currentLevel: 5.0);
```

Copy this checklist for new test classes:
- [ ] Create mock for each dependency interface
- [ ] Use `Mock<IInterface>()`, never mock concrete classes
- [ ] Setup all methods that will be called
- [ ] Use `It.IsAny<CancellationToken>()` for cancellation tokens
- [ ] Verify important interactions with `Times` constraint
- [ ] Use FluentAssertions for readable assertions