# VanDaemon - Camper Van Control System Project Plan

**Last Updated**: 2025-11-21 | **Constitution**: v1.0.0 | **Status**: Phase 2 In Progress

> **Governance**: This plan is subject to the VanDaemon Constitution (`.specify/memory/constitution.md`) which supersedes this document for architectural and development standards.

## Executive Summary

VanDaemon is a comprehensive IoT solution for monitoring and controlling camper van systems. Built on **.NET 10** with Blazor WebAssembly frontend and containerized backend API, this system provides real-time monitoring, control, and alerting accessible via web browsers on any device with **offline-first operation** and **JSON-based configuration storage**.

## System Architecture

### Overview

```
┌─────────────────────────────────────────────────────────────┐
│                     Client Devices                           │
│           (Android, iOS, Desktop Browsers)                   │
└────────────────────────┬────────────────────────────────────┘
                         │ HTTPS/WebSocket
                         ▼
┌─────────────────────────────────────────────────────────────┐
│              Blazor WebAssembly Frontend                     │
│  - Interactive Van Diagram                                   │
│  - Real-time Status Display                                  │
│  - Control Interface                                         │
│  - Alert Notifications                                       │
└────────────────────────┬────────────────────────────────────┘
                         │ REST API / SignalR
                         ▼
┌─────────────────────────────────────────────────────────────┐
│              .NET 10 Backend API (Docker)                    │
│  ┌─────────────────────────────────────────────────────┐   │
│  │           API Layer (Controllers)                    │   │
│  └───────────────────────┬─────────────────────────────┘   │
│  ┌───────────────────────┴─────────────────────────────┐   │
│  │         Business Logic Layer (Services)              │   │
│  │  - Tank Service                                      │   │
│  │  - Control Service                                   │   │
│  │  - Alert Service                                     │   │
│  │  - Settings Service                                  │   │
│  └───────────────────────┬─────────────────────────────┘   │
│  ┌───────────────────────┴─────────────────────────────┐   │
│  │         Persistence Layer (JsonFileStore)            │   │
│  └───────────────────────┬─────────────────────────────┘   │
│  ┌───────────────────────┴─────────────────────────────┐   │
│  │   Hardware Abstraction Layer (Plugin System)        │   │
│  │  - Simulated Plugin (testing)                       │   │
│  │  - Modbus Plugin                                     │   │
│  │  - I2C Plugin                                        │   │
│  │  - Victron Cerbo Plugin                             │   │
│  └───────────────────────┬─────────────────────────────┘   │
└────────────────────────────┬────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────┐
│              Hardware/External Systems                       │
│  - Modbus Devices (TCP/RTU)                                 │
│  - I2C Sensors (GPIO)                                       │
│  - Victron Cerbo GX (MQTT)                                  │
│  - Simulated Hardware (development/testing)                 │
└─────────────────────────────────────────────────────────────┘
```

## Technology Stack

### Backend (REQUIRED)
- **Framework**: .NET 10 (REQUIRED - upgrade from current .NET 8 implementation)
- **API Style**: RESTful API with SignalR for real-time updates
- **Containerization**: Docker with multi-stage builds
- **Persistence**: JSON file storage via `JsonFileStore` (REQUIRED - located in `data/` directory)
  - Configuration data: Persisted to JSON (tanks.json, controls.json, alerts.json, settings.json)
  - Real-time data: In-memory collections for performance
  - Thread-safe: SemaphoreSlim for concurrent access
- **Logging**: Serilog with structured logging
- **Testing**: xUnit, Moq, FluentAssertions
- **Communication Protocols**:
  - Modbus (TCP/RTU) via NModbus
  - I2C via System.Device.Gpio
  - MQTT (for Victron integration) via MQTTnet
  - HTTP/REST APIs
  - SignalR WebSockets

### Frontend (REQUIRED)
- **Framework**: Blazor WebAssembly (.NET 10)
- **UI Components**: MudBlazor (Material Design)
- **State Management**: Direct SignalR subscriptions with component-level state
- **Real-time Communication**: SignalR client with automatic reconnection
- **Graphics**: SVG for van diagrams and overlays

