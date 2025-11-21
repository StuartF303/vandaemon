<!--
═══════════════════════════════════════════════════════════════════════════════
SYNC IMPACT REPORT
═══════════════════════════════════════════════════════════════════════════════
Version: 0.0.0 → 1.0.0
Change Type: Initial constitution creation
Rationale: Establishing foundational governance for VanDaemon IoT project

Modified Principles:
  - All principles created (none existed previously)

Added Sections:
  ✅ Core Principles (5 principles defined)
  ✅ Architecture Requirements
  ✅ Development Workflow
  ✅ Governance

Templates Requiring Updates:
  ✅ plan-template.md - Constitution Check section references established
  ✅ spec-template.md - Will align with plugin and safety requirements
  ✅ tasks-template.md - Task types now include hardware integration categories

Follow-up TODOs:
  - None. All placeholders filled with project-specific values.

═══════════════════════════════════════════════════════════════════════════════
-->

# VanDaemon IoT Constitution

## Core Principles

### I. Plugin-First Hardware Abstraction

All hardware integrations MUST be implemented through the plugin system. Direct hardware access in application code is **prohibited**.

**Requirements**:
- Every sensor or control device MUST implement `ISensorPlugin` or `IControlPlugin` interfaces
- Plugins MUST be self-contained, independently testable, and documented
- Plugins MUST support graceful degradation (fail-safe behavior when hardware unavailable)
- Configuration MUST be passed via dictionary (JSON-serializable types only)
- Plugin initialization MUST validate hardware connectivity before returning success

**Rationale**: Camper vans vary widely in hardware configurations. Plugin abstraction enables support for Modbus, I2C, Victron Cerbo, and future integrations without changing application code.

### II. Real-Time Reliability (NON-NEGOTIABLE)

Real-time communication latency MUST be < 500ms end-to-end for safety-critical controls (water pump, heater, lighting).

**Requirements**:
- SignalR hub MUST broadcast state changes within 100ms of hardware event
- Background monitoring service MUST refresh tank levels every 5 seconds (configurable minimum 1s)
- WebSocket disconnections MUST trigger automatic reconnection with exponential backoff
- UI MUST display connection status prominently (green badge = connected, red = disconnected)
- Control actions MUST provide visual feedback within 200ms (optimistic UI updates allowed)

**Rationale**: Users rely on real-time feedback when operating critical van systems. Delayed updates could lead to water overflow, battery drain, or safety hazards.

### III. Offline-First & Local Storage

The system MUST function completely offline without internet connectivity. No cloud dependencies in core functionality.

