# Feature Specification: .NET 10 Platform Upgrade

**Feature Branch**: `003-dotnet-10-upgrade`
**Created**: 2025-11-21
**Status**: Draft
**Input**: Upgrade VanDaemon from .NET 8 to .NET 10

## User Scenarios & Testing *(mandatory)*

### User Story 1 - System Continues to Function After Upgrade (Priority: P1)

As a van owner, when the system is upgraded to the latest platform version, I want all my existing tank monitoring, controls, and alerts to continue working exactly as before so that I experience no disruption to my van management.

**Why this priority**: This is the fundamental requirement - the upgrade must be invisible to users. Any breaking changes would impact critical van monitoring functionality and violate user trust.

**Independent Test**: Can be fully tested by (1) deploying upgraded system, (2) verifying all existing features work (Dashboard, Tanks, Controls, Settings, Alerts), (3) confirming SignalR real-time updates function, (4) testing on both Raspberry Pi and Fly.io deployments. Delivers value by maintaining service continuity.

**Acceptance Scenarios**:

1. **Given** user opens Dashboard after upgrade, **When** page loads, **Then** tank levels display correctly with real-time updates
2. **Given** user toggles a control after upgrade, **When** control is activated, **Then** state changes within 200ms and persists correctly
3. **Given** user has custom settings configured before upgrade, **When** system is upgraded, **Then** all settings (van model, thresholds, alert preferences) are preserved
4. **Given** background monitoring service is running after upgrade, **When** tank level breaches threshold, **Then** alert is generated and broadcast via SignalR

---

### User Story 2 - Improved System Performance (Priority: P2)

As a van owner, when using the upgraded system, I want to experience faster page loads and more responsive controls so that I can monitor and manage my van systems more efficiently.

**Why this priority**: Platform upgrades typically include performance optimizations. Faster response times improve user experience, especially on resource-constrained Raspberry Pi deployments. Not critical for basic functionality but measurably improves quality.

**Independent Test**: Can be tested by (1) measuring page load times before and after upgrade, (2) measuring API response times, (3) measuring SignalR message latency, (4) comparing memory usage on Raspberry Pi. Demonstrates performance improvements.

**Acceptance Scenarios**:

1. **Given** user navigates to Dashboard after upgrade, **When** page loads, **Then** initial render completes at least 10% faster than pre-upgrade baseline
2. **Given** user triggers control action after upgrade, **When** API processes request, **Then** response time is equal to or better than pre-upgrade baseline
3. **Given** system runs on Raspberry Pi 4 after upgrade, **When** monitoring background service, **Then** memory usage is equal to or lower than pre-upgrade (maintain <500MB target)
4. **Given** SignalR connection is active after upgrade, **When** backend broadcasts update, **Then** client receives message within 100ms latency target

---

### User Story 3 - Enhanced Security and Stability (Priority: P3)

As a van owner, when the system is upgraded to the latest platform version, I want to benefit from the latest security patches and bug fixes so that my van monitoring system remains secure and reliable over time.

**Why this priority**: Security patches are important for long-term system health but are typically transparent to users. Not a user-facing feature but critical for system integrity. Justifies the upgrade effort even if no visible changes.

**Independent Test**: Can be tested by (1) running security vulnerability scanners before/after, (2) verifying no known CVEs in dependencies, (3) testing error handling edge cases, (4) long-running stability tests (24+ hours). Demonstrates security posture improvement.

**Acceptance Scenarios**:

1. **Given** system is upgraded to latest platform, **When** running dependency vulnerability scan, **Then** 0 high or critical severity vulnerabilities are detected
2. **Given** system encounters unexpected input after upgrade, **When** error occurs, **Then** system handles gracefully without crashes or data corruption
3. **Given** system runs continuously for 7 days after upgrade, **When** monitoring uptime and stability, **Then** no memory leaks or performance degradation observed
4. **Given** system is deployed on Raspberry Pi after upgrade, **When** platform security patches are applied, **Then** system remains protected against known vulnerabilities in previous platform version

---

### Edge Cases

- What happens if Docker build fails during upgrade due to .NET 10 SDK unavailability?
  - **Mitigation**: Ensure Docker images explicitly specify .NET 10 SDK version. Test build process in CI before deploying. Have rollback plan to .NET 8 if build fails.

- How does system handle incompatible NuGet packages that don't support .NET 10?
  - **Package Audit**: Check all current dependencies for .NET 10 compatibility before upgrade. Identify alternatives or updated versions for any incompatible packages. MudBlazor, Serilog, NModbus, SignalR all have .NET 10 support.

- What happens to existing data (tanks.json, controls.json, alerts.json, settings.json) during upgrade?
  - **Data Preservation**: JSON file storage is platform-agnostic. No data migration required. Files remain untouched during platform upgrade.

