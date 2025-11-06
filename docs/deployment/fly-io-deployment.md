# Fly.io Deployment Guide

This guide explains how to deploy the VanDaemon application to Fly.io as a single combined container.

## Architecture

VanDaemon is deployed as a **single Fly.io application** that includes both:

1. **API Backend** - ASP.NET Core Web API (runs on port 5000 internally)
2. **Web Frontend** - Blazor WebAssembly served by Nginx

The application uses:
- **Nginx** (port 8080) - Serves static files and proxies API requests
- **.NET API** (port 5000) - Handles API and SignalR requests
- **Supervisor** - Manages both processes in the same container

All traffic comes through a single endpoint at port 8080:
- `/` → Blazor WebAssembly static files
- `/api` → Proxied to .NET API
- `/hubs` → Proxied to SignalR hubs

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

### Application Not Responding

1. Check if the app is running: `flyctl status --app vandaemon`
2. Check logs for errors: `flyctl logs --app vandaemon`
3. Restart the application: `flyctl apps restart vandaemon`

### Build Failures

1. Check the GitHub Actions workflow logs
2. Test build locally: `docker build -f docker/Dockerfile.combined -t vandaemon-test .`
3. Ensure the `FLY_API_TOKEN` secret is set correctly

See the full guide above for more troubleshooting options.

## Further Reading

- [Fly.io Documentation](https://fly.io/docs/)
- [Fly.io .NET Guide](https://fly.io/docs/languages-and-frameworks/dotnet/)
- [Fly.io Pricing](https://fly.io/docs/about/pricing/)
