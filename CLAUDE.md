# VanDaemon - Camper Van Control System

VanDaemon is an IoT control system for camper vans built with .NET 10, Blazor WebAssembly, and SignalR. It monitors and controls van systems (water tanks, LPG, lighting, heating, electrical) through a modular plugin architecture that supports multiple hardware integration methods including MQTT-based LED dimmers, Modbus devices, and Victron Cerbo GX. The system features a touch-friendly dashboard with draggable overlays, real-time sensor updates, and configurable alerts.

## Tech Stack

| Layer | Technology | Version | Purpose |
|-------|------------|---------|---------|
| Runtime | .NET | 10.0 | Cross-platform backend and frontend |
| Backend | ASP.NET Core Web API | 10.x | REST API with SignalR real-time |
| Frontend | Blazor WebAssembly | 10.x | SPA with offline capability |
| UI Components | MudBlazor | 6.x | Material Design components |
| Real-time | SignalR | 10.x | WebSocket communication |
| Logging | Serilog | 8.x | Structured logging to console/file |
| Testing | xUnit + FluentAssertions + Moq | 2.6/6.12/4.20 | Unit and integration tests |
| E2E Testing | Playwright | 1.49.x | Browser automation tests |
| MQTT | MQTTnet | 4.3.x | LED dimmer communication |
| Modbus | FluentModbus | 5.x | Modbus TCP/RTU communication |

## Quick Start

```bash
# Prerequisites: .NET 10.0 SDK, Docker (optional)

# Clone and build
git clone <repo>
cd vandaemon
dotnet build VanDaemon.sln

# Run (two terminals)
# Terminal 1 - API
cd src/Backend/VanDaemon.Api && dotnet run
# API: http://localhost:5000, Swagger: http://localhost:5000/swagger

# Terminal 2 - Web UI
cd src/Frontend/VanDaemon.Web && dotnet run
# Web UI: http://localhost:5001

# Run tests
dotnet test VanDaemon.sln

# Run E2E tests (Windows PowerShell)
./run-e2e-tests.ps1

# Docker deployment
docker compose up -d
# Web UI: http://localhost:8080, API: http://localhost:5000
```

## Project Structure

```
vandaemon/
├── src/
│   ├── Backend/
│   │   ├── VanDaemon.Api/           # REST API, SignalR hub, background services
│   │   ├── VanDaemon.Core/          # Domain entities and enums
│   │   ├── VanDaemon.Application/   # Services, interfaces, JSON persistence
│   │   ├── VanDaemon.Infrastructure/# Future SQLite (currently empty)
│   │   └── VanDaemon.Plugins/       # Hardware integration plugins
│   │       ├── Abstractions/        # IHardwarePlugin, ISensorPlugin, IControlPlugin
│   │       ├── Simulated/           # Development/testing plugins
│   │       ├── Modbus/              # Modbus TCP/RTU devices
│   │       ├── I2C/                 # Direct I2C sensors
│   │       ├── Victron/             # Cerbo GX via MQTT
│   │       └── MqttLedDimmer/       # ESP32 LED dimmer control
│   └── Frontend/
│       └── VanDaemon.Web/           # Blazor WASM application
│           └── Pages/               # Dashboard, Tanks, Controls, Devices, Settings
├── tests/
│   ├── VanDaemon.Api.Tests/
│   ├── VanDaemon.Application.Tests/
│   ├── VanDaemon.Plugins.Modbus.Tests/
│   └── VanDaemon.E2E.Tests/         # Playwright browser tests
├── hw/
│   └── LEDDimmer/                   # ESP32 8-channel PWM LED dimmer (KiCad + Arduino)
├── docker/                          # Dockerfile.api, Dockerfile.web, Dockerfile.combined
├── tools/
│   └── CerboGXTest/                 # Victron MQTT test tool
└── docs/
```

## Architecture

VanDaemon follows **Clean Architecture** with these layers:

```
┌─────────────────────────────────────────────────────────┐
│                    Frontend (Blazor WASM)               │
│  Pages: Dashboard, Tanks, Controls, Devices, Settings   │
│  SignalR client for real-time updates                   │
└─────────────────────────────┬───────────────────────────┘
                              │ HTTP/WebSocket
┌─────────────────────────────▼───────────────────────────┐
│                      API Layer                          │
│  Controllers (thin), TelemetryHub, BackgroundService    │
└─────────────────────────────┬───────────────────────────┘
                              │
┌─────────────────────────────▼───────────────────────────┐
│                  Application Layer                      │
│  Services: Tank, Control, Alert, Settings, Electrical   │
│  JsonFileStore for configuration persistence            │
└─────────────┬───────────────────────────┬───────────────┘
              │                           │
┌─────────────▼─────────────┐ ┌───────────▼───────────────┐
│       Core Layer          │ │     Plugin System         │
│  Entities, Enums          │ │  Simulated, Modbus,       │
│  No external dependencies │ │  MqttLedDimmer, Victron   │
└───────────────────────────┘ └───────────────────────────┘
```

