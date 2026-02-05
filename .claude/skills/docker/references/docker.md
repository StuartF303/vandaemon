# Docker Reference

## Contents
- Dockerfile Patterns
- Docker Compose Configuration
- Multi-Container Architecture
- Common Anti-Patterns
- Troubleshooting

## Dockerfile Patterns

### API Container (Multi-Stage Build)

```dockerfile
# docker/Dockerfile.api
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/Backend/VanDaemon.Api/VanDaemon.Api.csproj", "Backend/VanDaemon.Api/"]
COPY ["src/Backend/VanDaemon.Core/VanDaemon.Core.csproj", "Backend/VanDaemon.Core/"]
RUN dotnet restore "Backend/VanDaemon.Api/VanDaemon.Api.csproj"
COPY src/ .
RUN dotnet publish "Backend/VanDaemon.Api/VanDaemon.Api.csproj" -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app .
HEALTHCHECK --interval=30s --timeout=3s CMD curl -f http://localhost:80/health || exit 1
ENTRYPOINT ["dotnet", "VanDaemon.Api.dll"]
```

### Web Container (Blazor WASM + nginx)

```dockerfile
# docker/Dockerfile.web
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/Frontend/VanDaemon.Web/VanDaemon.Web.csproj", "Frontend/VanDaemon.Web/"]
RUN dotnet restore
COPY src/Frontend/ Frontend/
RUN dotnet publish "Frontend/VanDaemon.Web/VanDaemon.Web.csproj" -c Release -o /app

FROM nginx:alpine AS final
COPY --from=build /app/wwwroot /usr/share/nginx/html
COPY docker/nginx.conf /etc/nginx/nginx.conf
```

## Docker Compose Configuration

### Local Development Setup

```yaml
# docker-compose.yml
services:
  api:
    build:
      context: .
      dockerfile: docker/Dockerfile.api
    ports:
      - "5000:80"
    volumes:
      - api-data:/app/data
      - api-logs:/app/logs
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:80/health"]
      interval: 30s
      timeout: 3s
      retries: 3

  web:
    build:
      context: .
      dockerfile: docker/Dockerfile.web
    ports:
      - "8080:80"
    depends_on:
      api:
        condition: service_healthy

volumes:
  api-data:
  api-logs:

networks:
  default:
    name: vandaemon
```

## Multi-Container Architecture

### WARNING: Using localhost Between Containers

**The Problem:**

```yaml
# BAD - localhost doesn't work between containers
environment:
  - API_URL=http://localhost:5000
```

**Why This Breaks:**
1. Each container has its own network namespace
2. `localhost` refers to the container itself, not the host
3. API calls fail with connection refused

**The Fix:**

```yaml
# GOOD - use container service name
environment:
  - API_URL=http://api:80
```

### Combined Container for Fly.io

Single container with supervisor managing nginx + API:

```dockerfile
# docker/Dockerfile.combined
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build-api
# ... build API ...

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build-web
# ... build Blazor WASM ...

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
RUN apt-get update && apt-get install -y nginx supervisor curl
COPY --from=build-api /app /app
COPY --from=build-web /app/wwwroot /var/www/html
COPY docker/nginx.combined.conf /etc/nginx/nginx.conf
COPY docker/supervisord.conf /etc/supervisor/conf.d/supervisord.conf
EXPOSE 8080
CMD ["/usr/bin/supervisord", "-c", "/etc/supervisor/conf.d/supervisord.conf"]
```

## Common Anti-Patterns

### WARNING: No Health Check

**The Problem:**

```dockerfile
# BAD - no health check defined
ENTRYPOINT ["dotnet", "VanDaemon.Api.dll"]
```

**Why This Breaks:**
1. Orchestrators can't determine container health
2. Load balancers route traffic to unhealthy containers
3. `depends_on: condition: service_healthy` won't work

**The Fix:**

```dockerfile
# GOOD - explicit health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=10s \
  CMD curl -f http://localhost:80/health || exit 1
ENTRYPOINT ["dotnet", "VanDaemon.Api.dll"]
```

### WARNING: COPY Before Restore

**The Problem:**

```dockerfile
# BAD - invalidates cache on any file change
COPY . .
RUN dotnet restore
RUN dotnet publish
```

**Why This Breaks:**
1. Any source file change invalidates restore cache
2. Every build re-downloads all NuGet packages
3. Build times increase dramatically

**The Fix:**

```dockerfile
# GOOD - copy csproj first, then restore, then copy source
COPY ["src/Backend/VanDaemon.Api/VanDaemon.Api.csproj", "Backend/VanDaemon.Api/"]
RUN dotnet restore "Backend/VanDaemon.Api/VanDaemon.Api.csproj"
COPY src/ .
RUN dotnet publish -c Release -o /app
```

## Troubleshooting

### Container Won't Start

```bash
# Check logs
docker compose logs api

# Check health status
docker inspect vandaemon-api --format='{{.State.Health.Status}}'

# Access container shell
docker compose exec api /bin/bash
```

### Port Already in Use

```bash
# Find process using port
netstat -ano | findstr :5000    # Windows
lsof -i :5000                   # Linux/Mac

# Use different port in docker-compose.yml
ports:
  - "5001:80"  # Changed from 5000