### Infrastructure
- **Container Orchestration**: Docker Compose
- **Reverse Proxy**: Nginx (serves static Blazor files, proxies API/WebSocket)
- **CI/CD**: GitHub Actions (automated deployment to Fly.io)
- **Health Monitoring**: `/health` endpoint for container orchestration
- **Deployment Targets**:
  - Raspberry Pi 4 (local deployment)
  - Fly.io (cloud deployment for remote access)

## Project Structure

```
VanDaemon/
├── src/
│   ├── Backend/
│   │   ├── VanDaemon.Api/                    # Main API project, SignalR hubs
│   │   ├── VanDaemon.Core/                   # Domain entities, enums
│   │   ├── VanDaemon.Application/            # Services, business logic, JsonFileStore
│   │   ├── VanDaemon.Infrastructure/         # (Reserved for future SQLite migration)
│   │   └── VanDaemon.Plugins/                # Hardware integration plugins
│   │       ├── Abstractions/                 # IHardwarePlugin, ISensorPlugin, IControlPlugin
│   │       ├── Simulated/                    # Simulated hardware (for testing)
│   │       ├── Modbus/                       # Modbus integration (placeholder)
│   │       ├── I2C/                          # I2C sensor integration (placeholder)
│   │       └── Victron/                      # Victron Cerbo integration (placeholder)
│   └── Frontend/
│       └── VanDaemon.Web/                    # Blazor WebAssembly app
├── tests/
│   ├── VanDaemon.Api.Tests/
│   ├── VanDaemon.Application.Tests/
│   └── VanDaemon.Infrastructure.Tests/
├── docs/
│   ├── api/                                  # API reference documentation
│   │   └── api-reference.md
│   └── deployment/                           # Deployment guides
│       ├── ARCHITECTURE.md                   # Architecture deep-dive
│       ├── plugin-development.md             # Plugin development guide
│       ├── raspberry-pi-setup.md             # Raspberry Pi deployment
│       └── fly-io-deployment.md              # Cloud deployment guide
├── docker-compose.yml                        # Single authoritative stack (api + web + mqtt) — dev & Pi appliance
├── docker/
│   ├── Dockerfile.api                        # Backend API container
│   ├── Dockerfile.web                        # Frontend nginx container
│   ├── Dockerfile.combined                   # Fly.io single-container deployment
│   ├── mosquitto/                            # Mosquitto broker config (owned broker, ADR-001)
│   ├── nginx.conf                            # Nginx configuration
│   └── supervisord.conf                      # Process manager for combined container
├── .github/
│   └── workflows/
│       ├── build.yml                         # CI build and test
│       └── fly-deploy.yml                    # CD deployment to Fly.io
├── .specify/
│   ├── memory/
│   │   └── constitution.md                   # Project constitution v1.0.0
│   ├── templates/                            # Spec/plan/task templates
│   └── scripts/                              # PowerShell utilities
├── .claude/
│   └── commands/                             # Speckit commands (/speckit.*)
├── CLAUDE.md                                 # Development guidance for Claude Code
├── DEPLOYMENT.md                             # Deployment documentation
├── QUICK_START.md                            # Quick start guide
├── README.md                                 # Project overview
├── PROJECT_PLAN.md                           # This file
└── VanDaemon.sln                             # Solution file
```

## Phase Completion Status

### ✅ Phase 1: Foundation - COMPLETE
**Completed**: 2025-Q4 | **Constitution Compliance**: ✅ Verified

1. **Project Setup** ✅
   - ✅ Solution structure initialized
   - ✅ CI/CD pipeline (GitHub Actions → Fly.io)
   - ✅ Docker containers configured (API + Web + Combined)
   - ✅ Constitution established (v1.0.0)
   - ✅ Coding standards documented (CLAUDE.md)

2. **Core Backend Services** ✅
   - ✅ Tank monitoring service (ITankService, TankService)
   - ✅ Control service (IControlService, ControlService)
   - ✅ Settings service (ISettingsService, SettingsService)
   - ✅ Alert service (IAlertService, AlertService)
   - ✅ JsonFileStore persistence layer
   - ✅ Background monitoring service (TelemetryBackgroundService)

3. **Basic Frontend** ✅
   - ✅ Application shell with MudBlazor
   - ✅ Dashboard page with real-time tank display
   - ✅ Tanks page with detailed monitoring
   - ✅ Controls page with switches and dimmers
   - ✅ Settings page (van model selection, alert thresholds)
   - ✅ Connection status indicator (green badge when connected)