### Key Domain Entities

| Entity | Location | Purpose |
|--------|----------|---------|
| `Tank` | Core/Entities | Water, waste, LPG, fuel monitoring with alert thresholds |
| `Control` | Core/Entities | Switches, dimmers, momentary buttons |
| `Alert` | Core/Entities | System alerts with severity levels (Info, Warning, Error, Critical) |
| `SystemConfiguration` | Core/Entities | Van settings, theme, toolbar position, driving side |
| `ElectricalDevice` | Core/Entities | Electrical system components |
| `ElectricalSystem` | Core/Entities | Overall electrical system state |

### Application Services

| Service | Interface | Purpose |
|---------|-----------|---------|
| `TankService` | `ITankService` | Tank CRUD, level monitoring, sensor integration |
| `ControlService` | `IControlService` | Control state management, plugin coordination |
| `AlertService` | `IAlertService` | Alert generation, acknowledgment, clearing |
| `SettingsService` | `ISettingsService` | System configuration persistence |
| `ElectricalService` | `IElectricalService` | Electrical system monitoring |
| `ElectricalDeviceService` | `IElectricalDeviceService` | Device management |
| `UnifiedConfigService` | `IUnifiedConfigService` | Combined configuration management |

### Plugin System

Plugins implement `ISensorPlugin` (reading) or `IControlPlugin` (actuating):

```csharp
// Plugin registration in Program.cs
builder.Services.AddSingleton<ISensorPlugin, SimulatedSensorPlugin>();
builder.Services.AddSingleton<IControlPlugin, ModbusControlPlugin>();
builder.Services.AddSingleton<MqttLedDimmerPlugin>();
builder.Services.AddSingleton<IControlPlugin>(sp => sp.GetRequiredService<MqttLedDimmerPlugin>());

// Initialize after app.Build()
var plugins = app.Services.GetServices<IControlPlugin>();
foreach (var plugin in plugins)
    await plugin.InitializeAsync(config);
```

**Available Plugins:**
- `Simulated` - Generates realistic fake data for development/testing
- `Modbus` - Industrial Modbus TCP/RTU protocol (fully implemented)
- `MqttLedDimmer` - ESP32-based 8-channel PWM LED controller via MQTT
- `Victron` - Cerbo GX integration via MQTT (placeholder)
- `I2C` - Direct I2C sensor integration (placeholder)

**Creating New Plugins:**
1. Reference `VanDaemon.Plugins.Abstractions`
2. Implement `ISensorPlugin` or `IControlPlugin`
3. Accept `ILogger<T>` via constructor injection
4. Store plugin state in private fields/dictionaries
5. Implement `IDisposable` for cleanup
6. Register in `Program.cs` as singleton
7. Initialize after `app.Build()` with configuration dictionary

### SignalR Real-Time

**Hub:** `/hubs/telemetry`
**Groups:** `tanks`, `controls`, `alerts`, `electrical`
**Client Methods:** `SubscribeToTanks()`, `SubscribeToControls()`, `SubscribeToAlerts()`, `SubscribeToElectrical()`
**Server Events:** `TankLevelUpdated`, `ControlStateChanged`, `AlertsUpdated`

Background service (`TelemetryBackgroundService`) polls sensors every 5 seconds (configurable via `VanDaemon:RefreshIntervalSeconds`).

### Data Storage

**Two-tier storage model:**

1. **Configuration (Persistent)** - JSON files via `JsonFileStore`
   - Location: `{AppContext.BaseDirectory}/data/`
   - Files: `tanks.json`, `controls.json`, `alerts.json`, `settings.json`
   - Thread-safe with `SemaphoreSlim`

2. **Real-time Data (Volatile)** - In-memory only
   - Tank levels, control states stored in service `List<T>` fields
   - Live sensor readings never persisted

## Development

### Commands

| Command | Description |
|---------|-------------|
| `dotnet build VanDaemon.sln` | Build solution |
| `dotnet build --configuration Release` | Release build |
| `dotnet clean VanDaemon.sln` | Clean build artifacts |
| `dotnet test VanDaemon.sln` | Run all tests |
| `dotnet test --verbosity normal` | Tests with output |
| `dotnet test --collect:"XPlat Code Coverage"` | Tests with coverage |
| `./run-e2e-tests.ps1` | E2E tests (starts API+Web) |
| `./run-e2e-tests.ps1 -Headless $false -SlowMo 500` | E2E with visible browser |
| `docker compose up -d` | Start Docker containers |
| `docker compose logs -f` | View container logs |