- How does upgrade affect SignalR WebSocket connections from existing clients?
  - **Client Compatibility**: SignalR protocol is backward compatible. Existing Blazor WebAssembly clients continue to work with upgraded backend. No forced client refresh required.

- What happens if Raspberry Pi deployment fails due to ARM architecture compatibility?
  - **ARM Testing**: Test .NET 10 on ARM64 architecture in CI before deploying to Raspberry Pi. .NET 10 has full ARM support. Verify linux-arm64 runtime is included in deployment.

- How does upgrade affect Fly.io deployment with combined Docker container?
  - **Deployment Testing**: Test combined Dockerfile build with .NET 10 before production deployment. Verify nginx and API both function in upgraded container. Run health check endpoints.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-1**: All existing features MUST function identically after upgrade, including Dashboard, Tanks page, Controls page, Settings page, and Alerts API

- **FR-2**: SignalR real-time communication MUST continue to work with existing clients, maintaining <100ms broadcast latency and automatic reconnection capability

- **FR-3**: Background monitoring service MUST continue polling tank levels every 5 seconds and broadcasting updates to connected clients

- **FR-4**: JSON file persistence (tanks.json, controls.json, alerts.json, settings.json) MUST remain unchanged and compatible with upgraded system

- **FR-5**: All existing API endpoints MUST maintain identical request/response contracts, ensuring no breaking changes for API clients

- **FR-6**: Docker containers (API, Web, Combined) MUST build successfully with .NET 10 SDK and runtime

- **FR-7**: System MUST continue to meet Constitution Principle II performance requirements (<500ms real-time latency) after upgrade

- **FR-8**: All existing unit tests (xUnit test projects) MUST pass without modification or with only platform-specific adjustments

### Non-Functional Requirements

- **NFR-1**: System performance MUST be equal to or better than pre-upgrade baseline, with no regression in page load times, API response times, or memory usage

- **NFR-2**: System MUST deploy successfully on all target platforms: Raspberry Pi 4 (ARM64 Linux), Fly.io (AMD64 Linux), Windows development environments

- **NFR-3**: Upgrade process MUST be reversible with documented rollback procedure in case of critical issues discovered post-deployment

- **NFR-4**: System MUST remain in compliance with VanDaemon Constitution v1.0.0 after upgrade (all 5 core principles)

- **NFR-5**: Docker image size MUST not increase by more than 20% to avoid excessive storage consumption and slow deployments