### 🚧 Phase 2: Hardware Integration - IN PROGRESS
**Started**: 2025-Q4 | **Constitution Compliance**: ⚠️ Partial

1. **Plugin Architecture** ✅
   - ✅ Plugin interfaces defined (IHardwarePlugin, ISensorPlugin, IControlPlugin)
   - ✅ Plugin loader implemented (DI registration in Program.cs)
   - ✅ Plugin configuration system (Dictionary<string, object>)
   - ✅ Simulated plugins for testing (SimulatedSensorPlugin, SimulatedControlPlugin)

2. **Initial Plugins** ⚠️
   - ✅ Simulated sensor plugin (working with realistic data generation)
   - ✅ Simulated control plugin (working with state management)
   - ❌ **Modbus plugin** (placeholder project only - no implementation)
   - ❌ **I2C plugin** (placeholder project only - no implementation)

   **Constitution Compliance Issue**: Principle IV requires simulated counterpart for each real plugin before implementation.

3. **Real-time Communication** ✅
   - ✅ SignalR hub (TelemetryHub at `/hubs/telemetry`)
   - ✅ Group-based subscriptions (tanks, controls, alerts)
   - ✅ Real-time data push (5-second polling with broadcast)
   - ✅ Connection resilience (auto-reconnect in frontend)
   - ✅ WebSocket support in Nginx

### ⏸️ Phase 3: User Interface Enhancement - PARTIALLY COMPLETE
**Status**: Dashboard complete, interactivity pending

1. **Interactive Van Diagram** ⚠️
   - ✅ SVG-based van image display (Mercedes Sprinter LWB)
   - ⚠️ Van type selection backend (Settings API has VanDiagram field)
   - ❌ **TODO**: Wire Settings API to Dashboard (Index.razor:256)
   - ❌ **Configurable overlays** (position tank/control indicators on diagram)
   - ❌ **Interactive controls** (click diagram elements to activate)
   - ❌ **Visual tank level indicators** (gauges, fill animations)
   - ❌ **System status icons** (battery, solar, connectivity)

2. **Control Interface** ✅
   - ✅ Touch-optimized MudBlazor controls
   - ✅ Switch toggles (lights, pump, heater)
   - ✅ Dimmer controls (slider with percentage)
   - ✅ Immediate visual feedback (<200ms)

3. **Alert System** ⚠️
   - ✅ Backend alert generation (AlertService.CheckTankAlertsAsync)
   - ✅ Alert API endpoints (GET, acknowledge, delete)
   - ✅ SignalR broadcast to "alerts" group
   - ❌ **Alert notification panel UI component**
   - ❌ **Alert history page**
   - ❌ **Alert sound/visual indicators**

   **Constitution Compliance Issue**: Principle II requires visual alerts for safety-critical events.

### ❌ Phase 4: Advanced Features - NOT STARTED
**Status**: Planned

1. **Additional Plugins** ❌
   - ❌ Victron Cerbo integration (MQTT-based)
   - ❌ Battery monitoring (voltage, current, SOC)
   - ❌ Fuel level monitoring
   - ❌ Solar panel monitoring

2. **Data Logging & History** ❌
   - ❌ Historical data storage (time-series)
   - ❌ Trend charts (tank levels over time)
   - ❌ Export functionality (CSV, JSON)
   - ❌ Data retention policies

3. **Multi-client Support** ⚠️
   - ✅ Concurrent client handling (SignalR supports multiple connections)
   - ✅ State synchronization (via SignalR broadcasts)
   - ❌ Client authentication (optional JWT-based auth)
   - ❌ User management
   - ❌ Role-based access control

### 🔮 Phase 5+: Future Enhancements - ROADMAP
**Status**: Planned for future iterations

1. Voice control integration (Alexa/Google Home)
2. GPS tracking and location-based features
3. Weather integration with forecasts
4. Maintenance scheduling and service reminders
5. Energy consumption analytics and optimization
6. Native mobile app (iOS/Android with .NET MAUI)
7. Multi-van management dashboard
8. Optional cloud synchronization (encrypted)

## Data Models

### Core Entities