### Port Configuration

| Environment | API | Web UI | SignalR |
|-------------|-----|--------|---------|
| Development | 5000 | 5001 | ws://localhost:5000/hubs/telemetry |
| Docker | 5000 | 8080 | ws://localhost:5000/hubs/telemetry |

**Important:** Frontend loads API URL from `wwwroot/appsettings.json` in development, uses same-origin in production.

### Adding a New Service

1. Create interface: `Application/Interfaces/I{Name}Service.cs`
2. Implement: `Application/Services/{Name}Service.cs`
3. Accept dependencies via constructor injection
4. Register in `Program.cs`: `builder.Services.AddSingleton<I{Name}Service, {Name}Service>()`
5. Consider persistence needs (add JsonFileStore calls if needed)

### Adding a New API Endpoint

1. Create/extend controller in `Api/Controllers/`
2. Inject service via constructor
3. Use `[HttpGet]`, `[HttpPost]` attributes
4. Return `ActionResult<T>`, accept `CancellationToken`
5. Routing is automatic via `[ApiController]` and `[Route("api/[controller]")]`

### Adding a New Tank Type

1. Add enum value to `VanDaemon.Core/Enums/TankType.cs`
2. No other code changes needed (dynamic configuration)

### Broadcasting SignalR Updates

```csharp
await _hubContext.Clients.Group("tanks").SendAsync(
    "TankLevelUpdated", tankId, currentLevel, tankName, cancellationToken);
```

## Testing

**Framework:** xUnit + FluentAssertions + Moq

```csharp
// Example test pattern
var mockService = new Mock<ITankService>();
mockService.Setup(x => x.GetAllTanksAsync(It.IsAny<CancellationToken>()))
    .ReturnsAsync(testData);

result.Should().NotBeNull();
result.Should().HaveCount(3);
result.Should().AllSatisfy(t => t.IsActive.Should().BeTrue());

// JsonFileStore testing - use temporary directory
var tempPath = Path.Combine(Path.GetTempPath(), $"vandaemon-tests-{Guid.NewGuid()}");
var fileStore = new JsonFileStore(loggerMock.Object, tempPath);
```

**Test Projects:**
- `VanDaemon.Api.Tests` - Controller and hub tests
- `VanDaemon.Application.Tests` - Service and JsonFileStore tests
- `VanDaemon.Plugins.Modbus.Tests` - Modbus plugin tests
- `VanDaemon.E2E.Tests` - Playwright browser automation

## Configuration

### appsettings.json

```json
{
  "VanDaemon": {
    "RefreshIntervalSeconds": 5,
    "EnableSimulatedPlugins": true
  },
  "MqttLedDimmer": {
    "MqttBroker": "localhost",
    "MqttPort": 1883,
    "BaseTopic": "vandaemon/leddimmer",
    "AutoDiscovery": true
  }
}
```

### Environment Variables (see .env.example)

| Variable | Description |
|----------|-------------|
| `ASPNETCORE_ENVIRONMENT` | Production/Development |
| `API_PORT`, `WEB_PORT` | Network ports (5000, 8080) |
| `DEFAULT_SENSOR_PLUGIN`, `DEFAULT_CONTROL_PLUGIN` | Plugin selection |
| `MODBUS_IP_ADDRESS`, `MODBUS_PORT` | Modbus connection |
| `VICTRON_MQTT_BROKER`, `VICTRON_DEVICE_ID` | Victron config |

## Deployment

### Docker (Recommended)

```bash
docker compose up -d
# Web: http://localhost:8080, API: http://localhost:5000
```

Two-container setup: `vandaemon-api` + `vandaemon-web` (nginx)

### Fly.io Cloud

```bash
flyctl deploy
# Single container with nginx + .NET API
```

Auto-deployment via `.github/workflows/deploy-fly.yml`

### Raspberry Pi

1. Install Docker: `curl -fsSL https://get.docker.com | sh`
2. Clone repo and run `docker compose up -d`
3. Enable I2C: `sudo raspi-config → Interface Options → I2C`
4. Add systemd service for auto-start (see README.md)

## Hardware Subprojects

### hw/LEDDimmer

ESP32-based 8-channel PWM LED controller with MQTT communication:

- **Firmware:** Arduino/PlatformIO (`led_dimmer.ino`)
- **PCB Design:** KiCad (see `HARDWARE_V2_DESIGN.md`, `PCB_LAYOUT_GUIDE.md`)
- **Build:** `pio run -e 8ch -t upload`
- **MQTT Topics:** `vandaemon/leddimmer/{deviceId}/channel/{N}/set`

