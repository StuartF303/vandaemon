# VanDaemon Deployment Architecture

This document provides a comprehensive overview of the VanDaemon deployment architecture for Fly.io.

## Overview

VanDaemon uses a **single-container architecture** that combines both the API backend and web frontend in one deployable unit. This approach simplifies deployment, reduces costs, and ensures consistency between components.

## Container Architecture

```
┌─────────────────────────────────────────────────────────────┐
│  Fly.io Machine (1024MB RAM, 1 Shared CPU)                  │
│  Region: IAD (Ashburn, Virginia)                             │
│                                                               │
│  ┌─────────────────────────────────────────────────────────┐│
│  │  Supervisor (Process Manager)                            ││
│  │  - Manages both nginx and API processes                  ││
│  │  - Auto-restarts on failure                              ││
│  │  - Logs to stdout/stderr                                 ││
│  └─────────────────────────────────────────────────────────┘│
│                                                               │
│  ┌─────────────────────────────────────────────────────────┐│
│  │  Nginx (Port 8080)                                       ││
│  │  ┌─────────────────────────────────────────────────┐    ││
│  │  │  Static File Server                              │    ││
│  │  │  - Serves Blazor WebAssembly files               │    ││
│  │  │  - Gzip compression enabled                      │    ││
│  │  │  - Cache headers for .dll, .wasm files           │    ││
│  │  └─────────────────────────────────────────────────┘    ││
│  │                                                           ││
│  │  ┌─────────────────────────────────────────────────┐    ││
│  │  │  Reverse Proxy                                   │    ││
│  │  │  - /api/* → 127.0.0.1:5000                      │    ││
│  │  │  - /hubs/* → 127.0.0.1:5000 (WebSocket)         │    ││
│  │  │  - /health → 127.0.0.1:5000                     │    ││
│  │  └─────────────────────────────────────────────────┘    ││
│  └─────────────────────────────────────────────────────────┘│
│                                                               │
│  ┌─────────────────────────────────────────────────────────┐│
│  │  .NET 8 API (Port 5000 - Internal Only)                 ││
│  │  ┌─────────────────────────────────────────────────┐    ││
│  │  │  ASP.NET Core Web API                            │    ││
│  │  │  - REST endpoints (/api/*)                       │    ││
│  │  │  - Health check (/health)                        │    ││
│  │  │  - Swagger/OpenAPI docs (/swagger)               │    ││
│  │  └─────────────────────────────────────────────────┘    ││
│  │                                                           ││
│  │  ┌─────────────────────────────────────────────────┐    ││
│  │  │  SignalR Hub                                     │    ││
│  │  │  - Real-time telemetry (/hubs/telemetry)        │    ││
│  │  │  - WebSocket connections                         │    ││
│  │  │  - Broadcasts tank levels, control states        │    ││
│  │  └─────────────────────────────────────────────────┘    ││
│  │                                                           ││
│  │  ┌─────────────────────────────────────────────────┐    ││
│  │  │  Background Services                             │    ││
│  │  │  - Tank monitoring (5s intervals)                │    ││
│  │  │  - Alert checking                                │    ││
│  │  │  - SignalR broadcasting                          │    ││
│  │  └─────────────────────────────────────────────────┘    ││
│  └─────────────────────────────────────────────────────────┘│
└─────────────────────────────────────────────────────────────┘
```

## Request Flow

### Static File Requests (Blazor WebAssembly)

```
User Browser
    ↓ HTTPS GET https://vandaemon.fly.dev/
Fly.io Edge (SSL Termination)
    ↓ HTTP GET / (Port 8080)
Nginx
    ↓ Serves from /var/www/html/
    ↓ Returns index.html
Blazor WASM loads in browser
    ↓ Browser downloads .dll, .wasm, .dat files
    ↓ All served by nginx with caching
```

### API Requests

```
User Browser (Blazor App)
    ↓ HTTPS GET https://vandaemon.fly.dev/api/tanks
Fly.io Edge (SSL Termination)
    ↓ HTTP GET /api/tanks (Port 8080)
Nginx (Reverse Proxy)
    ↓ Rewrites: /api/tanks → /tanks
    ↓ Proxies to 127.0.0.1:5000
.NET API
    ↓ Processes request
    ↓ Returns JSON response
Nginx
    ↓ Proxies response back
Fly.io Edge
    ↓ Returns HTTPS response
User Browser
```