#### Tank
```csharp
public class Tank
{
    Guid Id { get; set; }
    string Name { get; set; }
    TankType Type { get; set; }                    // FreshWater, WasteWater, LPG, Fuel
    double CurrentLevel { get; set; }              // Percentage (0-100)
    double Capacity { get; set; }                  // Liters
    double AlertLevel { get; set; }                // Threshold percentage
    bool AlertWhenOver { get; set; }               // false = low alert, true = high alert
    string SensorPlugin { get; set; }              // Plugin name (e.g., "Simulated Sensor Plugin")
    Dictionary<string, object> SensorConfiguration { get; set; }
    DateTime LastUpdated { get; set; }
    bool IsActive { get; set; }                    // Soft delete flag
}
```

#### Control
```csharp
public class Control
{
    Guid Id { get; set; }
    string Name { get; set; }
    ControlType Type { get; set; }                 // Toggle, Momentary, Dimmer, Selector
    object State { get; set; }                     // bool (Toggle), int (Dimmer), string (Selector)
    string ControlPlugin { get; set; }
    Dictionary<string, object> ControlConfiguration { get; set; }
    DateTime LastUpdated { get; set; }
    bool IsActive { get; set; }
    string IconName { get; set; }                  // Material Design icon name
}
```

#### Alert
```csharp
public class Alert
{
    Guid Id { get; set; }
    DateTime Timestamp { get; set; }
    AlertSeverity Severity { get; set; }           // Info, Warning, Error, Critical
    string Source { get; set; }                    // "Tank", "Control", "System"
    string Message { get; set; }
    bool Acknowledged { get; set; }
    DateTime? AcknowledgedAt { get; set; }
}
```

#### SystemSettings
```csharp
public class SystemSettings
{
    string VanModel { get; set; }
    string VanDiagram { get; set; }                // SVG file path
    double LowLevelThreshold { get; set; }         // Default 10%
    double HighLevelThreshold { get; set; }        // Default 90%
    bool EnableAlerts { get; set; }
    bool EnableAudioAlerts { get; set; }
    int RefreshIntervalSeconds { get; set; }       // Background service polling (default 5)
}
```

## API Endpoints

### Tanks
- `GET /api/tanks` - Get all active tanks
- `GET /api/tanks/{id}` - Get tank by ID
- `GET /api/tanks/{id}/level` - Get current tank level (triggers plugin read)
- `POST /api/tanks` - Create new tank
- `PUT /api/tanks/{id}` - Update tank configuration
- `DELETE /api/tanks/{id}` - Soft delete tank (sets IsActive = false)
- `POST /api/tanks/refresh` - Force refresh all tank levels

### Controls
- `GET /api/controls` - Get all active controls
- `GET /api/controls/{id}` - Get control by ID
- `POST /api/controls` - Create new control
- `POST /api/controls/{id}/state` - Set control state
- `PUT /api/controls/{id}` - Update control configuration
- `DELETE /api/controls/{id}` - Soft delete control

### Settings
- `GET /api/settings` - Get system settings
- `PUT /api/settings` - Update system settings
- `GET /api/settings/van-diagrams` - Get available van diagram options

### Alerts
- `GET /api/alerts` - Get alerts (with optional filtering: includeAcknowledged)
- `POST /api/alerts/check` - Manually trigger alert check
- `POST /api/alerts/{id}/acknowledge` - Acknowledge alert
- `DELETE /api/alerts/{id}` - Delete alert

### Health
- `GET /health` - Health check endpoint (returns status and timestamp)

### SignalR Hub
- **Hub**: `/hubs/telemetry` (TelemetryHub)
- **Client Methods**:
  - `SubscribeToTanks()` - Subscribe to tank updates
  - `SubscribeToControls()` - Subscribe to control updates
  - `SubscribeToAlerts()` - Subscribe to alert updates
- **Server Events**:
  - `TankLevelUpdated(Guid id, double level, string name)` - Tank level changed
  - `ControlStateChanged(Guid id, object state, string name)` - Control toggled
  - `AlertsUpdated(List<Alert> alerts)` - New alerts generated

## Plugin System

### IHardwarePlugin Interface
```csharp
public interface IHardwarePlugin : IDisposable
{
    string Name { get; }
    string Version { get; }
    Task InitializeAsync(Dictionary<string, object> configuration, CancellationToken cancellationToken = default);
    Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);
}
```

