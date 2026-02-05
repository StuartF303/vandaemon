# ASP.NET Core Workflows Reference

## Contents
- Adding a New API Endpoint
- Adding a New Service
- Setting Up SignalR Hub
- Configuring CORS
- Adding Health Checks

## Adding a New API Endpoint

### Workflow Checklist

Copy this checklist and track progress:
- [ ] Step 1: Create/extend controller in `Api/Controllers/`
- [ ] Step 2: Inject service via constructor
- [ ] Step 3: Add endpoint method with attributes
- [ ] Step 4: Return `ActionResult<T>`, accept `CancellationToken`
- [ ] Step 5: Test endpoint via Swagger or curl

### Example: Adding Tank Level Endpoint

```csharp
// src/Backend/VanDaemon.Api/Controllers/TanksController.cs
[HttpGet("{id}/level")]
public async Task<ActionResult<double>> GetLevel(Guid id, CancellationToken ct)
{
    var level = await _tankService.GetTankLevelAsync(id, ct);
    return level.HasValue ? Ok(level.Value) : NotFound();
}
```

### Validation Loop

1. Add endpoint code
2. Validate: `dotnet build src/Backend/VanDaemon.Api`
3. If build fails, fix errors and repeat step 2
4. Test: `curl http://localhost:5000/api/tanks/{id}/level`

## Adding a New Service

### Workflow Checklist

Copy this checklist and track progress:
- [ ] Step 1: Create interface in `Application/Interfaces/I{Name}Service.cs`
- [ ] Step 2: Create implementation in `Application/Services/{Name}Service.cs`
- [ ] Step 3: Accept dependencies via constructor injection
- [ ] Step 4: Register in `Program.cs` as singleton
- [ ] Step 5: Add persistence calls if needed (JsonFileStore)

### Example: Creating AlertService

```csharp
// Step 1: Interface
public interface IAlertService
{
    Task<IEnumerable<Alert>> GetAllAlertsAsync(CancellationToken ct = default);
    Task<Alert> CreateAlertAsync(Alert alert, CancellationToken ct = default);
    Task AcknowledgeAlertAsync(Guid id, CancellationToken ct = default);
}

// Step 2: Implementation
public class AlertService : IAlertService
{
    private readonly ILogger<AlertService> _logger;
    private readonly JsonFileStore _fileStore;
    private readonly List<Alert> _alerts = new();

    public AlertService(ILogger<AlertService> logger, JsonFileStore fileStore)
    {
        _logger = logger;
        _fileStore = fileStore;
    }

    public async Task<IEnumerable<Alert>> GetAllAlertsAsync(CancellationToken ct)
    {
        return _alerts.Where(a => !a.Acknowledged).ToList();
    }
}

// Step 4: Registration in Program.cs
builder.Services.AddSingleton<IAlertService, AlertService>();
```

## Setting Up SignalR Hub

### Hub Implementation

```csharp
// src/Backend/VanDaemon.Api/Hubs/TelemetryHub.cs
public class TelemetryHub : Hub
{
    public async Task SubscribeToTanks()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "tanks");
    }

    public async Task SubscribeToControls()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "controls");
    }

    public async Task SubscribeToAlerts()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "alerts");
    }
}
```

### Registration in Program.cs

```csharp
builder.Services.AddSignalR();

var app = builder.Build();

app.MapHub<TelemetryHub>("/hubs/telemetry");
```

### Broadcasting from Services

```csharp
public class TankService : ITankService
{
    private readonly IHubContext<TelemetryHub> _hubContext;

    public async Task UpdateTankLevelAsync(Guid id, double level, CancellationToken ct)
    {
        // Update internal state
        var tank = _tanks.First(t => t.Id == id);
        tank.CurrentLevel = level;

        // Broadcast to subscribers
        await _hubContext.Clients.Group("tanks")
            .SendAsync("TankLevelUpdated", id, level, tank.Name, ct);
    }
}
```

See the **signalr** skill for client-side patterns and connection management.

## Configuring CORS

### Development CORS (VanDaemon Pattern)

```csharp
// Program.cs
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:5001", "http://localhost:8080")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();  // Required for SignalR
    });
});

var app = builder.Build();
app.UseCors();  // Before UseRouting
```

### WARNING: AllowAnyOrigin with Credentials

**The Problem:**
```csharp
// BAD - Security vulnerability
policy.AllowAnyOrigin()
      .AllowCredentials();  // Throws exception in modern ASP.NET Core
```

**Why This Breaks:** Allows any website to make authenticated requests to your API.

**The Fix:**
```csharp
// GOOD - Explicit origins
policy.WithOrigins("http://localhost:5001")
      .AllowCredentials();
```

## Adding Health Checks

### Basic Health Endpoint

```csharp
// Program.cs
app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    timestamp = DateTime.UtcNow
}));
```

### Health Check with Dependencies

```csharp
builder.Services.AddHealthChecks()
    .AddCheck("api", () => HealthCheckResult.Healthy())
    .AddCheck<PluginHealthCheck>("plugins");

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString()
            }),
            timestamp = DateTime.UtcNow
        });
    }
});
```

### Custom Health Check

```csharp
public class PluginHealthCheck : IHealthCheck
{
    private readonly IEnumerable<IControlPlugin> _plugins;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, 
        CancellationToken ct = default)
    {
        foreach (var plugin in _plugins)
        {
            if (!await plugin.TestConnectionAsync(ct))
            {
                return HealthCheckResult.Unhealthy($"Plugin {plugin.Name} disconnected");
            }
        }
        return HealthCheckResult.Healthy();
    }
}
```

## Related Skills

- See the **signalr** skill for hub patterns and real-time communication
- See the **serilog** skill for structured logging in services
- See the **xunit** skill for testing controllers and services
- See the **docker** skill for containerized deployment configuration