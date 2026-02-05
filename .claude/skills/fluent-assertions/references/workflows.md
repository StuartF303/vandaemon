# FluentAssertions Workflows Reference

## Contents
- Test Structure Workflow
- Service Testing Workflow
- JsonFileStore Testing Workflow
- Plugin Testing Workflow
- Integration Testing Workflow
- Debugging Failed Assertions

## Test Structure Workflow

### Arrange-Act-Assert with FluentAssertions

```csharp
[Fact]
public async Task GetAllTanksAsync_ReturnsActiveTanksOnly()
{
    // Arrange - Setup mocks and test data
    var mockFileStore = new Mock<IJsonFileStore>();
    var testTanks = new List<Tank>
    {
        new Tank { Id = Guid.NewGuid(), Name = "Active", IsActive = true },
        new Tank { Id = Guid.NewGuid(), Name = "Inactive", IsActive = false }
    };
    mockFileStore.Setup(x => x.LoadAsync<List<Tank>>("tanks.json", It.IsAny<CancellationToken>()))
        .ReturnsAsync(testTanks);

    var service = new TankService(_loggerMock.Object, mockFileStore.Object);

    // Act - Execute the method under test
    var result = await service.GetAllTanksAsync();

    // Assert - Verify with FluentAssertions
    result.Should().NotBeNull();
    result.Should().ContainSingle();
    result.First().Name.Should().Be("Active");
}
```

## Service Testing Workflow

### Testing VanDaemon Services

Copy this checklist and track progress:
- [ ] Create mock for `IJsonFileStore` using **Moq**
- [ ] Setup mock return data
- [ ] Instantiate service with mocked dependencies
- [ ] Call service method
- [ ] Assert result with FluentAssertions
- [ ] Verify mock interactions if needed

```csharp
[Fact]
public async Task SetControlStateAsync_UpdatesStateAndPersists()
{
    // Arrange
    var controlId = Guid.NewGuid();
    var controls = new List<Control>
    {
        new Control { Id = controlId, Name = "Light", Type = ControlType.Toggle, State = false }
    };

    _mockFileStore.Setup(x => x.LoadAsync<List<Control>>("controls.json", It.IsAny<CancellationToken>()))
        .ReturnsAsync(controls);
    _mockFileStore.Setup(x => x.SaveAsync("controls.json", It.IsAny<List<Control>>(), It.IsAny<CancellationToken>()))
        .Returns(Task.CompletedTask);

    var service = new ControlService(_loggerMock.Object, _mockFileStore.Object);

    // Act
    var result = await service.SetControlStateAsync(controlId, true);

    // Assert
    result.Should().BeTrue("state change should succeed");
    controls.First().State.Should().Be(true);
    _mockFileStore.Verify(x => x.SaveAsync("controls.json", It.IsAny<List<Control>>(), It.IsAny<CancellationToken>()), Times.Once);
}
```

## JsonFileStore Testing Workflow

### Testing with Temporary Directories

```csharp
public class JsonFileStoreTests : IDisposable
{
    private readonly string _tempPath;
    private readonly JsonFileStore _fileStore;

    public JsonFileStoreTests()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), $"vandaemon-tests-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempPath);
        _fileStore = new JsonFileStore(_loggerMock.Object, _tempPath);
    }

    [Fact]
    public async Task SaveAndLoad_RoundTripsData()
    {
        // Arrange
        var tanks = new List<Tank>
        {
            new Tank { Id = Guid.NewGuid(), Name = "Test Tank", Type = TankType.FreshWater }
        };

        // Act
        await _fileStore.SaveAsync("tanks.json", tanks);
        var loaded = await _fileStore.LoadAsync<List<Tank>>("tanks.json");

        // Assert
        loaded.Should().BeEquivalentTo(tanks);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempPath))
            Directory.Delete(_tempPath, recursive: true);
    }
}
```

### Validation Loop

1. Write test with assertions
2. Run: `dotnet test --filter "FullyQualifiedName~JsonFileStoreTests"`
3. If assertion fails, check failure message for details
4. Fix implementation and repeat step 2
5. Only proceed when all assertions pass

## Plugin Testing Workflow

### Testing Simulated Plugins

```csharp
[Fact]
public async Task SimulatedSensorPlugin_ReturnsRealisticValues()
{
    // Arrange
    var plugin = new SimulatedSensorPlugin(_loggerMock.Object);
    await plugin.InitializeAsync(new Dictionary<string, object>());

    // Act
    var value = await plugin.ReadValueAsync("tank-fresh-water");

    // Assert - Simulated values should be realistic
    value.Should().BeInRange(0, 100, "tank levels are percentages");
}

[Fact]
public async Task SimulatedControlPlugin_PersistsState()
{
    // Arrange
    var plugin = new SimulatedControlPlugin(_loggerMock.Object);
    await plugin.InitializeAsync(new Dictionary<string, object>());

    // Act
    await plugin.SetStateAsync("light-main", true);
    var state = await plugin.GetStateAsync("light-main");

    // Assert
    state.Should().Be(true);
}
```

## Integration Testing Workflow

### Controller Integration Tests

```csharp
[Fact]
public async Task TanksController_GetAll_ReturnsOkWithTanks()
{
    // Arrange
    var mockService = new Mock<ITankService>();
    mockService.Setup(x => x.GetAllTanksAsync(It.IsAny<CancellationToken>()))
        .ReturnsAsync(new List<Tank> { new Tank { Name = "Test" } });

    var controller = new TanksController(mockService.Object);

    // Act
    var result = await controller.GetAll();

    // Assert
    var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
    var tanks = okResult.Value.Should().BeAssignableTo<IEnumerable<Tank>>().Subject;
    tanks.Should().ContainSingle();
    tanks.First().Name.Should().Be("Test");
}
```

## Debugging Failed Assertions

### Understanding Failure Messages

FluentAssertions provides detailed failure messages:

```
Expected result to contain 3 item(s), but found 2: {Tank1, Tank2}.
```

### Adding Context with Because

```csharp
// GOOD - Add context for complex scenarios
tanks.Should().HaveCount(expectedCount,
    "the test data file contains {0} active tanks and filtering removed {1}",
    totalTanks, inactiveCount);

// GOOD - Document business rules
alert.Severity.Should().Be(AlertSeverity.Critical,
    "LPG tank below 5% triggers critical alert per safety requirements");
```

### Scoped Assertions for Complex Objects

```csharp
// GOOD - Use Using() for focused sub-assertions
tank.Should().BeEquivalentTo(expected, options => options
    .Using<DateTime>(ctx => ctx.Subject.Should().BeCloseTo(ctx.Expectation, TimeSpan.FromSeconds(1)))
    .WhenTypeIs<DateTime>());
```

### Chaining for Readability

```csharp
// GOOD - Chain related assertions
result.Should()
    .NotBeNull()
    .And.HaveCount(3)
    .And.OnlyContain(t => t.IsActive);