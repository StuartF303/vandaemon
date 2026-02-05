# ASP.NET Core Patterns Reference

## Contents
- Controller Patterns
- Dependency Injection
- Background Services
- Error Handling
- Anti-Patterns

## Controller Patterns

### Thin Controller Pattern

Controllers should only handle HTTP concerns. All business logic lives in services.

```csharp
// GOOD - Thin controller
[HttpGet("{id}")]
public async Task<ActionResult<Tank>> GetById(Guid id, CancellationToken ct)
{
    var tank = await _tankService.GetByIdAsync(id, ct);
    return tank is null ? NotFound() : Ok(tank);
}

// BAD - Logic in controller
[HttpGet("{id}")]
public async Task<ActionResult<Tank>> GetById(Guid id)
{
    var tanks = await _fileStore.LoadAsync<List<Tank>>("tanks.json");
    var tank = tanks.FirstOrDefault(t => t.Id == id && t.IsActive);
    if (tank == null) return NotFound();
    tank.LastAccessed = DateTime.UtcNow;
    await _fileStore.SaveAsync("tanks.json", tanks);
    return Ok(tank);
}
```

### Route Conventions

```csharp
[ApiController]
[Route("api/[controller]")]  // Derives from class name: TanksController → api/tanks
public class TanksController : ControllerBase
{
    [HttpGet]                    // GET api/tanks
    [HttpGet("{id}")]            // GET api/tanks/{id}
    [HttpGet("{id}/level")]      // GET api/tanks/{id}/level
    [HttpPost]                   // POST api/tanks
    [HttpPost("{id}/state")]     // POST api/tanks/{id}/state
    [HttpPut("{id}")]            // PUT api/tanks/{id}
    [HttpDelete("{id}")]         // DELETE api/tanks/{id}
}
```

## Dependency Injection

### VanDaemon Service Lifetimes

| Lifetime | Usage | Examples |
|----------|-------|----------|
| Singleton | Services with state, plugins, file stores | `TankService`, `JsonFileStore` |
| Scoped | Per-request (auto for controllers) | Controllers |
| Hosted | Background tasks | `TelemetryBackgroundService` |

```csharp
// Program.cs registration order matters
builder.Services.AddSingleton<JsonFileStore>();
builder.Services.AddSingleton<ITankService, TankService>();
builder.Services.AddSingleton<IControlService, ControlService>();
builder.Services.AddHostedService<TelemetryBackgroundService>();
```

### Plugin Registration Pattern

```csharp
// Register plugin as both concrete and interface
builder.Services.AddSingleton<MqttLedDimmerPlugin>();
builder.Services.AddSingleton<IControlPlugin>(sp => 
    sp.GetRequiredService<MqttLedDimmerPlugin>());

// Initialize after Build(), before Run()
var app = builder.Build();
var plugins = app.Services.GetServices<IControlPlugin>();
foreach (var plugin in plugins)
{
    await plugin.InitializeAsync(config);
}
app.Run();
```

## Background Services

### Accessing Scoped Services

Background services are singletons. Create scope to access scoped services.

```csharp
public class TelemetryBackgroundService : BackgroundService
{
    private readonly IServiceProvider _provider;
    private readonly IHubContext<TelemetryHub> _hubContext;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            using var scope = _provider.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<ITankService>();
            
            var tanks = await service.GetAllTanksAsync(ct);
            await _hubContext.Clients.Group("tanks")
                .SendAsync("TankLevelUpdated", tanks, ct);
            
            await Task.Delay(5000, ct);
        }
    }
}
```

## Error Handling

### Global Exception Handler

```csharp
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new
        {
            error = "An unexpected error occurred",
            timestamp = DateTime.UtcNow
        });
    });
});
```

### WARNING: Exposing Internal Errors

**The Problem:**
```csharp
// BAD - Leaks implementation details
catch (Exception ex)
{
    return StatusCode(500, ex.ToString());
}
```

**Why This Breaks:** Exposes stack traces, connection strings, internal paths to attackers.

**The Fix:**
```csharp
// GOOD - Log internally, return safe message
catch (Exception ex)
{
    _logger.LogError(ex, "Failed to process tank {TankId}", id);
    return StatusCode(500, new { error = "Failed to process request" });
}
```

## Anti-Patterns

### WARNING: Missing CancellationToken

**The Problem:**
```csharp
// BAD - Ignores cancellation
[HttpGet]
public async Task<ActionResult<List<Tank>>> GetAll()
{
    return Ok(await _service.GetAllTanksAsync());
}
```

**Why This Breaks:** Request continues processing after client disconnects, wasting resources.

**The Fix:**
```csharp
// GOOD - Propagate cancellation
[HttpGet]
public async Task<ActionResult<List<Tank>>> GetAll(CancellationToken ct)
{
    return Ok(await _service.GetAllTanksAsync(ct));
}
```

### WARNING: Wrong Service Lifetime

**The Problem:**
```csharp
// BAD - Transient service with state
builder.Services.AddTransient<ITankService, TankService>();
// TankService holds List<Tank> in memory - each request gets fresh empty list!
```

**Why This Breaks:** State is lost between requests. VanDaemon services hold in-memory state.

**The Fix:**
```csharp
// GOOD - Singleton for stateful services
builder.Services.AddSingleton<ITankService, TankService>();
```

### WARNING: Blocking Async Context

**The Problem:**
```csharp
// BAD - Blocks thread pool
var result = _service.GetDataAsync().Result;
var data = _service.LoadAsync().GetAwaiter().GetResult();
```

**Why This Breaks:** Deadlocks in ASP.NET Core, exhausts thread pool under load.

**The Fix:**
```csharp
// GOOD - Await properly
var result = await _service.GetDataAsync(ct);