### SignalR WebSocket Connections

```
User Browser (Blazor App)
    ↓ WSS wss://vandaemon.fly.dev/hubs/telemetry
Fly.io Edge (SSL Termination)
    ↓ WS ws://127.0.0.1:8080/hubs/telemetry
Nginx (WebSocket Upgrade)
    ↓ Detects Upgrade: websocket header
    ↓ Proxies to 127.0.0.1:5000
    ↓ Maintains persistent connection
.NET API (SignalR Hub)
    ↓ Accepts WebSocket connection
    ↓ Adds client to hub
    ↓ Broadcasts updates every 5 seconds
    ↓ Bidirectional communication
Nginx (Transparent Proxy)
    ↓ Passes WebSocket frames
Fly.io Edge
    ↓ Encrypts WebSocket frames
User Browser
    ↓ Receives real-time updates
```

## Key Components

### 1. Fly.io Infrastructure

**Machine Configuration** (`fly.toml`):
- **App**: `vandaemon`
- **Region**: `iad` (Ashburn, Virginia - US East)
- **Resources**: 1 shared CPU, 1024MB RAM
- **Auto-scaling**: Machines stop when idle, start on request
- **Port**: 8080 (internal), 443 (external HTTPS)

**Health Checks**:
- **Endpoint**: `/health`
- **Interval**: Every 10 seconds
- **Timeout**: 5 seconds
- **Threshold**: 3 consecutive failures triggers restart

### 2. Nginx Configuration

**Purpose**: Serve static files and reverse proxy API requests

**Key Configuration** (`docker/nginx.combined.conf`):

```nginx
# WebSocket upgrade mapping
map $http_upgrade $connection_upgrade {
    default upgrade;
    '' close;
}

server {
    listen 8080;
    server_name _;

    # Static files (Blazor WASM)
    location / {
        root /var/www/html;
        try_files $uri $uri/ /index.html;
        gzip on;
        gzip_types text/css application/javascript application/wasm;
    }

    # API proxy
    location /api {
        proxy_pass http://127.0.0.1:5000;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }

    # SignalR WebSocket proxy
    location /hubs {
        proxy_pass http://127.0.0.1:5000;
        proxy_http_version 1.1;

        # WebSocket upgrade
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection $connection_upgrade;
        proxy_set_header Host $host;

        # Long-lived connection timeouts
        proxy_connect_timeout 7d;
        proxy_send_timeout 7d;
        proxy_read_timeout 7d;
    }

    # Health check proxy
    location /health {
        proxy_pass http://127.0.0.1:5000;
    }
}
```

### 3. Supervisor Process Management

**Purpose**: Manage both nginx and API in single container

**Configuration** (`docker/supervisord.conf`):

```ini
[supervisord]
nodaemon=true
user=root

[program:api]
command=dotnet /app/api/VanDaemon.Api.dll
directory=/app/api
autostart=true
autorestart=true
stderr_logfile=/dev/stderr
stderr_logfile_maxbytes=0
stdout_logfile=/dev/stdout
stdout_logfile_maxbytes=0
environment=ASPNETCORE_URLS="http://127.0.0.1:5000",ASPNETCORE_ENVIRONMENT="Production"

[program:nginx]
command=/usr/sbin/nginx -g "daemon off;"
autostart=true
autorestart=true
stderr_logfile=/dev/stderr
stderr_logfile_maxbytes=0
stdout_logfile=/dev/stdout
stdout_logfile_maxbytes=0
```

### 4. Multi-Stage Docker Build

**Purpose**: Build both API and Web, combine in single runtime image

**Dockerfile** (`docker/Dockerfile.combined`):

