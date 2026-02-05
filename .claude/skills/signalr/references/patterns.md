# SignalR Patterns Reference

## Contents
- Hub Design Patterns
- Broadcasting from Services
- Client Connection Patterns
- Error Handling
- Anti-Patterns

## Hub Design Patterns

### Subscription-Based Hub

VanDaemon uses group subscriptions so clients receive only relevant updates:

```csharp
public class TelemetryHub : Hub
{
    private readonly ILogger<TelemetryHub> _logger;

    public TelemetryHub(ILogger<TelemetryHub> logger)
    {
        _logger = logger;
    }

    public async Task SubscribeToTanks()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "tanks");
        _logger.LogInformation("Client {ConnectionId} subscribed to tanks", Context.ConnectionId);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client {ConnectionId} disconnected", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
```

### Hub Registration

```csharp
// Program.cs
builder.Services.AddSignalR();

// After app.Build()
app.MapHub<TelemetryHub>("/hubs/telemetry");
```

## Broadcasting from Services

### Using IHubContext in Background Services

```csharp
public class TelemetryBackgroundService : BackgroundService
{
    private readonly IHubContext<TelemetryHub> _hubContext;
    private readonly ITankService _tankService;

    public TelemetryBackgroundService(
        IHubContext<TelemetryHub> hubContext,
        ITankService tankService)
    {
        _hubContext = hubContext;
        _tankService = tankService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var tanks = await _tankService.GetAllTanksAsync(stoppingToken);
            
            foreach (var tank in tanks)
            {
                await _hubContext.Clients.Group("tanks").SendAsync(
                    "TankLevelUpdated",
                    tank.Id,
                    tank.CurrentLevel,
                    tank.Name,
                    stoppingToken);
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
```

### WARNING: Calling Hub Methods Directly from IHubContext

**The Problem:**

```csharp
// BAD - IHubContext doesn't have access to hub methods
await _hubContext.Clients.All.SendAsync("SubscribeToTanks");
```

**Why This Breaks:**
1. `IHubContext` only broadcasts—it cannot invoke hub methods
2. Hub methods like `SubscribeToTanks` are client-initiated
3. Server cannot force clients into groups

**The Fix:**

```csharp
// GOOD - Broadcast events, let clients manage subscriptions
await _hubContext.Clients.Group("tanks").SendAsync("TankLevelUpdated", id, level, name);
```

## Client Connection Patterns

### Blazor WASM Connection Setup

```csharp
@inject NavigationManager NavigationManager
@implements IAsyncDisposable

private HubConnection? _hubConnection;
private bool _isConnected;

protected override async Task OnInitializedAsync()
{
    var baseUri = NavigationManager.BaseUri.TrimEnd('/');
    
    _hubConnection = new HubConnectionBuilder()
        .WithUrl($"{baseUri}/hubs/telemetry")
        .WithAutomaticReconnect(new[] { 
            TimeSpan.FromSeconds(0),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(30)
        })
        .Build();

    RegisterHandlers();
    await _hubConnection.StartAsync();
    await SubscribeToGroups();
    _isConnected = true;
}

private void RegisterHandlers()
{
    _hubConnection!.On<Guid, double, string>("TankLevelUpdated", async (id, level, name) =>
    {
        UpdateTankLevel(id, level);
        await InvokeAsync(StateHasChanged);
    });
}

public async ValueTask DisposeAsync()
{
    if (_hubConnection is not null)
    {
        await _hubConnection.DisposeAsync();
    }
}
```

### Re-Subscribe After Reconnect

```csharp
_hubConnection.Reconnected += async (connectionId) =>
{
    _logger.LogInformation("Reconnected with ID: {ConnectionId}", connectionId);
    
    // Must re-subscribe—groups are lost on disconnect
    await _hubConnection.InvokeAsync("SubscribeToTanks");
    await _hubConnection.InvokeAsync("SubscribeToControls");
    
    _isConnected = true;
    await InvokeAsync(StateHasChanged);
};
```

## Error Handling

### Connection Failure Handling

```csharp
try
{
    await _hubConnection.StartAsync();
}
catch (HttpRequestException ex)
{
    _logger.LogError(ex, "Failed to connect to SignalR hub");
    _connectionError = "Unable to connect to server";
}
```

### WARNING: Not Handling Disconnections

**The Problem:**

```csharp
// BAD - No handling for connection loss
await _hubConnection.StartAsync();
// UI continues assuming connection is alive
```

**Why This Breaks:**
1. Network interruptions silently break real-time updates
2. Users see stale data without knowing connection is lost
3. No automatic recovery path

**The Fix:**

```csharp
// GOOD - Handle all connection states
_hubConnection.Closed += async (error) =>
{
    _isConnected = false;
    _connectionError = error?.Message ?? "Connection lost";
    await InvokeAsync(StateHasChanged);
};

_hubConnection.Reconnecting += async (error) =>
{
    _isConnected = false;
    _connectionError = "Reconnecting...";
    await InvokeAsync(StateHasChanged);
};
```

## Anti-Patterns

### WARNING: Sending Large Payloads

**The Problem:**

```csharp
// BAD - Sending entire entity list on every update
await _hubContext.Clients.Group("tanks").SendAsync("AllTanksUpdated", allTanks);
```

**Why This Breaks:**
1. Wastes bandwidth on unchanged data
2. Increases client processing load
3. Scales poorly with entity count

**The Fix:**

```csharp
// GOOD - Send only changed data
await _hubContext.Clients.Group("tanks").SendAsync(
    "TankLevelUpdated", tank.Id, tank.CurrentLevel, tank.Name);
```

### WARNING: Missing CancellationToken

**The Problem:**

```csharp
// BAD - No cancellation support
await _hubContext.Clients.Group("tanks").SendAsync("TankLevelUpdated", id, level, name);
```

**The Fix:**

```csharp
// GOOD - Pass cancellation token
await _hubContext.Clients.Group("tanks").SendAsync(
    "TankLevelUpdated", id, level, name, cancellationToken);