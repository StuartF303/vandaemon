# xUnit Patterns Reference

## Contents
- Test Naming Convention
- Mocking Dependencies
- Testing Exceptions
- Testing SignalR Hubs
- Common Anti-Patterns

## Test Naming Convention

**Pattern:** `MethodName_Condition_ExpectedResult`

```csharp
// GOOD - Clear intent
[Fact]
public async Task GetTankLevelAsync_WhenSensorFails_ReturnsLastKnownValue()

[Fact]
public async Task SetControlStateAsync_WithInvalidId_ThrowsNotFoundException()

// BAD - Unclear what's being tested
[Fact]
public async Task TestTankLevel()

[Fact]
public async Task Test1()
```

## Mocking Dependencies

### Service with Multiple Dependencies

```csharp
public class ControlServiceTests
{
    private readonly Mock<ILogger<ControlService>> _loggerMock = new();
    private readonly Mock<IControlPlugin> _pluginMock = new();
    private readonly Mock<IHubContext<TelemetryHub>> _hubContextMock = new();
    private readonly ControlService _sut;
    
    public ControlServiceTests()
    {
        // Setup hub context mock (common pattern for SignalR)
        var clientsMock = new Mock<IHubClients>();
        var groupMock = new Mock<IClientProxy>();
        clientsMock.Setup(c => c.Group(It.IsAny<string>())).Returns(groupMock.Object);
        _hubContextMock.Setup(h => h.Clients).Returns(clientsMock.Object);
        
        _sut = new ControlService(_loggerMock.Object, _pluginMock.Object, _hubContextMock.Object);
    }
}
```

### Verifying Calls Were Made

```csharp
[Fact]
public async Task SetStateAsync_BroadcastsToSignalR()
{
    await _sut.SetControlStateAsync(controlId, newState);
    
    // Verify SignalR broadcast happened
    _hubContextMock.Verify(h => 
        h.Clients.Group("controls").SendCoreAsync(
            "ControlStateChanged",
            It.IsAny<object[]>(),
            It.IsAny<CancellationToken>()),
        Times.Once);
}
```

## Testing Exceptions

```csharp
[Fact]
public async Task GetTankAsync_WithInvalidId_ThrowsKeyNotFoundException()
{
    var invalidId = Guid.NewGuid();
    
    Func<Task> act = async () => await _service.GetTankAsync(invalidId);
    
    await act.Should().ThrowAsync<KeyNotFoundException>()
        .WithMessage($"*{invalidId}*");
}

[Fact]
public async Task SetStateAsync_WhenPluginFails_LogsErrorAndRethrows()
{
    _pluginMock.Setup(p => p.SetStateAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
        .ThrowsAsync(new InvalidOperationException("Connection lost"));
    
    Func<Task> act = async () => await _sut.SetControlStateAsync(controlId, true);
    
    await act.Should().ThrowAsync<InvalidOperationException>();
    _loggerMock.Verify(
        x => x.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), 
            It.IsAny<Exception>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
        Times.Once);
}
```

## Testing SignalR Hubs

```csharp
public class TelemetryHubTests
{
    [Fact]
    public async Task SubscribeToTanks_AddsClientToGroup()
    {
        var groupsMock = new Mock<IGroupManager>();
        var contextMock = new Mock<HubCallerContext>();
        contextMock.Setup(c => c.ConnectionId).Returns("test-connection");
        
        var hub = new TelemetryHub { Groups = groupsMock.Object, Context = contextMock.Object };
        
        await hub.SubscribeToTanks();
        
        groupsMock.Verify(g => g.AddToGroupAsync("test-connection", "tanks", default), Times.Once);
    }
}
```

## WARNING: Common Anti-Patterns

### Anti-Pattern: Testing Implementation Details

```csharp
// BAD - Tests private method behavior
[Fact]
public void CalculateAlertThreshold_InternalMethod_ReturnsCorrectValue()
{
    var method = typeof(AlertService).GetMethod("CalculateThreshold", BindingFlags.NonPublic);
    // Don't do this
}

// GOOD - Test through public interface
[Fact]
public async Task CheckAlertsAsync_WhenBelowThreshold_CreatesWarningAlert()
{
    var result = await _service.CheckTankAlertsAsync();
    result.Should().ContainSingle(a => a.Severity == AlertSeverity.Warning);
}
```

### Anti-Pattern: Not Cleaning Up Temp Files

```csharp
// BAD - Leaves test artifacts
[Fact]
public async Task SaveAsync_CreatesFile()
{
    var store = new JsonFileStore(logger, "C:/temp/tests");
    await store.SaveAsync("data", testData);
    // File stays forever
}

// GOOD - Always cleanup
[Fact]
public async Task SaveAsync_CreatesFile()
{
    var tempPath = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid()}");
    try
    {
        var store = new JsonFileStore(logger, tempPath);
        await store.SaveAsync("data", testData);
        File.Exists(Path.Combine(tempPath, "data.json")).Should().BeTrue();
    }
    finally
    {
        if (Directory.Exists(tempPath))
            Directory.Delete(tempPath, recursive: true);
    }
}
```

### Anti-Pattern: Shared Mutable State Between Tests

```csharp
// BAD - Tests affect each other
public class TankServiceTests
{
    private static List<Tank> _sharedTanks = new(); // NEVER do this
    
// GOOD - Fresh state per test
public class TankServiceTests
{
    private List<Tank> CreateTestTanks() => new()
    {
        new Tank { Id = Guid.NewGuid(), Name = "Fresh Water", IsActive = true }
    };
}