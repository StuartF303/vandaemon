# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

VanDaemon is an IoT control system for camper vans built with .NET 10, Blazor WebAssembly, and SignalR. It monitors and controls van systems (water tanks, LPG, lighting, heating) through a modular plugin architecture that supports multiple hardware integration methods.

## Build and Development Commands

### Building
```bash
# Full build with tests and Docker images
./build.sh              # Linux/Mac
build.bat               # Windows

# Build solution only
dotnet build VanDaemon.sln

# Build specific configuration
dotnet build VanDaemon.sln --configuration Release

# Clean build artifacts
dotnet clean VanDaemon.sln
```

### Running Tests
```bash
# Run all tests
dotnet test VanDaemon.sln

# Run tests with detailed output
dotnet test VanDaemon.sln --verbosity normal

# Run specific test project
dotnet test tests/VanDaemon.Application.Tests/VanDaemon.Application.Tests.csproj

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"
```

### Running the Application

**Development mode (two terminals required):**
```bash
# Terminal 1 - Backend API (runs on port 5000)
cd src/Backend/VanDaemon.Api
dotnet run
# API available at: http://localhost:5000

# Terminal 2 - Frontend Web (runs on port 5001)
cd src/Frontend/VanDaemon.Web
dotnet run
# Web UI available at: http://localhost:5001
```

**Important for Development:**
- The API runs on port **5000** (configured in `Properties/launchSettings.json`)
- The frontend runs on port **5001** (configured in `Properties/launchSettings.json`)
- The frontend loads API URL from `wwwroot/appsettings.json` in development
- CORS is configured to allow `localhost:5001` → `localhost:5000` communication

**Docker mode (recommended for production):**
```bash
# Run from solution root
docker compose up -d        # Start services
docker compose logs -f      # View logs
docker compose down         # Stop services
docker compose down -v      # Stop and remove volumes
```

**Access points:**
- **Development:**
  - Web UI: http://localhost:5001
  - API: http://localhost:5000
  - Swagger: http://localhost:5000/swagger
  - SignalR Hub: ws://localhost:5000/hubs/telemetry

- **Docker/Production:**
  - Web UI: http://localhost:8080
  - API: http://localhost:5000
  - Swagger: http://localhost:5000/swagger
  - SignalR Hub: ws://localhost:5000/hubs/telemetry

## Architecture Overview

### Layered Architecture Pattern

VanDaemon follows **Clean Architecture** with clear separation of concerns:

**Core Layer** (`VanDaemon.Core`)
- Domain entities: `Tank`, `Control`, `Alert`, `SystemSettings`
- Enums: `TankType`, `ControlType`, `AlertSeverity`
- No dependencies on other layers
- Pure domain models with minimal logic

**Application Layer** (`VanDaemon.Application`)
- Service interfaces: `ITankService`, `IControlService`, `IAlertService`, `ISettingsService`
- Service implementations with business logic
- `JsonFileStore` for configuration persistence
- All services are **Singletons** to maintain in-memory state

**Infrastructure Layer** (`VanDaemon.Infrastructure`)
- Currently empty (placeholder for future SQLite implementation)
- Configuration is persisted via JSON files in `data/` directory

**API Layer** (`VanDaemon.Api`)
- REST API controllers (thin wrappers around services)
- SignalR hub: `TelemetryHub` at `/hubs/telemetry`
- Background service: `TelemetryBackgroundService` (5-second polling loop)
- Hosted service initialization and dependency injection setup

**Frontend Layer** (`VanDaemon.Web`)
- Blazor WebAssembly SPA
- MudBlazor UI components
- SignalR client for real-time updates
- Pages: Dashboard, Tanks, Controls, Settings

### Plugin System Architecture

**Core Concept:** Hardware abstraction through plugin interfaces enables support for multiple sensor/control types without changing application code.

**Plugin Interfaces** (in `VanDaemon.Plugins.Abstractions`):

1. **IHardwarePlugin** - Base interface for all plugins
   - `Name`, `Version` properties
   - `InitializeAsync(Dictionary<string, object> configuration)` - Flexible JSON-compatible config
   - `TestConnectionAsync()` - Health check
   - Implements `IDisposable` for cleanup

2. **ISensorPlugin** - For reading sensor data (extends IHardwarePlugin)
   - `ReadValueAsync(string sensorId)` - Read single sensor
   - `ReadAllValuesAsync()` - Batch read all sensors

3. **IControlPlugin** - For actuating hardware (extends IHardwarePlugin)
   - `SetStateAsync(string controlId, object state)` - Set control state
   - `GetStateAsync(string controlId)` - Read current state