**Note:** KiCad doesn't support semicolon comments

See `hw/LEDDimmer/README.md` for full documentation.

## Conventions

### Naming

- **Files:** PascalCase for C# files (`TankService.cs`, `Tank.cs`)
- **Classes/Interfaces:** PascalCase, interfaces prefixed with `I` (`ITankService`)
- **Methods:** PascalCase, async suffix for async methods (`GetAllTanksAsync`)
- **Variables:** camelCase for locals and parameters (`tankLevel`), `_camelCase` for private fields (`_logger`)
- **Properties:** PascalCase (`CurrentLevel`, `IsActive`)

### Code Style

- Nullable reference types enabled (`<Nullable>enable</Nullable>`)
- All async methods accept optional `CancellationToken` parameter
- Structured logging: `_logger.LogInformation("Tank {TankId} updated to {Level}%", tankId, level)`
- Soft deletes: Use `IsActive = false` instead of removing entities
- Plugin config: `Dictionary<string, object>` (JSON-serializable types only)
- JSON enums serialized as strings via `JsonStringEnumConverter`

### Dependency Injection

- **Singletons:** All services, plugins, `JsonFileStore`, `TelemetryService`, `SettingsStateService`, `IHubContext<TelemetryHub>`
- **Scoped:** Controllers (automatic by ASP.NET Core)
- **Hosted Services:** `TelemetryBackgroundService` (sensor polling), `MqttLedDimmerService` (MQTT device discovery)

## Common Issues

1. **Background Service Scope:** Create new scope when accessing services from background services (they're singletons)

2. **Plugin Initialization:** Must happen after `app.Build()` but before `app.Run()` in Program.cs

3. **JsonFileStore Thread Safety:** Always `await` operations - concurrent access protected by `SemaphoreSlim`

4. **SignalR Subscriptions:** Clients must call `SubscribeToTanks()` etc. before receiving group broadcasts

5. **CORS in Development:** Frontend (5001) → API (5000) requires explicit CORS config (handled in Program.cs)

6. **Docker Networking:** Services use container names (`http://api:80`) not localhost

7. **Control.State Type:** Cast based on `ControlType` (bool for Toggle, int for Dimmer 0-255)

8. **Alert Thresholds:** `AlertWhenOver=false` alerts when level drops below threshold (consumables), `=true` when above (waste)

## Key API Endpoints

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/tanks` | GET | List all active tanks |
| `/api/tanks/{id}` | GET/PUT/DELETE | Tank CRUD operations |
| `/api/tanks/{id}/level` | GET | Current tank level |
| `/api/tanks/refresh` | POST | Refresh all tank levels from sensors |
| `/api/controls` | GET | List all active controls |
| `/api/controls/{id}/state` | POST | Set control state |
| `/api/electrical` | GET | Electrical system state |
| `/api/settings` | GET/PUT | System configuration |
| `/api/settings/overlay-positions` | GET/POST | Dashboard overlay positions |
| `/health` | GET | Health check (returns status + timestamp) |

## Additional Resources

- @README.md - Full project documentation with troubleshooting
- @PROJECT_PLAN.md - Development roadmap
- @DEPLOYMENT.md - Fly.io deployment guide
- @DOCKER.md - Docker configuration details
- @hw/LEDDimmer/README.md - LED dimmer hardware and MQTT integration


## Skill Usage Guide

When working on tasks involving these technologies, invoke the corresponding skill:

| Skill | Invoke When |
|-------|-------------|
| dotnet | Configures .NET 10 projects, builds, and manages C# runtime |
| moq | Creates mock objects and configures test dependencies with Moq |
| xunit | Writes unit tests and integration tests with xUnit framework |
| playwright | Automates browser testing and E2E test scenarios with Playwright |
| aspnet-core | Builds REST APIs, SignalR hubs, and ASP.NET Core Web API applications |
| mudblazor | Creates Material Design UI components and touch-optimized interfaces |
| blazor | Develops Blazor WebAssembly single-page applications with component-based UI |
| csharp | Writes C# code following VanDaemon conventions and clean architecture patterns |
| signalr | Implements real-time WebSocket communication and SignalR hub subscriptions |
| fluent-assertions | Writes fluent, readable test assertions and validations |
| serilog | Implements structured logging and configures Serilog sinks |
| platformio | Builds and deploys ESP32 firmware with PlatformIO |
| mqttnet | Manages MQTT broker connections and message publishing/subscription |
| kicad | Designs PCB schematics and circuit layouts for hardware projects |
| docker | Configures containerization with Docker and Docker Compose |
| frontend-design | Applies UI design with Material Design components and SVG diagrams |