### ISensorPlugin Interface (extends IHardwarePlugin)
```csharp
public interface ISensorPlugin : IHardwarePlugin
{
    Task<double> ReadValueAsync(string sensorId, CancellationToken cancellationToken = default);
    Task<IDictionary<string, double>> ReadAllValuesAsync(CancellationToken cancellationToken = default);
}
```

### IControlPlugin Interface (extends IHardwarePlugin)
```csharp
public interface IControlPlugin : IHardwarePlugin
{
    Task<bool> SetStateAsync(string controlId, object state, CancellationToken cancellationToken = default);
    Task<object> GetStateAsync(string controlId, CancellationToken cancellationToken = default);
}
```

## Testing Strategy

### Unit Tests (Required by Constitution Principle IV)
- ✅ Service layer logic (TankService, ControlService, AlertService)
- ✅ Simulated plugin implementations
- ✅ Data validation and business rules
- ✅ Alert generation logic
- ❌ Controller tests (pending)

### Integration Tests (Planned)
- API endpoint testing (full request/response cycle)
- SignalR communication (hub subscriptions, broadcasts)
- Plugin loading and initialization
- JSON file persistence (concurrent access, data integrity)

### End-to-End Tests (Planned)
- Full user workflows (configure tank → read level → generate alert)
- Multi-client scenarios (state sync across browsers)
- Hardware simulation (simulated plugins with realistic behavior)

### Testing Tools
- **Framework**: xUnit
- **Mocking**: Moq
- **Assertions**: FluentAssertions
- **Coverage**: XPlat Code Coverage (via `dotnet test --collect:"XPlat Code Coverage"`)

## Deployment

### Controller hardware (decided — ADR-001)

The always-on headless **controller** is a **Raspberry Pi 5 (4 GB) booting from NVMe — never microSD** —
and it **hosts the Mosquitto MQTT broker locally**. This is a recorded decision
([ADR-001](docs/deployment/adr/ADR-001-controller-soc.md)), not a bare default. The near-zero-touch
flashable-image deployment of this controller is feature **006-pi-appliance-deploy** — see the operator
guide [docs/deployment/pi-appliance-setup.md](docs/deployment/pi-appliance-setup.md). The Pi 4 notes
below remain valid for a generic Docker-on-Pi install.

### Raspberry Pi Setup (Local Deployment)
1. **Prerequisites**
   - Raspberry Pi 4 (minimum 2GB RAM recommended)
   - Raspberry Pi OS (64-bit recommended)
   - Docker and Docker Compose V2 installed
   - I2C enabled (for sensor access): `sudo raspi-config → Interface Options → I2C`
   - User in `docker` and `i2c` groups

2. **Installation**
   ```bash
   # Clone repository
   git clone https://github.com/StuartF303/vandaemon.git
   cd vandaemon

   # Configure environment (optional)
   cp .env.example .env
   nano .env

   # Start services
   cd docker
   docker compose up -d

   # Verify status
   docker compose logs -f
   ```

3. **Access**
   - Web UI: http://raspberrypi.local:8080
   - API: http://raspberrypi.local:5000
   - Swagger: http://raspberrypi.local:5000/swagger

4. **Auto-Start on Boot**
   - Create systemd service (see `docs/deployment/raspberry-pi-setup.md`)
   - Enable with `sudo systemctl enable vandaemon`

### Fly.io Deployment (Cloud/Remote Access)
1. **Prerequisites**
   - Fly.io account and CLI installed
   - GitHub repository with Actions enabled

2. **Manual Deployment**
   ```bash
   flyctl auth login
   flyctl apps create vandaemon
   flyctl deploy
   ```

3. **Automatic Deployment**
   - GitHub Actions workflow (`.github/workflows/fly-deploy.yml`)
   - Triggers on push to `main` branch
   - Builds combined container with Nginx + API

4. **Access**
   - Web UI: https://vandaemon.fly.dev
   - API: https://vandaemon.fly.dev/api
   - Swagger: https://vandaemon.fly.dev/swagger
   - Health: https://vandaemon.fly.dev/health

See `DEPLOYMENT.md` and `docs/deployment/fly-io-deployment.md` for detailed instructions.