**Plugin Registration Pattern:**
```csharp
// In Program.cs - plugins registered as singletons
builder.Services.AddSingleton<ISensorPlugin, SimulatedSensorPlugin>();
builder.Services.AddSingleton<IControlPlugin, SimulatedControlPlugin>();

// After app build - initialize plugins with configuration
var sensorPlugins = app.Services.GetServices<ISensorPlugin>();
foreach (var plugin in sensorPlugins)
{
    await plugin.InitializeAsync(new Dictionary<string, object>());
}
```

**Available Plugins:**
- `Simulated` - Default testing/development plugins (generate realistic fake data)
- `Modbus` - Placeholder for Modbus TCP/RTU devices
- `I2C` - Placeholder for direct I2C sensor integration
- `Victron` - Placeholder for Victron Cerbo GX via MQTT

**Creating New Plugins:**
1. Reference `VanDaemon.Plugins.Abstractions`
2. Implement `ISensorPlugin` or `IControlPlugin`
3. Accept `ILogger<T>` via constructor injection
4. Store plugin state in private fields/dictionaries
5. Implement `IDisposable` for cleanup
6. Register in `Program.cs` as singleton
7. Initialize after app build with configuration dictionary

### Data Storage Strategy

**Two-tier storage model:**

1. **Configuration Data (Persistent)** - JSON files via `JsonFileStore`
   - Location: `{AppContext.BaseDirectory}/data/`
   - Files: `tanks.json`, `controls.json`, `alerts.json`, `settings.json`
   - Thread-safe with `SemaphoreSlim`
   - Human-readable (indented JSON)

2. **Real-Time Data (Volatile)** - In-memory collections
   - Tank levels, control states stored in service `List<T>` fields
   - Live sensor readings never persisted
   - Fast access, no database overhead

**Data Flow:**
- Startup: Services load configuration from JSON → in-memory collections
- Runtime: Services read live values from plugins (not persisted)
- Configuration changes: Services save updated config to JSON
- Real-time updates: Live data flows through SignalR (never persisted)

### SignalR Real-Time Communication

**TelemetryHub** (`/hubs/telemetry`):
- Group-based subscriptions: `tanks`, `controls`, `alerts`
- Client-callable methods: `SubscribeToTanks()`, `SubscribeToControls()`, `SubscribeToAlerts()`
- Server-to-client events: `TankLevelUpdated`, `ControlStateChanged`, `AlertsUpdated`

**Background Service** (`TelemetryBackgroundService`):
- Runs continuously for application lifetime
- Every 5 seconds (configurable via `VanDaemon:RefreshIntervalSeconds`):
  1. Calls `tankService.RefreshAllTankLevelsAsync()` to read all sensors
  2. Broadcasts tank updates via `TankLevelUpdated` to "tanks" group
  3. Calls `alertService.CheckTankAlertsAsync()` to check thresholds
  4. Broadcasts active alerts via `AlertsUpdated` to "alerts" group

**Important:** Background service uses `IServiceProvider.CreateScope()` to create scoped service instances per iteration (required because background services are singletons).

### Dependency Injection Patterns

**Singleton Lifetime:**
- `JsonFileStore` - File I/O coordination
- All plugins (`ISensorPlugin`, `IControlPlugin`) - Hardware connections
- All application services (`ITankService`, `IControlService`, etc.) - Maintain in-memory state
- `IHubContext<TelemetryHub>` - Server-side SignalR access

**Scoped Lifetime:**
- Controllers (automatically scoped by ASP.NET Core)

**Hosted Services:**
- `TelemetryBackgroundService` - Runs for application lifetime

## Key Domain Concepts

### Tank Entity
- Each tank has a `SensorPlugin` string (plugin name) and `SensorConfiguration` dictionary
- `AlertLevel` + `AlertWhenOver` define threshold behavior:
  - `AlertWhenOver=false`: Alert when CurrentLevel < AlertLevel (for consumables)
  - `AlertWhenOver=true`: Alert when CurrentLevel > AlertLevel (for waste)
- `IsActive` flag enables soft deletes
- `CurrentLevel` is percentage (0-100), `Capacity` is liters

### Control Entity
- `Type` enum: `Toggle` (on/off), `Momentary` (button), `Dimmer` (0-100), `Selector` (multi-choice)
- `State` is `object` type to support different control types (bool, int, string)
- Each control references a `ControlPlugin` and has `ControlConfiguration` dictionary
- `IconName` used by frontend for rendering