- **NFR-6**: System MUST continue to support HTTPS on both Raspberry Pi (Let's Encrypt) and Fly.io (automatic) deployments without configuration changes

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-1**: 100% of existing automated tests pass after upgrade (all xUnit tests in solution)

- **SC-2**: 0 breaking changes detected in API contracts (all endpoints maintain request/response compatibility)

- **SC-3**: Performance is equal or improved: page loads within 10% of baseline, API responses within baseline, memory usage ≤500MB

- **SC-4**: System successfully deploys on all 3 target platforms (Raspberry Pi, Fly.io, Windows dev) without errors

- **SC-5**: System runs continuously for 24 hours post-upgrade on Raspberry Pi without crashes, memory leaks, or errors

- **SC-6**: 0 high or critical security vulnerabilities detected in .NET 10 dependencies

- **SC-7**: Docker image size increase <20% compared to .NET 8 baseline (acceptable for platform upgrade)

## Out of Scope

The following are explicitly **not** included in this upgrade:

- **New .NET 10 features**: No adoption of new language features, APIs, or platform capabilities beyond compatibility fixes
- **Architecture changes**: No refactoring or restructuring of code beyond what's required for platform compatibility
- **Dependency updates**: Only update NuGet packages if required for .NET 10 compatibility; no opportunistic upgrades
- **Performance tuning**: No specific optimization work beyond leveraging platform improvements
- **Breaking API changes**: No changes to existing API contracts or endpoints
- **Database migration**: No change to JSON file storage (platform-agnostic, no migration needed)
- **UI redesign**: No changes to MudBlazor components or page layouts
- **Feature additions**: Pure platform upgrade; no new functionality

## Assumptions

1. **.NET 10 is released and stable**: .NET 10 SDK and runtime are generally available and production-ready (assuming November 2025 release)
2. **All dependencies support .NET 10**: MudBlazor, Serilog, xUnit, Moq, FluentAssertions, SignalR, NModbus libraries have .NET 10-compatible versions
3. **Docker base images available**: Microsoft provides official .NET 10 SDK and ASP.NET runtime Docker images for linux-arm64 and linux-amd64
4. **Blazor WebAssembly unchanged**: No breaking changes in Blazor WASM that require client-side modifications
5. **SignalR protocol compatibility**: SignalR maintains protocol compatibility across .NET 8 and .NET 10
6. **JSON serialization unchanged**: System.Text.Json behavior is consistent or improved in .NET 10
7. **ARM64 support maintained**: .NET 10 continues full support for ARM64 architecture (Raspberry Pi 4)
8. **No breaking changes in language**: C# language features used in codebase remain supported in .NET 10
9. **CI/CD compatibility**: GitHub Actions runners support .NET 10 SDK
10. **Rollback capability**: Can revert to .NET 8 by checking out previous git commit and rebuilding

## Dependencies

### Internal Dependencies
- **All projects in solution**: VanDaemon.Api, VanDaemon.Core, VanDaemon.Application, VanDaemon.Infrastructure, VanDaemon.Web, all Plugin projects, all Test projects
- **Docker configurations**: Dockerfile.api, Dockerfile.web, Dockerfile.combined, docker-compose.yml
- **CI/CD pipelines**: .github/workflows/build.yml, .github/workflows/fly-deploy.yml

### External Dependencies
- **.NET 10 SDK**: Required for building all projects
- **.NET 10 Runtime (ASP.NET)**: Required for running backend API
- **.NET 10 Runtime (WebAssembly)**: Required for Blazor WebAssembly frontend
- **Docker**: Must support .NET 10 base images (mcr.microsoft.com/dotnet/sdk:10.0, mcr.microsoft.com/dotnet/aspnet:10.0)
- **GitHub Actions**: Must support .NET 10 SDK in CI runners
- **NuGet packages**: All dependencies must have .NET 10-compatible versions

## Technical Constraints

1. **Target Framework Change**: All .csproj files must update `<TargetFramework>net8.0</TargetFramework>` to `<TargetFramework>net10.0</TargetFramework>`
2. **Docker Base Images**: Dockerfiles must update base image tags from :8.0 to :10.0
3. **CI/CD SDK Version**: GitHub Actions workflows must specify .NET 10 SDK version (actions/setup-dotnet@v3 with dotnet-version: '10.0.x')
4. **Breaking API Changes**: Must identify and address any breaking changes in .NET 10 platform APIs
5. **NuGet Package Compatibility**: Must audit and update package versions for .NET 10 support
6. **Build Warnings**: Must address any new warnings or obsolete API usages introduced by .NET 10 compiler
7. **Runtime Behavior Changes**: Must test for any behavioral differences in JSON serialization, HTTP client, SignalR, etc.

## Risks

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| NuGet package incompatibility | Medium | High | Audit all packages before upgrade, identify alternatives, test in dev environment first |
| Breaking changes in platform APIs | Low | High | Review .NET 10 breaking changes documentation, run all tests, incremental deployment |
| Performance regression | Low | Medium | Baseline performance metrics pre-upgrade, compare post-upgrade, rollback if regression > 20% |
| Docker build failures | Low | Medium | Test builds in CI before production deployment, verify ARM64 support explicitly |
| SignalR protocol changes | Very Low | High | Test WebSocket connections with existing clients, verify backward compatibility |
| Raspberry Pi compatibility issues | Low | High | Test on actual Raspberry Pi 4 hardware before production deployment, verify ARM64 runtime |
| Deployment rollback needed | Low | Critical | Document rollback procedure, keep .NET 8 deployment available, use git tags for versions |

## Open Questions

None. .NET platform upgrade process is well-documented, and all technical dependencies are known.

## Constitution Compliance

This upgrade maintains compliance with all VanDaemon Constitution principles:

- **Principle I: Plugin-First Hardware Abstraction**: No changes to plugin interfaces or architecture
- **Principle II: Real-Time Reliability**: <500ms latency maintained or improved (FR-7, SC-3)
- **Principle III: Offline-First & Local Storage**: JSON file storage unchanged, offline capability maintained (FR-4)
- **Principle IV: Test-Driven Hardware Integration**: All existing tests must pass, simulated plugins unchanged (FR-8, SC-1)
- **Principle V: Clean Architecture**: Layer separation unchanged, no architectural modifications

The upgrade is a platform modernization effort that maintains all architectural and functional characteristics.

## Upgrade Justification

This upgrade is required per PROJECT_PLAN.md Section "Technology Stack" Line 68:

> **Framework**: .NET 10 (REQUIRED - upgrade from current .NET 8 implementation)

**Benefits**:
1. **Long-term support**: Stay within supported platform lifecycle
2. **Security patches**: Access to latest security fixes
3. **Performance improvements**: Platform-level optimizations
4. **Modern tooling**: Latest SDK and runtime features
5. **Ecosystem alignment**: Match industry standards and documentation