```dockerfile
# Stage 1: Build API
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-api
WORKDIR /src
COPY src/Backend/ ./Backend/
RUN dotnet publish Backend/VanDaemon.Api/VanDaemon.Api.csproj \
    -c Release -o /app/api

# Stage 2: Build Web Frontend
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-web
WORKDIR /src
COPY src/Frontend/ ./Frontend/
RUN dotnet publish Frontend/VanDaemon.Web/VanDaemon.Web.csproj \
    -c Release -o /app/web

# Stage 3: Runtime - Combined
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Install nginx and supervisor
RUN apt-get update && \
    apt-get install -y nginx supervisor && \
    rm -rf /var/lib/apt/lists/*

# Copy API
COPY --from=build-api /app/api ./api

# Copy Web (static files)
COPY --from=build-web /app/web/wwwroot /var/www/html

# Copy configurations
COPY docker/nginx.combined.conf /etc/nginx/nginx.conf
COPY docker/supervisord.conf /etc/supervisor/conf.d/supervisord.conf

EXPOSE 8080
CMD ["/usr/bin/supervisord", "-c", "/etc/supervisor/conf.d/supervisord.conf"]
```

## Frontend Configuration

### Dynamic API URL

The frontend automatically determines the correct API base URL:

**Code** (`src/Frontend/VanDaemon.Web/Program.cs`):

```csharp
var apiBaseUrl = builder.HostEnvironment.BaseAddress.TrimEnd('/');

builder.Services.AddScoped(sp => new HttpClient {
    BaseAddress = new Uri(apiBaseUrl)
});
```

**Behavior**:
- **Production**: `https://vandaemon.fly.dev`
- **Development**: `http://localhost:8080`
- **No configuration files needed**

### SignalR Connection

**Code** (`src/Frontend/VanDaemon.Web/Services/TelemetryService.cs`):

```csharp
var hubConnection = new HubConnectionBuilder()
    .WithUrl($"{apiBaseUrl}/hubs/telemetry")
    .WithAutomaticReconnect()
    .Build();
```

**Connection URL**:
- **Production**: `wss://vandaemon.fly.dev/hubs/telemetry`
- **Development**: `ws://localhost:8080/hubs/telemetry`

## Backend Configuration

### Health Check Endpoint

**Code** (`src/Backend/VanDaemon.Api/Program.cs`):

```csharp
app.MapGet("/health", () => Results.Ok(new {
    status = "healthy",
    timestamp = DateTime.UtcNow
}));
```

**Returns**:
```json
{
  "status": "healthy",
  "timestamp": "2025-11-06T14:30:00Z"
}
```

### Environment-Aware CORS

**Code** (`src/Backend/VanDaemon.Api/Program.cs`):

```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        if (builder.Environment.IsDevelopment())
        {
            // Strict CORS for development
            policy.WithOrigins("http://localhost:8080", "http://localhost:5001")
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
        }
        else
        {
            // Permissive for production
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        }
    });
});
```

### SignalR Hub Registration

**Code** (`src/Backend/VanDaemon.Api/Program.cs`):

```csharp
app.MapHub<TelemetryHub>("/hubs/telemetry");
```

## Deployment Pipeline

### GitHub Actions Workflow

**File**: `.github/workflows/deploy-fly.yml`

```yaml
name: Deploy to Fly.io

on:
  push:
    branches: [main]
  workflow_dispatch:

jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3

      - uses: superfly/flyctl-actions/setup-flyctl@master

      - run: flyctl deploy --remote-only
        env:
          FLY_API_TOKEN: ${{ secrets.FLY_API_TOKEN }}
```

**Trigger**: Push to `main` branch or manual dispatch

**Process**:
1. Checkout code
2. Setup Fly.io CLI
3. Deploy to Fly.io (remote build)
4. Fly.io builds Docker image
5. Fly.io deploys new machine
6. Health checks verify deployment
7. Old machine removed (zero-downtime)

## Networking and Security

### SSL/TLS

