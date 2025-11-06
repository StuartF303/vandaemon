# Fly.io Deployment Guide

This guide explains how to deploy the VanDaemon application to Fly.io as a single combined container.

## Architecture

VanDaemon is deployed as a **single Fly.io application** that includes both:

1. **API Backend** - ASP.NET Core Web API (runs on port 5000 internally)
2. **Web Frontend** - Blazor WebAssembly served by Nginx

The application uses:
- **Nginx** (port 8080) - Serves static files and proxies API/WebSocket requests
- **.NET API** (port 5000) - Handles API and SignalR requests
- **Supervisor** - Manages both processes in the same container

All traffic comes through a single endpoint at port 8080:
- `/` → Blazor WebAssembly static files (nginx serves directly)
- `/api/*` → Proxied to .NET API (nginx → 127.0.0.1:5000)
- `/hubs/*` → Proxied to SignalR WebSocket (nginx → 127.0.0.1:5000 with WebSocket upgrade)
- `/health` → Health check endpoint (nginx → 127.0.0.1:5000)

### Key Implementation Details

#### Health Check Endpoint
The API includes a `/health` endpoint that Fly.io uses for health monitoring:

```csharp
app.MapGet("/health", () => Results.Ok(new {
    status = "healthy",
    timestamp = DateTime.UtcNow
}));
```

This endpoint:
- Returns HTTP 200 OK with status information
- Monitored by Fly.io every 10 seconds
- Triggers restart if health checks fail

#### Dynamic API URL Configuration
The frontend automatically determines the correct API URL:

```csharp
// In VanDaemon.Web/Program.cs
var apiBaseUrl = builder.HostEnvironment.BaseAddress.TrimEnd('/');
```

This means:
- **Production**: Uses `https://vandaemon.fly.dev` (current host)
- **Development**: Uses `http://localhost:8080` (current host)
- **No manual configuration needed**

#### WebSocket Support for SignalR
Nginx is configured to handle long-lived WebSocket connections:

```nginx
# WebSocket connection upgrade mapping
map $http_upgrade $connection_upgrade {
    default upgrade;
    '' close;
}

location /hubs {
    proxy_pass http://127.0.0.1:5000;
    proxy_http_version 1.1;

    # WebSocket headers
    proxy_set_header Upgrade $http_upgrade;
    proxy_set_header Connection $connection_upgrade;
    proxy_set_header Host $host;

    # Long-lived connection timeouts (7 days)
    proxy_connect_timeout 7d;
    proxy_send_timeout 7d;
    proxy_read_timeout 7d;
}
```

This configuration:
- Upgrades HTTP connections to WebSocket protocol
- Maintains persistent connections for real-time updates
- Supports timeouts up to 7 days for idle connections

#### CORS Configuration
The API uses environment-aware CORS settings:

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
            // Permissive for production (single domain)
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        }
    });
});
```

## Prerequisites

1. [Fly.io account](https://fly.io/app/sign-up)
2. [Fly.io CLI installed](https://fly.io/docs/hands-on/install-flyctl/)
3. Authenticated with Fly.io: `flyctl auth login`

## Initial Setup

### 1. Create Fly.io Application

Create the application:

```bash
flyctl apps create vandaemon
```

**Note**: You can choose a different name, but update the `app` field in `fly.toml` accordingly.

### 2. Configure Secrets (Optional)

If your application requires any secrets:

```bash
flyctl secrets set --app vandaemon \
  DATABASE_URL="your-database-url" \
  JWT_SECRET="your-jwt-secret"
```

### 3. Deploy Application

Deploy the combined application:

```bash
flyctl deploy
```

Or explicitly specify the config:

```bash
flyctl deploy --config fly.toml
```

## GitHub Actions Deployment

The repository includes a GitHub Actions workflow for automatic deployment.

### Setup

1. Get your Fly.io API token:

```bash
flyctl auth token
```

2. Add the token to GitHub Secrets:
   - Go to your repository on GitHub
   - Navigate to Settings → Secrets and variables → Actions
   - Click "New repository secret"
   - Name: `FLY_API_TOKEN`
   - Value: Your Fly.io API token

3. The workflow will automatically deploy when you push to the `main` branch

### Manual Deployment via GitHub Actions

You can also trigger a manual deployment:

1. Go to the "Actions" tab in your GitHub repository
2. Select "Deploy to Fly.io" workflow
3. Click "Run workflow"

## Configuration

### Main Configuration (`fly.toml`)

```toml
app = "vandaemon"
primary_region = "iad"

[build]
  dockerfile = "docker/Dockerfile.combined"

[http_service]
  internal_port = 8080
  force_https = true
  auto_stop_machines = true

[[vm]]
  cpu_kind = "shared"
  cpus = 1
  memory_mb = 1024
