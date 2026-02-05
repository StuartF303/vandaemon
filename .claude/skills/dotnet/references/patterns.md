# .NET Patterns Reference

## Contents
- Project Structure
- Dependency Injection Registration
- Configuration Patterns
- Build Configuration
- Common Anti-Patterns

## Project Structure

VanDaemon follows Clean Architecture with strict layer dependencies:

```
src/Backend/
├── VanDaemon.Api/           # Entry point, controllers, hubs
├── VanDaemon.Application/   # Services, interfaces, JsonFileStore
├── VanDaemon.Core/          # Entities, enums (no dependencies)
├── VanDaemon.Infrastructure/# Future SQLite (empty)
└── VanDaemon.Plugins/
    ├── Abstractions/        # Plugin interfaces
    ├── Simulated/           # Test plugins
    ├── Modbus/              # Real hardware
    └── MqttLedDimmer/       # MQTT control
```

### Project File Template

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\VanDaemon.Core\VanDaemon.Core.csproj" />
  </ItemGroup>
</Project>
```

## Dependency Injection Registration

### Service Registration in Program.cs

```csharp
// Singletons for stateful services
builder.Services.AddSingleton<ITankService, TankService>();
builder.Services.AddSingleton<IControlService, ControlService>();
builder.Services.AddSingleton<JsonFileStore>();

// Plugin registration (singleton with factory for multiple implementations)
builder.Services.AddSingleton<MqttLedDimmerPlugin>();
builder.Services.AddSingleton<IControlPlugin>(sp => 
    sp.GetRequiredService<MqttLedDimmerPlugin>());

// Hosted services for background work
builder.Services.AddHostedService<TelemetryBackgroundService>();
builder.Services.AddHostedService<MqttLedDimmerService>();
```

### WARNING: Plugin Initialization Order

**The Problem:**

```csharp
// BAD - Plugins not initialized before app starts
var app = builder.Build();
app.Run(); // Plugins never initialized!
```

**Why This Breaks:**
Plugins require async initialization (MQTT connections, hardware setup). Without explicit initialization, they remain in an unconnected state.

**The Fix:**

```csharp
var app = builder.Build();

// Initialize plugins AFTER Build() but BEFORE Run()
var plugins = app.Services.GetServices<IControlPlugin>();
foreach (var plugin in plugins)
{
    await plugin.InitializeAsync(config);
}

app.Run();
```

## Configuration Patterns

### Strongly-Typed Options

```csharp
// Options class
public class MqttLedDimmerOptions
{
    public string MqttBroker { get; set; } = "localhost";
    public int MqttPort { get; set; } = 1883;
    public string BaseTopic { get; set; } = "vandaemon/leddimmer";
}

// Registration
builder.Services.Configure<MqttLedDimmerOptions>(
    builder.Configuration.GetSection("MqttLedDimmer"));

// Usage via injection
public class MqttLedDimmerPlugin(IOptions<MqttLedDimmerOptions> options)
{
    private readonly MqttLedDimmerOptions _options = options.Value;
}
```

### Environment-Specific Configuration

```
appsettings.json              # Base configuration
appsettings.Development.json  # Development overrides
appsettings.Production.json   # Production overrides
```

Environment selected via `ASPNETCORE_ENVIRONMENT` variable.

## Build Configuration

### Release Build with Optimization

```bash
dotnet build --configuration Release
dotnet publish -c Release -o ./publish
```

### Docker Multi-Stage Build

```dockerfile
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish -c Release -o /app

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "VanDaemon.Api.dll"]
```

## Common Anti-Patterns

### WARNING: Scoped Services in Singletons

**The Problem:**

```csharp
// BAD - Injecting scoped service into singleton
public class TelemetryBackgroundService : BackgroundService
{
    private readonly DbContext _context; // Scoped!
    
    public TelemetryBackgroundService(DbContext context)
    {
        _context = context; // Captured scoped instance
    }
}
```

**Why This Breaks:**
DbContext is scoped (one per request). Background services are singletons that outlive any request scope, causing the DbContext to be disposed while still in use.

**The Fix:**

```csharp
// GOOD - Create scope for each operation
public class TelemetryBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DbContext>();
            // Use context within this scope
        }
    }
}
```

### WARNING: Missing CancellationToken Propagation

**The Problem:**

```csharp
// BAD - Ignoring cancellation
public async Task<List<Tank>> GetAllTanksAsync()
{
    return await _fileStore.LoadAsync<List<Tank>>("tanks.json");
}
```

**The Fix:**

```csharp
// GOOD - Propagate cancellation token
public async Task<List<Tank>> GetAllTanksAsync(CancellationToken ct = default)
{
    return await _fileStore.LoadAsync<List<Tank>>("tanks.json", ct);
}