- **Termination**: Fly.io edge (automatic)
- **Certificates**: Managed by Fly.io (Let's Encrypt)
- **Internal**: HTTP between edge and container
- **External**: HTTPS only (force_https = true)

### Port Configuration

| Component | Internal Port | External Port | Protocol |
|-----------|---------------|---------------|----------|
| Nginx | 8080 | 443 | HTTP/HTTPS |
| .NET API | 5000 | N/A (internal only) | HTTP |
| SignalR | 5000 | 443 (via nginx) | WebSocket |

### Firewall Rules

- **Inbound**: Only port 443 (HTTPS) exposed
- **Internal**: All services communicate via localhost
- **Outbound**: Unrestricted (for plugin communication)

## Scaling and Performance

### Current Configuration

- **Memory**: 1024MB RAM
- **CPU**: 1 shared CPU
- **Regions**: 1 (iad - US East)
- **Instances**: 1 (auto-scales to 0 when idle)

### Scaling Options

**Vertical Scaling** (more resources):
```bash
flyctl scale memory 2048 --app vandaemon
flyctl scale count 2 --app vandaemon  # More CPUs
```

**Horizontal Scaling** (more instances):
```bash
flyctl scale count 2 --app vandaemon  # 2 machines
```

**Geographic Distribution**:
```bash
flyctl regions add lhr ord --app vandaemon  # Add London, Chicago
```

### Performance Considerations

- **WebSocket Connections**: Maintain state, don't scale horizontally without Redis backplane
- **Static Files**: Cached by nginx and browser
- **API Requests**: Stateless, can scale horizontally
- **Background Services**: Run on all instances (coordination needed for scaling)

## Monitoring and Observability

### Fly.io Metrics

- **Health Checks**: Every 10 seconds
- **Machine Status**: started/stopped/crashed
- **Resource Usage**: CPU, memory, network
- **Request Logs**: All HTTP requests

### Application Logs

**Structure**:
```
[timestamp] [component] [level] message
```

**Components**:
- `api:` - .NET API logs
- `nginx:` - Nginx access/error logs
- `supervisord:` - Process management logs

**Accessing**:
```bash
flyctl logs --app vandaemon
flyctl logs --app vandaemon | grep "api:"
flyctl logs --app vandaemon --tail
```

## Disaster Recovery

### Backup Strategy

- **Code**: Stored in Git repository
- **Configuration**: Tracked in fly.toml
- **Data**: SQLite database (ephemeral, regenerated on start)
- **Settings**: Stored in database (lost on redeploy)

### Recovery Procedures

**Complete Failure**:
```bash
# Redeploy from scratch
flyctl deploy --remote-only
```

**Rollback to Previous Version**:
```bash
flyctl releases list --app vandaemon
flyctl releases rollback v23 --app vandaemon
```

**Data Loss**:
- Application regenerates default data on startup
- Users need to reconfigure settings via UI

## Cost Analysis

### Fly.io Pricing (Free Tier)

- **Included**: 3 shared-cpu VMs, 3GB persistent storage
- **VanDaemon Usage**: 1 VM (1024MB RAM, 1 shared CPU)
- **Cost**: $0/month (within free tier)
- **Auto-stop**: Stops when idle to save resources

### Scaling Costs

- **Additional RAM**: ~$0.0000022/MB/sec
- **Additional CPU**: ~$0.02/CPU/month
- **Bandwidth**: First 100GB free, then $0.02/GB

**Example**:
- 2GB RAM, 2 CPUs, 24/7 uptime: ~$10/month
- Perfect for production camper van usage

## Troubleshooting Reference

### Quick Diagnostics

```bash
# Is it running?
flyctl status --app vandaemon

# What's in the logs?
flyctl logs --app vandaemon --tail

# Are both processes running?
flyctl ssh console -C "supervisorctl status"

# Is health check passing?
curl https://vandaemon.fly.dev/health

# Test WebSocket support
curl -i -N -H "Connection: Upgrade" \
     -H "Upgrade: websocket" \
     https://vandaemon.fly.dev/hubs/telemetry
```

### Common Issues Matrix

| Symptom | Likely Cause | Solution |
|---------|--------------|----------|
| 502 Bad Gateway | API not running | Check supervisor logs |
| 404 on /api/* | nginx config wrong | Verify nginx.combined.conf |
| WebSocket fails | Missing upgrade headers | Check nginx WebSocket config |
| Health check fails | /health endpoint missing | Verify Program.cs |
| localhost in prod | Frontend config wrong | Use HostEnvironment.BaseAddress |
| Out of memory | Insufficient RAM | Scale to 2048MB |

## Further Reading

- [Fly.io Documentation](https://fly.io/docs/)
- [Nginx WebSocket Proxy](https://nginx.org/en/docs/http/websocket.html)
- [ASP.NET Core SignalR](https://docs.microsoft.com/aspnet/core/signalr/)
- [Blazor WebAssembly Hosting](https://docs.microsoft.com/aspnet/core/blazor/host-and-deploy/webassembly)