## Constitution Compliance Checklist

> All development MUST comply with VanDaemon Constitution v1.0.0 (`.specify/memory/constitution.md`)

### Core Principles
- ✅ **I. Plugin-First Hardware Abstraction**: All hardware uses plugin interfaces
- ⚠️ **II. Real-Time Reliability**: <500ms latency achieved, missing timeouts and fail-safe defaults
- ✅ **III. Offline-First & Local Storage**: JSON-based, no cloud dependencies
- ⚠️ **IV. Test-Driven Hardware Integration**: Simulated plugins exist, missing tests for Modbus/I2C/Victron
- ✅ **V. Clean Architecture**: Strict layer separation maintained

### Outstanding Constitution Requirements
1. **Safety & Fail-Safe Mechanisms** (Architecture Requirements)
   - ❌ Fail-safe defaults on hardware connection loss (pump OFF, heater OFF)
   - ❌ Critical alerts for hardware failures
   - ❌ Control state read-back verification
   - ❌ 5-second timeout on control operations

2. **Testing Gates** (Development Workflow)
   - ⚠️ Controller tests missing
   - ⚠️ Integration tests not implemented

3. **Plugin Simulated Counterparts** (Principle IV)
   - ❌ Simulated Modbus plugin (required before implementing real Modbus)
   - ❌ Simulated I2C plugin (required before implementing real I2C)
   - ❌ Simulated Victron plugin (required before implementing real Victron)

## Performance Requirements

**Constitution Principle II mandates:**
- **Control Response Time**: < 200ms for visual feedback (optimistic UI updates)
- **Real-time Updates**: < 500ms end-to-end latency for safety-critical controls
- **SignalR Broadcast**: < 100ms from hardware event to client notification
- **Background Refresh**: 5 seconds default (configurable minimum 1s)

**Additional targets:**
- **Concurrent Clients**: Support 5+ simultaneous browser connections
- **CPU Usage**: < 30% average on Raspberry Pi 4
- **Memory**: < 500MB total (API + Web containers)
- **WebSocket Reconnection**: Exponential backoff (1s, 2s, 4s, 8s, max 30s)

## Security Considerations

