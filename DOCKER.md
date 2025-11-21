# VanDaemon Docker Guide

This guide explains how to run VanDaemon using Docker and Docker Compose.

## Prerequisites

- Docker Engine 20.10+ or Docker Desktop
- Docker Compose v2.0+ (included with Docker Desktop)

## Quick Start

From the solution root directory, run:

```bash
docker compose up
```

This will:
1. Build the API container (.NET 10 backend)
2. Build the Web container (Blazor WebAssembly frontend with nginx)
3. Start both containers with proper networking
4. Expose the API on port 5000 and the Web UI on port 8080

Access the application:
- **Web UI**: http://localhost:8080
- **API**: http://localhost:5000
- **API Health**: http://localhost:5000/health

## Docker Compose Commands

### Start in Detached Mode
```bash
docker compose up -d
```

### View Logs
```bash
# All services
docker compose logs -f

# Specific service
docker compose logs -f api
docker compose logs -f web
```

### Stop Services
```bash
docker compose stop
```

### Stop and Remove Containers
```bash
docker compose down
```

### Stop, Remove, and Clean Volumes
```bash
docker compose down -v
```

### Rebuild Images
```bash
# Rebuild all images
docker compose build

# Rebuild specific service
docker compose build api
docker compose build web

# Rebuild and start
docker compose up --build
```

## Configuration

### Environment Variables

Copy `.env.example` to `.env` and customize:

```bash
cp .env.example .env
```

Key variables:
- `ASPNETCORE_ENVIRONMENT`: Development, Production
- `API_PORT`: External port for API (default: 5000)
- `WEB_PORT`: External port for Web UI (default: 8080)
- `DEFAULT_SENSOR_PLUGIN`: Simulated, Modbus, I2C, Victron
- `DEFAULT_CONTROL_PLUGIN`: Simulated, Modbus, I2C, Victron

### Custom Ports

To change ports, edit `docker-compose.yml`:

```yaml
services:
  api:
    ports:
      - "YOUR_PORT:80"  # Change 5000 to your desired port
  web:
    ports:
      - "YOUR_PORT:80"  # Change 8080 to your desired port
```

## Data Persistence

Docker volumes are used for data persistence:

- `api-data`: Stores JSON data files (tanks.json, controls.json, settings.json, alerts.json)
- `api-logs`: Stores application logs

### Backing Up Data

```bash
# Create backup directory
mkdir -p backups

# Backup api-data volume
docker run --rm -v vandaemon_api-data:/data -v $(pwd)/backups:/backup alpine tar czf /backup/api-data-backup.tar.gz -C /data .

# Backup api-logs volume
docker run --rm -v vandaemon_api-logs:/logs -v $(pwd)/backups:/backup alpine tar czf /backup/api-logs-backup.tar.gz -C /logs .
```

### Restoring Data

```bash
# Restore api-data volume
docker run --rm -v vandaemon_api-data:/data -v $(pwd)/backups:/backup alpine tar xzf /backup/api-data-backup.tar.gz -C /data

# Restore api-logs volume
docker run --rm -v vandaemon_api-logs:/logs -v $(pwd)/backups:/backup alpine tar xzf /backup/api-logs-backup.tar.gz -C /logs
```

## Architecture

### API Container (vandaemon-api)
- **Base Image**: mcr.microsoft.com/dotnet/aspnet:10.0
- **Build**: Multi-stage build with SDK:10.0
- **Port**: 80 (mapped to 5000 on host)
- **Health Check**: HTTP GET /health every 30s
- **Volumes**:
  - `/app/data` - JSON data storage
  - `/app/logs` - Application logs

### Web Container (vandaemon-web)
- **Base Image**: nginx:alpine
- **Build**: .NET SDK 10.0 for Blazor build, nginx for serving
- **Port**: 80 (mapped to 8080 on host)
- **Dependencies**: Waits for API health check before starting
- **Environment**: API_BASE_URL configured to connect to API container

### Networking
- Both containers run on the `vandaemon` bridge network
- Containers can communicate using service names (e.g., `http://api:80`)
- Only exposed ports are accessible from host

## Troubleshooting

### Check Container Status
```bash
docker compose ps
```

### Check Container Health
```bash
docker inspect vandaemon-api --format='{{.State.Health.Status}}'
```

### Access Container Shell
```bash
# API container
docker compose exec api /bin/bash

# Web container
docker compose exec web /bin/sh
```

### View Real-time Resource Usage
```bash
docker compose stats
```

### Common Issues

**Port already in use:**
```bash
# Check what's using the port
netstat -ano | findstr :5000    # Windows
lsof -i :5000                   # Linux/Mac

# Change port in docker-compose.yml or stop the conflicting service
```

**Build fails with "No space left on device":**
```bash
# Clean up Docker system
docker system prune -a --volumes
```

**API container not healthy:**
```bash
# Check logs
docker compose logs api

# Verify health endpoint manually
docker compose exec api curl http://localhost:80/health
```

**Web can't connect to API:**
```bash
# Verify network connectivity
docker compose exec web ping api

# Check API_BASE_URL environment variable
docker compose exec web env | grep API_BASE_URL
```

## Production Deployment

For production deployments:

1. **Use environment-specific compose file**:
   ```bash
   docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d
   ```

2. **Set proper environment variables**:
   - Set `ASPNETCORE_ENVIRONMENT=Production`
   - Configure production plugin settings
   - Set appropriate log levels

3. **Use a reverse proxy** (nginx, Traefik, Caddy) for:
   - HTTPS/TLS termination
   - Load balancing
   - Rate limiting
   - Security headers

4. **Configure volume backups**:
   - Schedule regular backups of `api-data` volume
   - Implement log rotation for `api-logs` volume

5. **Monitor containers**:
   - Use `docker compose logs` or external logging (ELK, Loki)
   - Monitor health endpoints
   - Set up alerts for container failures

## Development with Docker

### Hot Reload Development

For development with hot reload, mount source code as volumes:

```yaml
services:
  api:
    volumes:
      - ./src:/src
      - api-data:/app/data
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
```

### Debug with Docker

1. Enable remote debugging in API container
2. Expose debug port (5001)
3. Attach debugger from IDE (Visual Studio, VS Code)

See `.vscode/launch.json` for debug configuration examples.

## See Also

- [Deployment Guide](DEPLOYMENT.md)
- [Quick Start Guide](QUICK_START.md)
- [Main README](README.md)
