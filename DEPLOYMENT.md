# VanDaemon Deployment

Quick deployment guide for VanDaemon Camper Van Control System.

## Deployment Options

VanDaemon supports multiple deployment methods:

1. **Fly.io** (Recommended for cloud hosting) - See below
2. **Docker Compose** (For local/Raspberry Pi) - See [docker/docker-compose.yml](docker/docker-compose.yml)
3. **Kubernetes** - Coming soon

## Fly.io Deployment (Cloud)

Deploy VanDaemon to the cloud as a single combined container.

### Quick Start

1. **Install Fly.io CLI**
   ```bash
   curl -L https://fly.io/install.sh | sh
   ```

2. **Login to Fly.io**
   ```bash
   flyctl auth login
   ```

3. **Create App**
   ```bash
   flyctl apps create vandaemon
   ```

4. **Deploy**
   ```bash
   flyctl deploy
   ```

5. **Access Your Application**
   - **Single URL**: https://vandaemon.fly.dev
   - Web Frontend: `/`
   - API Backend: `/api`
   - SignalR: `/hubs`

### Architecture

Single container with:
- **Nginx** (port 8080) - Serves Blazor WASM and proxies API/WebSocket requests
- **.NET API** (port 5000) - Handles backend logic internally
- **Supervisor** - Manages both processes

#### Request Routing
- `/` → Static Blazor WebAssembly files (nginx)
- `/api/*` → Proxied to .NET API (nginx → 127.0.0.1:5000)
- `/hubs/*` → Proxied to SignalR WebSocket (nginx → 127.0.0.1:5000 with WebSocket upgrade)
- `/health` → Health check endpoint (nginx → 127.0.0.1:5000)

### GitHub Actions Auto-Deploy

The repository includes automatic deployment via GitHub Actions.

**Setup:**
1. Get your Fly.io token: `flyctl auth token`
2. Add to GitHub Secrets: `FLY_API_TOKEN`
3. Push to `main` branch to auto-deploy

See [docs/deployment/fly-io-deployment.md](docs/deployment/fly-io-deployment.md) for detailed instructions.

## Raspberry Pi Deployment (Local)

For running on a Raspberry Pi:

1. **Install Docker**
   ```bash
   curl -fsSL https://get.docker.com | sh
   sudo usermod -aG docker $USER
   ```

2. **Clone Repository**
   ```bash
   git clone https://github.com/StuartF303/vandaemon.git
   cd vandaemon
   ```

3. **Start Services**
   ```bash
   docker compose -f docker/docker-compose.yml up -d
   ```

4. **Access Locally**
   - Web: http://localhost:8080
   - API: http://localhost:5000

See [docs/deployment/raspberry-pi-setup.md](docs/deployment/raspberry-pi-setup.md) for detailed setup.

## Configuration Files

- `fly.toml` - Fly.io configuration for combined deployment
- `docker/Dockerfile.combined` - Production Docker image
- `docker/nginx.combined.conf` - Nginx configuration
- `docker/supervisord.conf` - Process management
- `docker/docker-compose.yml` - Local Docker Compose deployment

## Environment Variables

### Combined Deployment
- `ASPNETCORE_ENVIRONMENT` - Environment (Development/Production)
- API runs on internal port 5000
- Nginx serves on port 8080

### Local Deployment
- Configure ports in `docker-compose.yml`
- API: Port 5000
- Web: Port 8080

## Monitoring

### Fly.io
```bash
# View logs
flyctl logs --app vandaemon

# Check status
flyctl status --app vandaemon

# Open dashboard
flyctl dashboard --app vandaemon
```

### Docker Compose
```bash
# View logs
docker compose -f docker/docker-compose.yml logs -f

# Check status
docker compose -f docker/docker-compose.yml ps
```

## Troubleshooting

### Fly.io Build Failures
- Ensure Docker build works locally: `docker build -f docker/Dockerfile.combined .`
- Check GitHub Actions logs for detailed errors
- Verify `FLY_API_TOKEN` secret is set

### Health Check Failures
If Fly.io reports health check failures:

```bash
# Check health endpoint directly
curl https://vandaemon.fly.dev/health

# Expected response:
# {"status":"healthy","timestamp":"2025-11-06T14:30:00Z"}

# View application logs
flyctl logs --app vandaemon

# Check process status
flyctl ssh console -C "supervisorctl status"
# Should show:
# api                              RUNNING   pid 123, uptime 0:05:00
# nginx                            RUNNING   pid 124, uptime 0:05:00
```

Common causes:
- API not started: Check supervisor logs
- Port mismatch: Verify nginx listens on 8080
- Health endpoint not defined: Ensure `app.MapGet("/health", ...)` in Program.cs

### SignalR WebSocket Connection Issues
If real-time updates aren't working:

```bash
# Test WebSocket upgrade
curl -i -N -H "Connection: Upgrade" \
     -H "Upgrade: websocket" \
     -H "Sec-WebSocket-Version: 13" \
     -H "Sec-WebSocket-Key: test" \
     https://vandaemon.fly.dev/hubs/telemetry

# Should return 101 Switching Protocols
```

Common causes:
- Nginx WebSocket config missing: Check `docker/nginx.combined.conf` has:
  - `map $http_upgrade $connection_upgrade` directive
  - `proxy_set_header Upgrade $http_upgrade`
  - `proxy_set_header Connection $connection_upgrade`
- Timeout too short: Extend to 7d for long-lived connections
- CORS issues: Check browser console for errors

### Connection Issues
- Check both nginx and API are running: `flyctl logs`
- Verify health endpoint: `curl https://vandaemon.fly.dev/health`
- Check supervisord status: `flyctl ssh console -C "supervisorctl status"`

### Frontend API URL Issues
If frontend makes requests to `localhost` in production:

This is now fixed automatically:
- Frontend uses `builder.HostEnvironment.BaseAddress`
- No manual configuration needed
- Works in both development (localhost) and production (deployed URL)

Verify with browser DevTools Network tab:
- Requests should go to `https://vandaemon.fly.dev/api/*`
- NOT `http://localhost:5000/api/*`

### Local Deployment Issues
- Port conflicts: Change ports in `docker-compose.yml`
- Build errors: Run `dotnet restore` and `dotnet build`
- Permission issues: Add user to docker group

## Cost

### Fly.io
- **Free tier**: 1024MB RAM, 1 shared CPU
- **Auto-scaling**: Stops when idle to save costs
- **Single container**: More efficient than split deployment

### Raspberry Pi
- **Free**: Runs locally on your hardware
- **No cloud costs**: Perfect for offline use

## Support

For detailed documentation, see:
- [Fly.io Deployment Guide](docs/deployment/fly-io-deployment.md)
- [Raspberry Pi Setup](docs/deployment/raspberry-pi-setup.md)
- [API Reference](docs/api/api-reference.md)

For issues, please visit: https://github.com/StuartF303/vandaemon/issues
