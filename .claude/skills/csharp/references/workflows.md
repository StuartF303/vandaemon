# C# Workflows Reference

## Contents
- Adding a New Service
- Adding a New Entity
- Creating a Plugin
- Background Service Pattern
- Testing Workflows

## Adding a New Service

Copy this checklist and track progress:
- [ ] Create interface in `Application/Interfaces/I{Name}Service.cs`
- [ ] Create implementation in `Application/Services/{Name}Service.cs`
- [ ] Register in `Program.cs` as singleton
- [ ] Add unit tests in `tests/VanDaemon.Application.Tests/`

### Step 1: Interface

```csharp
// src/Backend/VanDaemon.Application/Interfaces/IMaintenanceService.cs
public interface IMaintenanceService
{
    Task<IReadOnlyList<MaintenanceRecord>> GetAllAsync(CancellationToken ct = default);
    Task<MaintenanceRecord?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<MaintenanceRecord> CreateAsync(MaintenanceRecord record, CancellationToken ct = default);
    Task<MaintenanceRecord> UpdateAsync(MaintenanceRecord record, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
```

### Step 2: Implementation

```csharp
// src/Backend/VanDaemon.Application/Services/MaintenanceService.cs
public class MaintenanceService : IMaintenanceService
{
    private readonly ILogger<MaintenanceService> _logger;
    private readonly JsonFileStore _fileStore;
    private List<MaintenanceRecord> _records = new();

    public MaintenanceService(ILogger<MaintenanceService> logger, JsonFileStore fileStore)
    {
        _logger = logger;
        _fileStore = fileStore;
    }

    public async Task<IReadOnlyList<MaintenanceRecord>> GetAllAsync(CancellationToken ct = default)
    {
        if (_records.Count == 0)
        {
            _records = await _fileStore.LoadAsync<List<MaintenanceRecord>>("maintenance.json", ct)
                ?? new List<MaintenanceRecord>();
        }
        return _records.Where(r => r.IsActive).ToList().AsReadOnly();
    }
}
```

### Step 3: Registration

```csharp
// Program.cs
builder.Services.AddSingleton<IMaintenanceService, MaintenanceService>();
```

## Adding a New Entity

### Step 1: Create Entity

```csharp
// src/Backend/VanDaemon.Core/Entities/MaintenanceRecord.cs
public class MaintenanceRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Description { get; set; } = string.Empty;
    public DateTime DueDate { get; set; }
    public MaintenanceType Type { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
}
```

### Step 2: Create Enum (if needed)

```csharp
// src/Backend/VanDaemon.Core/Enums/MaintenanceType.cs
public enum MaintenanceType
{
    OilChange,
    TireRotation,
    FilterReplacement,
    Inspection,
    Other
}
```

## Creating a Plugin

Copy this checklist and track progress:
- [ ] Create plugin class implementing `ISensorPlugin` or `IControlPlugin`
- [ ] Implement `IDisposable` for cleanup
- [ ] Register in `Program.cs`
- [ ] Initialize after `app.Build()`
- [ ] Add configuration to `appsettings.json`

### Plugin Template

```csharp
public class WeatherPlugin : ISensorPlugin, IDisposable
{
    private readonly ILogger<WeatherPlugin> _logger;
    private readonly HttpClient _httpClient;
    private bool _disposed;
    private Dictionary<string, object> _config = new();

    public string Name => "Weather Plugin";
    public string Version => "1.0.0";

    public WeatherPlugin(ILogger<WeatherPlugin> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient();
    }

    public Task InitializeAsync(Dictionary<string, object> config, CancellationToken ct = default)
    {
        _config = config;
        _logger.LogInformation("{PluginName} v{Version} initialized", Name, Version);
        return Task.CompletedTask;
    }

    public Task<bool> TestConnectionAsync(CancellationToken ct = default)
        => Task.FromResult(true);

    public async Task<double> ReadValueAsync(string sensorId, CancellationToken ct = default)
    {
        // Implementation
        return 0.0;
    }

    public Task<IDictionary<string, double>> ReadAllValuesAsync(CancellationToken ct = default)
        => Task.FromResult<IDictionary<string, double>>(new Dictionary<string, double>());

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _httpClient.Dispose();
    }
}
```

### Registration and Initialization

```csharp
// Program.cs - Registration
builder.Services.AddSingleton<WeatherPlugin>();
builder.Services.AddSingleton<ISensorPlugin>(sp => sp.GetRequiredService<WeatherPlugin>());

// After app.Build() - Initialization
var weatherPlugin = app.Services.GetRequiredService<WeatherPlugin>();
await weatherPlugin.InitializeAsync(new Dictionary<string, object>
{
    ["ApiKey"] = configuration["Weather:ApiKey"] ?? ""
});
```

## Background Service Pattern

```csharp
public class PollingBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PollingBackgroundService> _logger;

    public PollingBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<PollingBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Create scope for scoped services if needed
                using var scope = _serviceProvider.CreateScope();
                var tankService = scope.ServiceProvider.GetRequiredService<ITankService>();
                
                await tankService.RefreshAllLevelsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in polling loop");
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
```

## Testing Workflows

See the **xunit** skill for detailed testing patterns.

### Service Test Pattern

```csharp
public class TankServiceTests
{
    private readonly Mock<ILogger<TankService>> _loggerMock;
    private readonly string _tempPath;
    private readonly JsonFileStore _fileStore;

    public TankServiceTests()
    {
        _loggerMock = new Mock<ILogger<TankService>>();
        _tempPath = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempPath);
        _fileStore = new JsonFileStore(
            new Mock<ILogger<JsonFileStore>>().Object, _tempPath);
    }

    [Fact]
    public async Task GetAllTanksAsync_ReturnsOnlyActiveTanks()
    {
        // Arrange
        var service = new TankService(_loggerMock.Object, _fileStore, Array.Empty<ISensorPlugin>());
        
        // Act
        var result = await service.GetAllTanksAsync();
        
        // Assert
        result.Should().NotBeNull();
        result.Should().AllSatisfy(t => t.IsActive.Should().BeTrue());
    }
}
```

### Validation Loop

1. Write/modify code
2. Run tests: `dotnet test VanDaemon.sln`
3. If tests fail, fix issues and repeat step 2
4. Only commit when all tests pass