# Monitoring Reference

## Contents
- Health Checks
- Logging with Serilog
- Container Monitoring
- Troubleshooting Commands
- Common Anti-Patterns

## Health Checks

### API Health Endpoint

```csharp
// Program.cs
app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    timestamp = DateTime.UtcNow
}));
```

### Docker Health Check

```dockerfile
HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
  CMD curl -f http://localhost:80/health || exit 1
```

### Compose Health Check

```yaml
services:
  api:
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:80/health"]
      interval: 30s
      timeout: 3s
      retries: 3
      start_period: 10s
```

## Logging with Serilog

See the **serilog** skill for detailed configuration.

### Container Log Output

```json
// Structured JSON in Docker logs
{"Timestamp":"2025-01-21T10:30:00Z","Level":"Information","Message":"Tank FreshWater updated to 75%"}
```

### View Container Logs

```bash
# All services
docker compose logs -f

# Single service with timestamps
docker compose logs -f --timestamps api

# Last 100 lines
docker compose logs --tail 100 api
```

### Log Volume Configuration

```yaml
services:
  api:
    volumes:
      - api-logs:/app/logs
    environment:
      - Serilog__WriteTo__1__Args__path=/app/logs/vandaemon-.txt
```

## Container Monitoring

### Real-Time Resource Usage

```bash
# All containers
docker compose stats

# Output:
# CONTAINER    CPU %   MEM USAGE / LIMIT    NET I/O
# api          2.5%    120MiB / 512MiB      1.2MB / 500KB
# web          0.1%    10MiB / 128MiB       500KB / 1MB
```

### Check Container Status

```bash
# Container states
docker compose ps

# Detailed inspection
docker inspect vandaemon-api --format='{{.State.Health.Status}}'
# Returns: healthy, unhealthy, or starting
```

### Fly.io Monitoring

```bash
# View logs
flyctl logs --app vandaemon

# Check status
flyctl status --app vandaemon

# SSH into container
flyctl ssh console -C "supervisorctl status"
# Output:
# api    RUNNING   pid 123, uptime 0:05:00
# nginx  RUNNING   pid 124, uptime 0:05:00
```

## Troubleshooting Commands

### Container Won't Start

```bash
# Check logs for errors
docker compose logs api | tail -50

# Check if port is in use
netstat -ano | findstr :5000  # Windows
lsof -i :5000                 # Linux

# Verify image builds
docker compose build --no-cache api
```

### Health Check Failures

```bash
# Test health endpoint directly
docker compose exec api curl http://localhost:80/health

# Check if API process is running
docker compose exec api ps aux | grep dotnet

# Inspect health check history
docker inspect vandaemon-api --format='{{json .State.Health}}'
```

### SignalR Connection Issues

```bash
# Test WebSocket upgrade
curl -i -N \
  -H "Connection: Upgrade" \
  -H "Upgrade: websocket" \
  -H "Sec-WebSocket-Version: 13" \
  -H "Sec-WebSocket-Key: test" \
  https://vandaemon.fly.dev/hubs/telemetry

# Expected: 101 Switching Protocols
```

## Common Anti-Patterns

### WARNING: No Log Rotation

**The Problem:**

```yaml
# BAD - logs grow forever
volumes:
  - api-logs:/app/logs
# No rotation configured
```

**Why This Breaks:**
1. Disk fills up over time
2. Container crashes when disk full
3. Performance degrades with huge log files

**The Fix:**

```json
// appsettings.json - Serilog rolling file
{
  "Serilog": {
    "WriteTo": [{
      "Name": "File",
      "Args": {
        "path": "/app/logs/vandaemon-.txt",
        "rollingInterval": "Day",
        "retainedFileCountLimit": 7
      }
    }]
  }
}
```

### WARNING: No Resource Limits

**The Problem:**

```yaml
# BAD - container can consume unlimited resources
services:
  api:
    image: vandaemon-api
```

**Why This Breaks:**
1. Memory leak crashes entire host
2. CPU spike affects other containers
3. Unpredictable behavior under load

**The Fix:**

```yaml
# GOOD - explicit resource limits
services:
  api:
    image: vandaemon-api
    deploy:
      resources:
        limits:
          cpus: '1.0'
          memory: 512M
        reservations:
          cpus: '0.25'
          memory: 128M
```

## Monitoring Checklist

Copy this checklist for production monitoring setup:

- [ ] Health endpoint returns JSON with status and timestamp
- [ ] Dockerfile has HEALTHCHECK instruction
- [ ] docker-compose has healthcheck with start_period
- [ ] Serilog configured with rolling file (7 day retention)
- [ ] Resource limits set in docker-compose
- [ ] Fly.io health checks configured in fly.toml
- [ ] Log aggregation strategy defined (local volumes or cloud)