**Requirements**:
- All configuration MUST be stored locally (JSON files or SQLite)
- Real-time data (tank levels, control states) MUST be served from in-memory cache
- Web frontend MUST be served as static files (Blazor WebAssembly)
- API MUST run on local network only (default: Raspberry Pi on van's WiFi)
- Cloud sync features MUST be optional and clearly labeled

**Rationale**: Camper vans frequently operate in remote locations without cellular or internet access. The system must be fully functional offline.

### IV. Test-Driven Hardware Integration

Hardware integration code MUST be testable without physical devices present.

**Requirements**:
- Every plugin MUST have a simulated counterpart for testing (e.g., `SimulatedSensorPlugin`)
- Simulated plugins MUST generate realistic data with gradual changes and noise
- Unit tests MUST use mocked plugins (Moq framework required)
- Integration tests MUST verify plugin contract compliance (initialization, reading, state changes)
- Hardware-specific tests MUST be clearly marked and skippable in CI/CD

**Rationale**: Cannot test Modbus devices, I2C sensors, or Victron integration in CI pipeline. Simulated plugins enable TDD workflow and continuous testing.

### V. Clean Architecture & Separation of Concerns

Project MUST maintain strict layer separation: Core → Application → Infrastructure → API.

**Requirements**:
- **Core layer**: Domain entities only, no dependencies
- **Application layer**: Business logic services, must not reference Infrastructure
- **Infrastructure layer**: Data access, external APIs, persistence implementations
- **API layer**: Controllers, SignalR hubs, dependency injection configuration
- **Plugin layer**: Hardware abstractions, independent of application services
- Controllers MUST be thin wrappers around services (no business logic)
- Services MUST be registered as Singletons (maintain in-memory state)
- All dependencies MUST be injected via constructor

**Rationale**: Clean architecture ensures testability, maintainability, and enables future database migration (JSON → SQLite) without touching application code.

## Architecture Requirements

### Safety & Fail-Safe Mechanisms

**Critical controls** (water pump, heater, propane) MUST implement fail-safe behavior:
- Hardware plugin connection loss MUST default to safe state (pump OFF, heater OFF)
- Alert service MUST generate critical alerts for hardware failures
- Control state MUST be verified after each command (read-back confirmation)
- Timeout on control operations: 5 seconds maximum, then raise alert

### Data Persistence Strategy

**Two-tier storage model** (established pattern):
1. **Configuration data**: Persisted to JSON files in `data/` directory (thread-safe via `JsonFileStore`)
2. **Real-time data**: In-memory collections in services (tank levels, control states)

**Migration path**: Infrastructure layer prepared for SQLite implementation. When migrating:
- Configuration data moves to SQLite tables
- Real-time data remains in-memory (performance critical)
- JsonFileStore becomes obsolete (deprecated gracefully)

### SignalR Communication Pattern

**Group-based subscriptions** MUST be used for targeted broadcasts:
- `tanks` group: Tank level updates
- `controls` group: Control state changes
- `alerts` group: Alert notifications

Clients MUST explicitly subscribe to groups (security boundary). Server-side broadcasts use `IHubContext<TelemetryHub>` from background service.

## Development Workflow

### Branch Strategy

- `main` branch: Production-ready code, requires PR approval
- Feature branches: `<issue-number>-<feature-name>` (e.g., `42-modbus-plugin`)
- All commits to `main` MUST include tests and pass CI/CD

### Testing Gates

**Before merging to main**:
- ✅ All unit tests pass (`dotnet test VanDaemon.sln`)
- ✅ No compiler warnings in Release build
- ✅ Code follows project conventions (async/await, nullable reference types)
- ✅ New services have corresponding tests in `tests/` directory
- ✅ Plugin implementations include simulated counterpart for testing

### Commit Message Format

Follow conventional commits:
- `feat:` New features or plugin implementations
- `fix:` Bug fixes or hardware integration corrections
- `docs:` Documentation updates (README, CLAUDE.md, constitution)
- `test:` Test additions or corrections
- `refactor:` Code restructuring without behavior changes
- `chore:` Dependency updates, build script changes

Include Claude Code attribution:
```
🤖 Generated with [Claude Code](https://claude.com/claude-code)

Co-Authored-By: Claude <noreply@anthropic.com>
```

### Code Quality Standards

**Nullable reference types**: Enabled project-wide (`<Nullable>enable</Nullable>`)
- Use `?` for nullable types explicitly
- Avoid `!` null-forgiving operator except when certainty proven

**Async patterns**:
- All I/O methods MUST be async with `Async` suffix
- Accept `CancellationToken cancellationToken = default` parameter
- Use `await` instead of `.Result` or `.Wait()`

**Logging**:
- Use structured logging: `_logger.LogInformation("Tank {TankId} level: {Level}%", tankId, level)`
- No `Console.WriteLine` in production code
- Log levels: Debug (verbose), Information (state changes), Warning (degraded), Error (failures), Critical (safety issues)

## Governance

### Constitution Authority

This constitution **supersedes all other practices**. When conflicts arise between this document and code comments, READMEs, or verbal agreements, the constitution takes precedence.

### Amendment Process

1. Propose amendment via GitHub issue with `constitution` label
2. Provide rationale, impact analysis, and migration plan
3. Version bump according to semantic versioning:
   - **MAJOR**: Removes or redefines core principles (breaks backward compatibility)
   - **MINOR**: Adds new principles or expands requirements
   - **PATCH**: Clarifications, wording improvements, typo fixes
4. Update Sync Impact Report at top of file
5. Propagate changes to affected templates and documentation
6. Require approval from repository owner before merging

### Compliance Review

**All PRs MUST**:
- Verify compliance with Core Principles (I-V)
- Reference constitution sections if introducing architectural changes
- Justify any exceptions with safety or performance rationale

**Quarterly review** of constitution adherence:
- Check for drift between constitution and actual codebase patterns
- Update constitution if established patterns prove superior (via amendment process)

### Runtime Guidance

For day-to-day development guidance beyond constitutional principles, refer to:
- `CLAUDE.md` - Architecture patterns, build commands, common gotchas
- `README.md` - Quick start, deployment, troubleshooting
- `PROJECT_PLAN.md` - Feature roadmap and phased implementation

**Version**: 1.0.0 | **Ratified**: 2025-11-21 | **Last Amended**: 2025-11-21