### Alert Entity
- Generated automatically by `AlertService.CheckTankAlertsAsync()`
- `Severity` enum: `Info`, `Warning`, `Error`, `Critical`
- Can be acknowledged by user (doesn't delete, just marks as seen)
- Cleared automatically when condition resolves

## Testing Patterns

**Framework:** xUnit with FluentAssertions and Moq

**Test Structure:**
- Arrange: Set up mocks and test data
- Act: Call method under test
- Assert: Verify behavior with FluentAssertions

**Common Patterns:**
```csharp
// Mock setup
var mockService = new Mock<ITankService>();
mockService.Setup(x => x.GetAllTanksAsync(It.IsAny<CancellationToken>()))
    .ReturnsAsync(testData);

// FluentAssertions
result.Should().NotBeNull();
result.Should().HaveCount(3);
result.Should().AllSatisfy(t => t.IsActive.Should().BeTrue());

// JsonFileStore testing - use temporary directory
var tempPath = Path.Combine(Path.GetTempPath(), $"vandaemon-tests-{Guid.NewGuid()}");
var fileStore = new JsonFileStore(loggerMock.Object, tempPath);
```

**Test Projects:**
- `VanDaemon.Api.Tests` - Controller and hub tests
- `VanDaemon.Application.Tests` - Service and plugin tests
- `VanDaemon.Infrastructure.Tests` - Data access tests (currently empty)

## Deployment

### Docker Deployment
- Two-container setup: `vandaemon-api` + `vandaemon-web` (nginx)
- Configuration files: `docker/Dockerfile.api`, `docker/Dockerfile.web`, `docker/docker-compose.yml`
- Nginx reverse proxy serves Blazor WASM and proxies API/WebSocket requests

### Fly.io Cloud Deployment
- Single-container combined deployment
- Nginx + .NET API managed by supervisord
- Configuration: `fly.toml`, `docker/Dockerfile.combined`, `docker/supervisord.conf`
- GitHub Actions auto-deployment: `.github/workflows/fly-deploy.yml`
- Health check endpoint: `/health`

### Raspberry Pi Deployment
- Use Docker Compose setup from `docker/` directory
- Enable I2C for hardware sensors: `sudo raspi-config → Interface Options → I2C`
- Systemd service for auto-start on boot (see `README.md`)
- User must be in `docker` and `i2c` groups

## Important Development Patterns

### Adding a New Tank Type
1. Add enum value to `VanDaemon.Core/Enums/TankType.cs`
2. No other code changes needed (dynamic configuration)

### Adding a New Service
1. Create interface in `VanDaemon.Application/Interfaces/I{Name}Service.cs`
2. Implement in `VanDaemon.Application/Services/{Name}Service.cs`
3. Accept dependencies via constructor injection
4. Register in `Program.cs`: `builder.Services.AddSingleton<I{Name}Service, {Name}Service>()`
5. Consider persistence needs (add JsonFileStore calls if needed)

### Adding a New API Endpoint
1. Create or extend controller in `VanDaemon.Api/Controllers/`
2. Inject service via constructor
3. Use `[HttpGet]`, `[HttpPost]`, etc. attributes
4. Return `ActionResult<T>` for proper status codes
5. Include `CancellationToken` parameter
6. No need to manually configure routing (uses `[ApiController]` and `[Route("api/[controller]")]`)

### Broadcasting SignalR Updates
```csharp
// Inject IHubContext<TelemetryHub>
await _hubContext.Clients.Group("tanks").SendAsync(
    "TankLevelUpdated",
    tankId,
    currentLevel,
    tankName,
    cancellationToken
);
```

## Configuration Files

### appsettings.json
- `VanDaemon:RefreshIntervalSeconds` - Background service polling interval (default: 5)
- `AllowedHosts` - CORS configuration
- `Logging` - Serilog configuration

### Environment Variables
- `.env.example` shows available configuration options
- Used primarily for deployment secrets (not during development)

## Common Gotchas

1. **Background Service Scope:** Always create a new scope when accessing services from `TelemetryBackgroundService` (services are singletons but may need scoped resources in the future)

2. **Plugin Initialization:** Plugins must be initialized after `app.Build()` but before `app.Run()` - initialization is async and happens in Program.cs

3. **JsonFileStore Thread Safety:** Always `await` JsonFileStore operations - concurrent access is protected by internal SemaphoreSlim

4. **SignalR Group Subscriptions:** Clients must explicitly call `SubscribeToTanks()` etc. before receiving group broadcasts

5. **Soft Deletes:** Use `IsActive = false` instead of removing entities from collections (preserves history and prevents null reference issues)

6. **Object State Type:** `Control.State` is `object` type - cast appropriately based on `ControlType` (bool for Toggle, int for Dimmer, etc.)

7. **Docker Networking:** In Docker Compose, services communicate via service names (e.g., `http://api:8080`) not localhost

## Project-Specific Conventions

- **Logging:** Use structured logging with message templates: `_logger.LogInformation("Tank {TankId} updated to {Level}%", tankId, level)`
- **Cancellation Tokens:** All async methods accept optional `CancellationToken` parameter (defaults to `default`)
- **Async Naming:** All async methods end with `Async` suffix
- **Null Handling:** Nullable reference types enabled (`<Nullable>enable</Nullable>`) - use `?` appropriately
- **Configuration Dictionaries:** Use `Dictionary<string, object>` for plugin configuration (JSON-serializable types only)
