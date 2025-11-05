# Camper Van Control System - Project Plan

## Executive Summary

The Camper Van Control System is a comprehensive IoT solution for monitoring and controlling various systems within a camper van. Built on .NET 10 with Blazor for the frontend and a containerized backend API, this system provides real-time monitoring, control, and alerting capabilities accessible via web browsers on any device.

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
│  │  - Tank Monitoring Service                           │   │
│  │  - Switch Control Service                            │   │
│  │  - Alert Service                                     │   │
│  │  - Settings Service                                  │   │
│  └───────────────────────┬─────────────────────────────┘   │
│  ┌───────────────────────┴─────────────────────────────┐   │
│  │         Data Access Layer (Repositories)             │   │
│  └───────────────────────┬─────────────────────────────┘   │
│  ┌───────────────────────┴─────────────────────────────┐   │
│  │   Hardware Abstraction Layer (Plugin System)        │   │
│  │  - Modbus Plugin                                     │   │
│  │  - I2C Plugin                                        │   │
│  │  - Victron Cerbo Plugin                             │   │
│  │  - Direct Sensor Plugin                             │   │
│  └───────────────────────┬─────────────────────────────┘   │
└────────────────────────────┬────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────┐
│              Hardware/External Systems                       │
│  - Modbus Devices                                           │
│  - I2C Sensors                                              │
│  - Victron Cerbo GX                                         │
│  - Direct GPIO Sensors                                      │
└─────────────────────────────────────────────────────────────┘
```

## Technology Stack

### Backend
- **Framework**: .NET 10
- **API Style**: RESTful API with SignalR for real-time updates
- **Containerization**: Docker
- **Database**: SQLite (for settings/configuration), In-memory cache for real-time data
- **Testing**: xUnit, Moq, FluentAssertions
- **Communication Protocols**:
  - Modbus (TCP/RTU)
  - I2C
  - MQTT (for Victron integration)
  - REST APIs

### Frontend
- **Framework**: Blazor WebAssembly
- **UI Components**: MudBlazor or Radzen (for rich UI components)
- **State Management**: Fluxor (Redux pattern for Blazor)
- **Real-time Communication**: SignalR client
- **SVG/Canvas**: For interactive van diagrams

### Infrastructure
- **Container Orchestration**: Docker Compose
- **Reverse Proxy**: Nginx (for production)
- **CI/CD**: GitHub Actions
- **Code Quality**: SonarQube, StyleCop

## Project Structure

```
VanDaemon/
├── src/
│   ├── Backend/
│   │   ├── VanDaemon.Api/                    # Main API project
│   │   ├── VanDaemon.Core/                   # Domain models, interfaces
│   │   ├── VanDaemon.Application/            # Business logic, services
│   │   ├── VanDaemon.Infrastructure/         # Data access, external services
│   │   └── VanDaemon.Plugins/                # Hardware integration plugins
│   │       ├── VanDaemon.Plugins.Modbus/
│   │       ├── VanDaemon.Plugins.I2C/
│   │       ├── VanDaemon.Plugins.Victron/
│   │       └── VanDaemon.Plugins.DirectSensor/
│   └── Frontend/
│       └── VanDaemon.Web/                    # Blazor WebAssembly app
├── tests/
│   ├── VanDaemon.Api.Tests/
│   ├── VanDaemon.Application.Tests/
│   ├── VanDaemon.Infrastructure.Tests/
│   └── VanDaemon.Web.Tests/
├── docs/
│   ├── architecture/
│   ├── api/
│   ├── deployment/
│   └── user-guide/
├── docker/
│   ├── Dockerfile.api
│   ├── Dockerfile.web
│   └── docker-compose.yml
├── .github/
│   └── workflows/
│       ├── build.yml
│       └── deploy.yml
└── README.md
```

## Core Features

### Phase 1: Foundation (Weeks 1-2)
1. **Project Setup**
   - Initialize solution structure
   - Set up CI/CD pipeline
   - Configure Docker containers
   - Establish coding standards

2. **Core Backend Services**
   - Tank monitoring service (abstract)
   - Switch control service (abstract)
   - Settings service with persistence
   - Alert/notification service

3. **Basic Frontend**
   - Application shell
   - Settings page
   - Basic van diagram display
   - Connection status indicator

### Phase 2: Hardware Integration (Weeks 3-4)
1. **Plugin Architecture**
   - Define plugin interfaces
   - Implement plugin loader
   - Create plugin configuration system

2. **Initial Plugins**
   - Simulated sensor plugin (for testing)
   - Modbus plugin
   - Direct GPIO/I2C plugin

3. **Real-time Communication**
   - SignalR hub implementation
   - Real-time data push to clients
   - Connection resilience

### Phase 3: User Interface Enhancement (Weeks 5-6)
1. **Interactive Van Diagram**
   - SVG-based van templates
   - Configurable van type selection
   - Interactive control overlays
   - Tank level indicators
   - System status icons

2. **Control Interface**
   - Touch-optimized controls
   - Switch toggles
   - Dimmer controls (for lighting)
   - Pump controls

3. **Alert System**
   - Visual alerts on UI
   - Configurable thresholds
   - Alert history

### Phase 4: Advanced Features (Weeks 7-8)
1. **Additional Plugins**
   - Victron Cerbo integration
   - Battery monitoring
   - Fuel level monitoring

2. **Data Logging & History**
   - Historical data storage
   - Trend charts
   - Export functionality

3. **Multi-client Support**
   - Concurrent client handling
   - State synchronization
   - Client authentication (optional)

## Data Models

### Core Entities

#### Tank
```csharp
- Id: Guid
- Name: string
- Type: TankType (FreshWater, WasteWater, LPG, Fuel)
- CurrentLevel: double (0-100%)
- Capacity: double (liters)
- LowLevelThreshold: double (default 10%)
- HighLevelThreshold: double (default 90%)
- SensorPlugin: string
- SensorConfiguration: Dictionary<string, object>
```

#### Switch/Control
```csharp
- Id: Guid
- Name: string
- Type: ControlType (Toggle, Momentary, Dimmer)
- State: bool/int
- ControlPlugin: string
- ControlConfiguration: Dictionary<string, object>
```

#### Alert
```csharp
- Id: Guid
- Timestamp: DateTime
- Severity: AlertSeverity (Info, Warning, Critical)
- Source: string
- Message: string
- Acknowledged: bool
```

#### SystemConfiguration
```csharp
- VanModel: string
- VanDiagram: string (SVG path)
- AlertSettings: AlertSettings
- PluginConfigurations: List<PluginConfiguration>
```

## API Endpoints

### Tanks
- `GET /api/tanks` - Get all tanks
- `GET /api/tanks/{id}` - Get tank by ID
- `GET /api/tanks/{id}/level` - Get current tank level
- `PUT /api/tanks/{id}` - Update tank configuration

### Controls
- `GET /api/controls` - Get all controls
- `GET /api/controls/{id}` - Get control by ID
- `POST /api/controls/{id}/activate` - Activate control
- `PUT /api/controls/{id}/state` - Update control state

### Settings
- `GET /api/settings` - Get system settings
- `PUT /api/settings` - Update system settings
- `GET /api/settings/van-diagrams` - Get available van diagrams

### Alerts
- `GET /api/alerts` - Get alerts (with filtering)
- `POST /api/alerts/{id}/acknowledge` - Acknowledge alert
- `DELETE /api/alerts/{id}` - Clear alert

### SignalR Hubs
- `TelemetryHub` - Real-time tank levels, control states
- `AlertHub` - Real-time alert notifications

## Plugin System

### IHardwarePlugin Interface
```csharp
interface IHardwarePlugin
{
    string Name { get; }
    string Version { get; }
    Task InitializeAsync(Dictionary<string, object> configuration);
    Task<bool> TestConnectionAsync();
    void Dispose();
}
```

### ISensorPlugin Interface (extends IHardwarePlugin)
```csharp
interface ISensorPlugin : IHardwarePlugin
{
    Task<double> ReadValueAsync(string sensorId);
    Task<IDictionary<string, double>> ReadAllValuesAsync();
}
```

### IControlPlugin Interface (extends IHardwarePlugin)
```csharp
interface IControlPlugin : IHardwarePlugin
{
    Task<bool> SetStateAsync(string controlId, object state);
    Task<object> GetStateAsync(string controlId);
}
```

## Testing Strategy

### Unit Tests
- Service layer logic
- Plugin implementations
- Data validation
- Alert generation logic

### Integration Tests
- API endpoint testing
- Database operations
- SignalR communication
- Plugin loading

### End-to-End Tests
- Full user workflows
- Multi-client scenarios
- Real hardware simulation

## Deployment

### Raspberry Pi Setup
1. **Prerequisites**
   - Raspberry Pi 4 (minimum 2GB RAM)
   - Raspbian OS (64-bit recommended)
   - Docker and Docker Compose installed
   - Network connectivity (WiFi AP mode optional)

2. **Installation**
   ```bash
   # Clone repository
   git clone https://github.com/yourorg/vandaemon.git
   cd vandaemon

   # Configure environment
   cp .env.example .env
   nano .env

   # Start services
   docker-compose up -d
   ```

3. **Access**
   - Web UI: http://raspberrypi.local:8080
   - API: http://raspberrypi.local:8080/api

### Configuration
- Environment variables for plugin selection
- Volume mounts for persistent configuration
- Port mapping for external access

## Security Considerations

1. **Network Security**
   - HTTPS for production (Let's Encrypt)
   - Optional authentication (JWT)
   - Rate limiting on API endpoints

2. **Hardware Access**
   - Plugin sandboxing
   - Permission management for GPIO/I2C
   - Fail-safe mechanisms

3. **Data Privacy**
   - Local data storage (no cloud by default)
   - Optional cloud backup with encryption

## Performance Requirements

- **Response Time**: < 100ms for control actions
- **Real-time Updates**: < 500ms latency
- **Concurrent Clients**: Support 5+ simultaneous connections
- **CPU Usage**: < 30% average on Raspberry Pi 4
- **Memory**: < 500MB total

## Monitoring and Logging

- **Application Logging**: Serilog with structured logging
- **Metrics**: Prometheus metrics endpoint
- **Health Checks**: `/health` endpoint
- **Performance Monitoring**: Application Insights (optional)

## Future Enhancements

### Phase 5+
1. Voice control integration (Alexa/Google Home)
2. GPS tracking and location-based features
3. Weather integration
4. Maintenance scheduling and reminders
5. Energy consumption analytics
6. Mobile app (native iOS/Android)
7. Multi-van management
8. Cloud synchronization

## Risk Assessment

| Risk | Impact | Likelihood | Mitigation |
|------|--------|------------|------------|
| Hardware compatibility issues | High | Medium | Extensive plugin testing, simulation mode |
| Network connectivity issues | Medium | High | Offline mode, local caching |
| Performance on Raspberry Pi | High | Medium | Performance testing, optimization |
| Plugin configuration complexity | Medium | Medium | Configuration wizard, presets |
| Real-time communication failures | High | Low | Automatic reconnection, fallback polling |

## Success Criteria

1. Successfully deploy on Raspberry Pi 4
2. Real-time monitoring with < 1 second latency
3. Support for at least 3 different hardware integrations
4. 95% uptime over 30-day period
5. Responsive UI on mobile devices
6. Comprehensive documentation and setup guides

## Timeline

- **Week 1-2**: Foundation and core services
- **Week 3-4**: Hardware integration plugins
- **Week 5-6**: UI enhancement and controls
- **Week 7-8**: Advanced features and testing
- **Week 9**: Documentation and deployment
- **Week 10**: Testing and refinement

## Resources Required

- Development: 1-2 developers
- Testing: Access to Raspberry Pi hardware
- Hardware: Sample Modbus devices, sensors for testing
- Documentation: Technical writer (part-time)

## Conclusion

This project plan provides a comprehensive roadmap for developing the Camper Van Control System. The modular architecture ensures extensibility, while the phased approach allows for incremental delivery and testing. The use of modern .NET technologies and containerization ensures maintainability and ease of deployment.
