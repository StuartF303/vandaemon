# SignalR Workflows Reference

## Contents
- Adding a New SignalR Event
- Testing SignalR Connections
- Nginx WebSocket Configuration
- Troubleshooting Connection Issues

## Adding a New SignalR Event

### Workflow Checklist

Copy this checklist and track progress:
- [ ] Step 1: Add hub method (if client-initiated)
- [ ] Step 2: Define event name constant
- [ ] Step 3: Add server broadcast code
- [ ] Step 4: Add client handler
- [ ] Step 5: Test with multiple clients

### Step 1: Add Hub Method (Optional)

Only needed if clients initiate the action:

```csharp
// TelemetryHub.cs
public async Task SubscribeToElectrical()
{
    await Groups.AddToGroupAsync(Context.ConnectionId, "electrical");
}
```

### Step 2: Define Event in Service

```csharp
// Broadcasting from ElectricalService
public async Task UpdateElectricalStateAsync(ElectricalSystem state, CancellationToken ct)
{
    _currentState = state;
    
    await _hubContext.Clients.Group("electrical").SendAsync(
        "ElectricalStateUpdated",
        state.BatteryVoltage,
        state.BatteryCurrent,
        state.SolarWatts,
        ct);
}
```

### Step 3: Add Client Handler

```csharp
// Blazor component
_hubConnection.On<double, double, double>("ElectricalStateUpdated", 
    async (voltage, current, solar) =>
{
    _batteryVoltage = voltage;
    _batteryCurrent = current;
    _solarWatts = solar;
    await InvokeAsync(StateHasChanged);
});
```

### Step 4: Validate

1. Run API: `dotnet run --project src/Backend/VanDaemon.Api`
2. Run Web: `dotnet run --project src/Frontend/VanDaemon.Web`
3. Open browser DevTools → Network → WS tab
4. Verify WebSocket messages appear

## Testing SignalR Connections

### Manual Testing with Browser Console

```javascript
// In browser DevTools console
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/telemetry")
    .build();

connection.on("TankLevelUpdated", (id, level, name) => {
    console.log(`Tank ${name}: ${level}%`);
});

await connection.start();
await connection.invoke("SubscribeToTanks");
```

### Unit Testing Hub Methods

```csharp
[Fact]
public async Task SubscribeToTanks_AddsClientToGroup()
{
    // Arrange
    var mockGroups = new Mock<IGroupManager>();
    var mockClients = new Mock<IHubCallerClients>();
    var mockContext = new Mock<HubCallerContext>();
    mockContext.Setup(c => c.ConnectionId).Returns("test-connection-id");

    var hub = new TelemetryHub(Mock.Of<ILogger<TelemetryHub>>())
    {
        Groups = mockGroups.Object,
        Clients = mockClients.Object,
        Context = mockContext.Object
    };

    // Act
    await hub.SubscribeToTanks();

    // Assert
    mockGroups.Verify(g => g.AddToGroupAsync(
        "test-connection-id", 
        "tanks", 
        It.IsAny<CancellationToken>()), Times.Once);
}
```

See the **xunit** and **moq** skills for testing patterns.

## Nginx WebSocket Configuration

### Required nginx.conf Settings

```nginx
# docker/nginx.combined.conf
map $http_upgrade $connection_upgrade {
    default upgrade;
    '' close;
}

server {
    listen 8080;

    location /hubs/ {
        proxy_pass http://127.0.0.1:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection $connection_upgrade;
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
        proxy_read_timeout 7d;  # Long timeout for persistent connections
    }
}
```

### WARNING: Missing WebSocket Headers

**The Problem:**

```nginx
# BAD - Missing upgrade headers
location /hubs/ {
    proxy_pass http://127.0.0.1:5000;
}
```

**Why This Breaks:**
1. WebSocket upgrade request fails
2. SignalR falls back to long-polling (slow, resource-intensive)
3. Real-time updates delayed by seconds instead of milliseconds

**The Fix:**
Include all WebSocket headers as shown above.

See the **docker** skill for full nginx configuration.

## Troubleshooting Connection Issues

### Diagnostic Workflow

1. Check API health:
   ```bash
   curl http://localhost:5000/health
   ```

2. Verify WebSocket endpoint:
   ```bash
   curl -i -N -H "Connection: Upgrade" \
        -H "Upgrade: websocket" \
        -H "Sec-WebSocket-Version: 13" \
        -H "Sec-WebSocket-Key: test" \
        http://localhost:5000/hubs/telemetry
   # Should return 101 Switching Protocols
   ```

3. Check browser DevTools:
   - Network → WS tab shows connection
   - Console shows no CORS errors

4. Verify nginx proxy (if Docker):
   ```bash
   docker logs vandaemon-web 2>&1 | grep -i websocket
   ```

### Common Issues and Fixes

| Symptom | Cause | Fix |
|---------|-------|-----|
| 404 on /hubs/telemetry | Hub not mapped | Add `app.MapHub<TelemetryHub>("/hubs/telemetry")` |
| 502 Bad Gateway | API not running | Start API before web container |
| Falls back to polling | Missing nginx headers | Add WebSocket upgrade headers |
| Disconnects after 60s | Nginx timeout | Set `proxy_read_timeout 7d` |
| CORS error | Wrong origin | Add frontend URL to CORS policy |

### CORS Configuration for Development

```csharp
// Program.cs
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:5001")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();  // Required for SignalR
    });
});
```

### Iterate-Until-Pass Pattern

1. Make configuration change
2. Restart affected service: `docker compose restart web`
3. Test WebSocket: Check browser Network → WS tab
4. If connection fails, check logs and repeat from step 1
5. Only proceed when WebSocket shows "101 Switching Protocols"