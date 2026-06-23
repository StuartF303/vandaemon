---
name: docker
description: |
  Configures containerization with Docker and Docker Compose for VanDaemon.
  Use when: building containers, configuring docker-compose, deploying to Docker environments, troubleshooting container networking, or setting up multi-container applications.
allowed-tools: Read, Edit, Write, Glob, Grep, Bash
---

# Docker Skill

VanDaemon uses a multi-container architecture. There is **one authoritative stack** in the repo-root
`docker-compose.yml` — `api` + `web` + `mqtt` (Mosquitto) — used for **both** local dev and the
Raspberry Pi appliance (feature 006). A separate combined single-container image exists for Fly.io. The
API runs on .NET 10; the Web frontend uses nginx to serve Blazor WASM static files and proxy
API/WebSocket requests. Data persists via Docker volumes for JSON file storage and MQTT retained state.

> **There is only ONE compose file (repo root).** The old `docker/docker-compose.yml` was removed in
> feature 006 — do not reintroduce a second stack. The API reaches the broker by the `mqtt` service
> name via the `MqttLedDimmer__MqttBroker` env var; the committed `appsettings.json` default stays
> `localhost` so bare-metal `dotnet run` is unaffected.

> **Pi-5 appliance**: a flashable arm64 image (pi-gen) bakes this same stack + prebuilt GHCR images for
> a near-zero-touch headless controller. See `deploy/pi/` and `docs/deployment/pi-appliance-setup.md`.
> Multi-arch / appliance build pipeline lessons are in [ci-cd](references/ci-cd.md).

## Quick Start

### Start Development Environment

```bash
# From solution root
docker compose up -d

# View logs
docker compose logs -f

# Rebuild after code changes
docker compose up --build
```

### Access Points

| Service | URL | Purpose |
|---------|-----|---------|
| Web UI | http://localhost:8080 | Blazor WASM frontend |
| API | http://localhost:5000 | REST API + Swagger |
| Health | http://localhost:5000/health | Container health check |
| SignalR | ws://localhost:5000/hubs/telemetry | Real-time updates |

## Key Concepts

| Concept | Usage | Example |
|---------|-------|---------|
| Multi-stage builds | Minimize image size | `FROM sdk AS build` → `FROM aspnet AS final` |
| Health checks | Container orchestration | `HEALTHCHECK CMD curl /health` |
| Volume mounts | Persist JSON data | `api-data:/app/data` |
| Bridge network | Inter-container comms | `http://api:80` not `localhost:5000` |
| Supervisor | Multi-process container | Combined nginx + API for Fly.io |

## Common Patterns

### Container Networking

**When:** Containers need to communicate

```yaml
# docker-compose.yml
services:
  web:
    depends_on:
      api:
        condition: service_healthy
    # Use container name, not localhost
    environment:
      - API_BASE_URL=http://api:80
```

### Volume Persistence

**When:** Data must survive container restarts

```yaml
volumes:
  api-data:
  api-logs:

services:
  api:
    volumes:
      - api-data:/app/data    # JSON config files
      - api-logs:/app/logs    # Serilog output
```

## See Also

- [docker](references/docker.md)
- [ci-cd](references/ci-cd.md)
- [deployment](references/deployment.md)
- [monitoring](references/monitoring.md)

## Related Skills

- See the **dotnet** skill for .NET 10 SDK configuration in Dockerfiles
- See the **aspnet-core** skill for API container configuration
- See the **serilog** skill for log volume configuration