# xUnit Workflows Reference

## Contents
- TDD Workflow
- Test Organization
- Running Specific Tests
- Integration Test Setup
- Debugging Failed Tests

## TDD Workflow

### Adding a New Feature with Tests

Copy this checklist and track progress:
- [ ] Write failing test for expected behavior
- [ ] Run test to confirm it fails
- [ ] Implement minimum code to pass
- [ ] Run test to confirm it passes
- [ ] Refactor if needed
- [ ] Run all related tests to prevent regression

```bash
# 1. Create test first
dotnet test --filter "MethodName_Scenario_ExpectedResult"  # Should fail

# 2. Implement feature
# Edit service/controller code

# 3. Verify test passes
dotnet test --filter "MethodName_Scenario_ExpectedResult"  # Should pass

# 4. Run full test suite
dotnet test VanDaemon.sln
```

## Test Organization

### Project Structure

```
tests/
├── VanDaemon.Api.Tests/
│   ├── Controllers/
│   │   ├── TanksControllerTests.cs
│   │   └── ControlsControllerTests.cs
│   └── Hubs/
│       └── TelemetryHubTests.cs
├── VanDaemon.Application.Tests/
│   ├── Services/
│   │   ├── TankServiceTests.cs
│   │   ├── ControlServiceTests.cs
│   │   └── AlertServiceTests.cs
│   └── Persistence/
│       └── JsonFileStoreTests.cs
└── VanDaemon.Plugins.Modbus.Tests/
    └── ModbusPluginTests.cs
```

### Test Class Template

```csharp
namespace VanDaemon.Application.Tests.Services;

public class TankServiceTests : IDisposable
{
    private readonly Mock<ILogger<TankService>> _loggerMock = new();
    private readonly Mock<ISensorPlugin> _sensorMock = new();
    private readonly TankService _sut;
    private readonly string _tempPath;
    
    public TankServiceTests()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), $"tests-{Guid.NewGuid()}");
        _sut = new TankService(_loggerMock.Object, _sensorMock.Object);
    }
    
    public void Dispose()
    {
        if (Directory.Exists(_tempPath))
            Directory.Delete(_tempPath, recursive: true);
    }
    
    [Fact]
    public async Task GetAllTanksAsync_ReturnsActiveTanks()
    {
        // Test implementation
    }
}
```

## Running Specific Tests

```bash
# By class name
dotnet test --filter "FullyQualifiedName~TankServiceTests"

# By method name
dotnet test --filter "DisplayName~GetAllTanksAsync"

# By trait/category
dotnet test --filter "Category=Integration"

# Multiple filters
dotnet test --filter "FullyQualifiedName~Service & Category!=Slow"

# With detailed output
dotnet test --verbosity normal --logger "console;verbosity=detailed"
```

## Integration Test Setup

### Testing with Real JsonFileStore

```csharp
public class IntegrationTests : IAsyncLifetime
{
    private string _dataPath = null!;
    private JsonFileStore _fileStore = null!;
    
    public async Task InitializeAsync()
    {
        _dataPath = Path.Combine(Path.GetTempPath(), $"integration-{Guid.NewGuid()}");
        Directory.CreateDirectory(_dataPath);
        _fileStore = new JsonFileStore(Mock.Of<ILogger<JsonFileStore>>(), _dataPath);
        
        // Seed test data
        await _fileStore.SaveAsync("tanks", CreateTestTanks());
    }
    
    public Task DisposeAsync()
    {
        if (Directory.Exists(_dataPath))
            Directory.Delete(_dataPath, recursive: true);
        return Task.CompletedTask;
    }
    
    [Fact]
    public async Task FullWorkflow_CreateUpdateDelete_Tank()
    {
        // Integration test using real file storage
    }
}
```

### Controller Integration Tests

```csharp
public class TanksControllerIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    
    public TanksControllerIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }
    
    [Fact]
    public async Task GetTanks_ReturnsSuccessAndCorrectContentType()
    {
        var response = await _client.GetAsync("/api/tanks");
        
        response.EnsureSuccessStatusCode();
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
    }
}
```

## Debugging Failed Tests

### Iterate-Until-Pass Pattern

1. Run failing test with verbose output
2. Read error message and stack trace
3. Add diagnostic logging if needed
4. Fix the issue
5. Repeat until all tests pass

```bash
# Step 1: Run with full output
dotnet test --filter "FailingTest" --verbosity detailed

# Step 2: If assertion unclear, add temporary logging
[Fact]
public async Task DebugTest()
{
    var result = await _service.GetDataAsync();
    _output.WriteLine($"Result count: {result.Count}");  // ITestOutputHelper
    _output.WriteLine($"First item: {JsonSerializer.Serialize(result.First())}");
    result.Should().HaveCount(3);
}

# Step 3: Fix and re-run
dotnet test --filter "FailingTest"

# Step 4: Run related tests
dotnet test --filter "FullyQualifiedName~TankService"

# Step 5: Full suite
dotnet test VanDaemon.sln
```

### Using ITestOutputHelper

```csharp
public class DiagnosticTests
{
    private readonly ITestOutputHelper _output;
    
    public DiagnosticTests(ITestOutputHelper output)
    {
        _output = output;
    }
    
    [Fact]
    public async Task DiagnoseFailure()
    {
        _output.WriteLine("Starting test...");
        var result = await _service.DoSomethingAsync();
        _output.WriteLine($"Got result: {result}");
    }
}