```

**Key Settings:**
- **App Name**: `vandaemon` (single application)
- **Region**: `iad` (Ashburn, Virginia)
- **Port**: 8080 (Nginx serves both static files and proxies API)
- **Resources**: 1 shared CPU, 1024MB RAM
- **Auto-scaling**: Stops when idle, starts on request

### Architecture Details

**Container Structure:**
```
┌─────────────────────────────────────┐
│  Fly.io Machine (1024MB RAM)        │
│                                      │
│  ┌────────────────────────────────┐ │
│  │  Nginx (Port 8080)             │ │
│  │  - Serves static Blazor files  │ │
│  │  - Proxies /api → 127.0.0.1:5000│ │
│  │  - Proxies /hubs → 127.0.0.1:5000│ │
│  └────────────────────────────────┘ │
│                                      │
│  ┌────────────────────────────────┐ │
│  │  .NET API (Port 5000)          │ │
│  │  - REST API endpoints          │ │
│  │  - SignalR hubs                │ │
│  │  - Health checks               │ │
│  └────────────────────────────────┘ │
│                                      │
│  Supervised by: supervisord          │
└─────────────────────────────────────┘
```

## Accessing Your Application

After deployment, your application will be available at:

- **Single URL**: `https://vandaemon.fly.dev`
  - Root (`/`) - Web Frontend
  - API (`/api/*`) - Backend API
  - SignalR (`/hubs/*`) - Real-time communication

## Monitoring

### View Logs

```bash
# Tail logs (both API and Nginx)
flyctl logs --app vandaemon

# View specific component
flyctl logs --app vandaemon | grep "api:"
flyctl logs --app vandaemon | grep "nginx:"
```

### Check Status

```bash
# Overall status
flyctl status --app vandaemon

# Check health
flyctl checks list --app vandaemon
```

### Access Dashboard

```bash
# Open web dashboard
flyctl dashboard --app vandaemon
```

## Troubleshooting

### Health Check Failures

**Symptom**: Fly.io shows "Health checks failing" or "Machine unhealthy"

**Diagnosis**:
```bash
# Check health endpoint directly
curl https://vandaemon.fly.dev/health

# Expected response:
# {"status":"healthy","timestamp":"2025-11-06T14:30:00Z"}

# View logs to see health check attempts
flyctl logs --app vandaemon | grep health

# Check both processes are running
flyctl ssh console -C "supervisorctl status"
# Expected output:
# api                              RUNNING   pid 123, uptime 0:05:00
# nginx                            RUNNING   pid 124, uptime 0:05:00
```

**Common Causes**:
- **API not started**: Check supervisor logs with `flyctl ssh console -C "supervisorctl tail api"`
- **Port mismatch**: Verify nginx is configured to listen on 8080 in `nginx.combined.conf`
- **Health endpoint missing**: Ensure Program.cs has `app.MapGet("/health", ...)`
- **Network issues**: Check if nginx is proxying requests to 127.0.0.1:5000

**Fix**:
```bash
# Restart the application
flyctl apps restart vandaemon

# If still failing, redeploy
flyctl deploy --remote-only
```

### SignalR WebSocket Connection Failing

**Symptom**: Real-time updates not working, browser shows failed WebSocket connection

**Diagnosis**:
```bash
# Test WebSocket upgrade capability
curl -i -N \
     -H "Connection: Upgrade" \
     -H "Upgrade: websocket" \
     -H "Sec-WebSocket-Version: 13" \
     -H "Sec-WebSocket-Key: test" \
     https://vandaemon.fly.dev/hubs/telemetry

# Expected: HTTP/1.1 101 Switching Protocols
# If you get 404: WebSocket route not configured
# If you get 502: Backend not responding
```

Check browser console:
```
F12 → Network tab → WS filter
Look for: wss://vandaemon.fly.dev/hubs/telemetry
Status: Should be "101 Switching Protocols"
```

**Common Causes**:
1. **Missing WebSocket configuration in nginx**:
   - Check `docker/nginx.combined.conf` has `map $http_upgrade` directive
   - Verify `/hubs` location block has `proxy_set_header Upgrade` and `Connection` headers

2. **Timeout too short**:
   - SignalR needs long-lived connections (7 days recommended)
   - Check nginx has `proxy_read_timeout 7d;`

3. **CORS issues**:
   - Check browser console for CORS errors
   - Verify API CORS policy allows SignalR connections

4. **SignalR hub not registered**:
   - Verify Program.cs has `app.MapHub<TelemetryHub>("/hubs/telemetry")`

**Fix**:
```bash
# Verify nginx configuration
flyctl ssh console -C "cat /etc/nginx/nginx.conf" | grep -A 20 "location /hubs"

# Check API logs for SignalR connections
flyctl logs | grep SignalR

# Redeploy with fixed configuration
git add docker/nginx.combined.conf
git commit -m "fix: Update nginx WebSocket configuration"
git push
```

