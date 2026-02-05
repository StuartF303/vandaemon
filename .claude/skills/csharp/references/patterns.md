# C# Patterns Reference

## Contents
- Async Patterns
- Dependency Injection
- Entity Design
- Service Layer Patterns
- Anti-Patterns

## Async Patterns

### Always Accept CancellationToken

```csharp
// GOOD - Allows graceful cancellation
public async Task<Tank?> GetTankAsync(Guid id, CancellationToken cancellationToken = default)
{
    return await _fileStore.LoadAsync<Tank>($"tank-{id}.json", cancellationToken);
}

// BAD - No way to cancel long-running operations
public async Task<Tank?> GetTankAsync(Guid id)
{
    return await _fileStore.LoadAsync<Tank>($"tank-{id}.json");
}
```

### Async Suffix Convention

```csharp
// GOOD - Clear async method naming
Task<List<Tank>> GetAllTanksAsync(CancellationToken ct);
Task UpdateTankAsync(Tank tank, CancellationToken ct);

// BAD - Confusing, looks synchronous
Task<List<Tank>> GetAllTanks(CancellationToken ct);
```

## Dependency Injection

### Constructor Injection Pattern

```csharp
public class TankService : ITankService
{
    private readonly ILogger<TankService> _logger;
    private readonly JsonFileStore _fileStore;
    private readonly IEnumerable<ISensorPlugin> _sensorPlugins;

    // All dependencies via constructor
    public TankService(
        ILogger<TankService> logger,
        JsonFileStore fileStore,
        IEnumerable<ISensorPlugin> sensorPlugins)
    {
        _logger = logger;
        _fileStore = fileStore;
        _sensorPlugins = sensorPlugins;
    }
}
```

### Registration in Program.cs

```csharp
// Services - always singletons in VanDaemon
builder.Services.AddSingleton<ITankService, TankService>();
builder.Services.AddSingleton<IControlService, ControlService>();

// Plugins - register concrete, then interface
builder.Services.AddSingleton<MqttLedDimmerPlugin>();
builder.Services.AddSingleton<IControlPlugin>(sp => sp.GetRequiredService<MqttLedDimmerPlugin>());
```

## Entity Design

### Standard Entity Structure

```csharp
public class Control
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ControlType Type { get; set; }
    public object? State { get; set; }  // Cast based on Type
    public string ControlPlugin { get; set; } = string.Empty;
    public Dictionary<string, object> ControlConfiguration { get; set; } = new();
    public DateTime LastUpdated { get; set; }
    public bool IsActive { get; set; } = true;
    public string? IconName { get; set; }
}
```

### WARNING: State Type Handling

**The Problem:**

```csharp
// BAD - Assumes State is always bool
var isOn = (bool)control.State;  // Throws if State is int (Dimmer)
```

**Why This Breaks:**
1. `ControlType.Toggle` uses `bool`
2. `ControlType.Dimmer` uses `int` (0-255)
3. Direct cast crashes at runtime

**The Fix:**

```csharp
// GOOD - Type-safe state access
var state = control.Type switch
{
    ControlType.Toggle => Convert.ToBoolean(control.State),
    ControlType.Dimmer => Convert.ToInt32(control.State),
    _ => control.State
};
```

## Service Layer Patterns

### Filtering Active Entities

```csharp
public async Task<IReadOnlyList<Tank>> GetAllTanksAsync(CancellationToken ct = default)
{
    var tanks = await _fileStore.LoadAsync<List<Tank>>("tanks.json", ct);
    return tanks?.Where(t => t.IsActive).ToList().AsReadOnly()
        ?? new List<Tank>().AsReadOnly();
}
```

### Structured Logging

```csharp
// GOOD - Named placeholders for structured logging
_logger.LogInformation("Tank {TankId} level updated to {Level}%", tank.Id, tank.CurrentLevel);
_logger.LogWarning("Plugin {PluginName} connection failed after {RetryCount} attempts", name, retries);

// BAD - String interpolation loses structure
_logger.LogInformation($"Tank {tank.Id} level updated to {tank.CurrentLevel}%");
```

## Anti-Patterns

### WARNING: Blocking Async Code

**The Problem:**

```csharp
// BAD - Blocks thread pool, causes deadlocks
var result = GetDataAsync().Result;
var result2 = GetDataAsync().GetAwaiter().GetResult();
```

**Why This Breaks:**
1. Deadlocks in ASP.NET Core under load
2. Wastes thread pool threads
3. Defeats async benefits

**The Fix:**

```csharp
// GOOD - Async all the way
var result = await GetDataAsync();
```

### WARNING: Missing Null Checks with Nullable Types

**The Problem:**

```csharp
// BAD - Tank? can be null
Tank? tank = await GetTankAsync(id);
var name = tank.Name;  // NullReferenceException
```

**The Fix:**

```csharp
// GOOD - Handle null explicitly
Tank? tank = await GetTankAsync(id);
if (tank is null)
{
    _logger.LogWarning("Tank {TankId} not found", id);
    return null;
}
var name = tank.Name;
```

### WARNING: Disposing Singletons in Background Services

**The Problem:**

```csharp
// BAD - Service is singleton, outlives scope
using var scope = _serviceProvider.CreateScope();
var tankService = scope.ServiceProvider.GetRequiredService<ITankService>();
// tankService disposed when scope ends, but singleton still referenced
```

**The Fix:**

```csharp
// GOOD - Don't dispose singleton services
var scope = _serviceProvider.CreateScope();
var tankService = scope.ServiceProvider.GetRequiredService<ITankService>();
// Use tankService, don't dispose scope containing singletons