1. **Network Security**
   - HTTPS for production (Let's Encrypt on Raspberry Pi)
   - Fly.io automatic HTTPS
   - CORS configured for local network access
   - Optional JWT authentication (planned Phase 4)

2. **Hardware Access**
   - Plugin isolation (separate processes/containers planned)
   - GPIO/I2C permission management (user groups)
   - Fail-safe mechanisms (Constitution Architecture Requirements)

3. **Data Privacy**
   - Local data storage (JSON files in `data/` directory)
   - No cloud dependencies in core functionality
   - Optional cloud sync (encrypted, user-controlled)

## Monitoring and Logging

- **Application Logging**: Serilog with structured logging
  - File: `logs/vandaemon-{Date}.txt` (rolling daily)
  - Console: Structured JSON in Docker logs
- **Health Checks**: `/health` endpoint (returns 200 OK with timestamp)
- **SignalR Connection Status**: Frontend displays green badge when connected
- **Metrics**: Prometheus endpoint (planned Phase 4)

## Risk Assessment

| Risk | Impact | Likelihood | Mitigation |
|------|--------|------------|------------|
| Hardware compatibility issues | High | Medium | Extensive plugin testing, simulation mode, Constitution Principle IV |
| Network connectivity issues | Medium | High | Offline-first design, local caching, Constitution Principle III |
| Performance on Raspberry Pi | High | Medium | Performance testing, .NET 10 optimization, <500MB memory target |
| Plugin configuration complexity | Medium | Medium | Configuration wizard (planned), presets, documentation |
| Real-time communication failures | High | Low | Automatic reconnection, exponential backoff, Constitution Principle II |
| Constitution compliance drift | Medium | Medium | Quarterly reviews, PR compliance checks, constitution gates in phases |
| .NET 10 upgrade compatibility | Medium | Low | Incremental upgrade, testing on all platforms before production |

## Success Criteria

1. ✅ Successfully deploy on Raspberry Pi 4
2. ✅ Real-time monitoring with < 500ms latency (Constitution compliance)
3. ⏸️ Support for at least 3 different hardware integrations (Simulated ✅, Modbus ❌, I2C ❌, Victron ❌)
4. ⏸️ 95% uptime over 30-day period (pending long-term testing)
5. ✅ Responsive UI on mobile devices
6. ✅ Comprehensive documentation (CLAUDE.md, README.md, DEPLOYMENT.md, constitution)
7. ✅ Constitution v1.0.0 ratified and enforced

## Development Milestones

### Milestone 1: Foundation (COMPLETED - 2025-Q4)
- ✅ Project structure and CI/CD
- ✅ Core services with JSON persistence
- ✅ Basic UI with real-time updates
- ✅ Docker containerization
- ✅ Constitution established

### Milestone 2: Hardware Integration (IN PROGRESS)
- ✅ Plugin architecture and simulated plugins
- 🚧 Modbus plugin implementation (next priority)
- 🚧 I2C plugin implementation
- ⏸️ Victron plugin implementation

### Milestone 3: UI Enhancement (PARTIALLY COMPLETE)
- ✅ Control interface completed
- 🚧 Settings API integration to Dashboard (TODO: Index.razor:256)
- ⏸️ Interactive SVG diagram with overlays
- ⏸️ Alert notification panel

### Milestone 4: Constitution Compliance (IN PROGRESS)
- ⏸️ Fail-safe mechanisms
- ⏸️ Control operation timeouts
- ⏸️ Hardware failure alerts
- ⏸️ Controller and integration tests

### Milestone 5: .NET 10 Upgrade (PLANNED)
- ⏸️ Upgrade all projects to .NET 10
- ⏸️ Test on Raspberry Pi and Fly.io
- ⏸️ Update Docker images
- ⏸️ Verify SignalR compatibility

### Milestone 6: Production Hardening (PLANNED)
- ⏸️ Historical data logging
- ⏸️ Prometheus metrics
- ⏸️ Authentication and authorization
- ⏸️ 30-day uptime validation

## Resources Required

- **Development**: 1 developer (AI-assisted with Claude Code)
- **Testing Hardware**:
  - Raspberry Pi 4 (2GB+ RAM)
  - Sample Modbus device (RS485 adapter + sensor)
  - I2C sensors (temperature, humidity, analog-to-digital converters)
  - Victron Cerbo GX or simulated MQTT broker
- **Cloud Services**: Fly.io free tier (1 shared-cpu-1x, 256MB)
- **Documentation**: Maintained via Claude Code + manual review

## Related Documentation

- **Constitution**: `.specify/memory/constitution.md` - Governance and architectural standards
- **Development Guide**: `CLAUDE.md` - Architecture patterns, build commands, common gotchas
- **Quick Start**: `QUICK_START.md` - Getting started guide
- **Deployment**: `DEPLOYMENT.md` - Deployment procedures
- **API Reference**: `docs/api/api-reference.md` - REST API documentation
- **Architecture Deep-Dive**: `docs/deployment/ARCHITECTURE.md` - Detailed architecture
- **Plugin Development**: `docs/deployment/plugin-development.md` - Creating custom plugins
- **Controller decision**: `docs/deployment/adr/ADR-001-controller-soc.md` - Pi 5 / NVMe / hosted broker (ADR-001)
- **Pi-5 appliance**: `docs/deployment/pi-appliance-setup.md` - Headless flashable-image controller deployment (feature 006)

## Conclusion

VanDaemon provides a robust, extensible IoT platform for camper van control systems with:
- **Constitution-driven development** ensuring quality and maintainability
- **Plugin-first architecture** for hardware flexibility
- **Offline-first design** for reliability in remote locations
- **JSON-based persistence** for simplicity and portability
- **.NET 10 requirement** for modern performance and features
- **Real-time updates** via SignalR with <500ms latency
- **Docker containerization** for easy deployment

The phased approach allows incremental delivery while maintaining constitution compliance at each milestone.

---

**Next Actions**:
1. Review and update constitution if needed (`/speckit.constitution`)
2. Create feature specifications for upcoming work (`/speckit.specify`)
3. Upgrade to .NET 10 (currently on .NET 8)
4. Implement constitution compliance (fail-safe mechanisms, timeouts)
5. Complete Modbus plugin with simulated counterpart