### Frontend Making Requests to localhost

**Symptom**: Browser DevTools shows requests to `http://localhost:5000/api/*` instead of deployed URL

**Diagnosis**:
- Open browser DevTools (F12) → Network tab
- Look for failed requests to `localhost:5000`
- If you see these, the frontend is not using the correct API base URL

**Fix**: This is now fixed automatically in the codebase:
```csharp
// VanDaemon.Web/Program.cs
var apiBaseUrl = builder.HostEnvironment.BaseAddress.TrimEnd('/');
```

The frontend now:
- Uses `https://vandaemon.fly.dev` in production
- Uses `http://localhost:8080` in development
- No configuration file changes needed

If you're still seeing localhost requests:
1. Clear browser cache (Ctrl+Shift+Delete)
2. Hard refresh the page (Ctrl+Shift+R)
3. Verify you're running the latest version: `flyctl releases list`

### Application Not Responding

**Symptom**: Site doesn't load, times out, or shows 502 Bad Gateway

**Diagnosis**:
```bash
# Check machine status
flyctl status --app vandaemon

# Look for:
# Machines:
# ID            STATE     REGION  HEALTH CHECKS
# abc123        started   iad     3 total, 3 passing

# If machine is stopped or health checks failing:
flyctl logs --app vandaemon --tail
```

**Common Causes**:
- **Machine auto-stopped**: Fly.io stops idle machines to save costs
- **Out of memory**: Check logs for OOM killer
- **Crash loop**: Application keeps restarting

**Fix**:
```bash
# Check if machine is stopped
flyctl status --app vandaemon

# Wake up machine
flyctl machine start <machine-id>

# If keeps failing, check logs
flyctl logs --app vandaemon

# Restart application
flyctl apps restart vandaemon
```

### Build Failures

**Symptom**: GitHub Actions workflow fails or `flyctl deploy` errors

**Common Causes**:

1. **Docker build fails locally**:
```bash
# Test build locally
docker build -f docker/Dockerfile.combined -t vandaemon-test .

# Common issues:
# - Missing dependencies in Dockerfile
# - .NET restore failures
# - File path errors
```

2. **GitHub Actions secret not set**:
```bash
# Verify FLY_API_TOKEN is set:
# GitHub → Repository → Settings → Secrets and variables → Actions
# Should see: FLY_API_TOKEN

# Get new token if needed:
flyctl auth token
```

3. **Fly.io deployment failures**:
```bash
# Check Fly.io deployment logs
flyctl logs --app vandaemon

# Common issues:
# - Registry authentication failures
# - Resource limits exceeded
# - Syntax errors in fly.toml
```

**Fix**:
```bash
# Retry deployment
flyctl deploy --remote-only

# Force rebuild
flyctl deploy --remote-only --no-cache

# Check fly.toml syntax
cat fly.toml
```

### Logs and Monitoring

**View real-time logs**:
```bash
# All logs
flyctl logs --app vandaemon

# Filter by component
flyctl logs --app vandaemon | grep "api:"
flyctl logs --app vandaemon | grep "nginx:"

# Follow logs (like tail -f)
flyctl logs --app vandaemon --tail
```

**Check process status**:
```bash
# SSH into machine
flyctl ssh console --app vandaemon

# Inside machine:
supervisorctl status
supervisorctl tail api
supervisorctl tail nginx

# Check nginx configuration
nginx -t

# Check if API is responding internally
curl http://127.0.0.1:5000/health
```

**Performance monitoring**:
```bash
# Check resource usage
flyctl status --app vandaemon

# View metrics dashboard
flyctl dashboard --app vandaemon

# Scale if needed
flyctl scale memory 2048 --app vandaemon
flyctl scale count 2 --app vandaemon
```

### Emergency Rollback

If a deployment breaks production:

```bash
# List recent releases
flyctl releases list --app vandaemon

# Rollback to previous version
flyctl releases rollback <release-id> --app vandaemon

# Example:
# flyctl releases rollback v23 --app vandaemon
```

### Common Error Messages

**"Machine failed to reach healthy state"**
- Health check endpoint not responding
- See "Health Check Failures" section above

**"connection reset by peer" or "WebSocket connection failed"**
- WebSocket configuration issue
- See "SignalR WebSocket Connection Failing" section above

**"502 Bad Gateway"**
- nginx can't reach backend API on port 5000
- Check supervisor status: API process may have crashed

**"404 Not Found" for /api/* or /hubs/*  **
- nginx proxy configuration incorrect
- Verify nginx.combined.conf has location blocks for /api and /hubs

## Further Reading

- [Fly.io Documentation](https://fly.io/docs/)
- [Fly.io .NET Guide](https://fly.io/docs/languages-and-frameworks/dotnet/)
- [Fly.io Pricing](https://fly.io/docs/about/pricing/)
