# Fly.io Deployment Guide

This guide explains how to deploy the VanDaemon application to Fly.io.

## Architecture

VanDaemon is deployed as two separate Fly.io applications:

1. **API Backend** (`vandaemon-api`) - ASP.NET Core Web API
2. **Web Frontend** (`vandaemon-web`) - Blazor WebAssembly with Nginx

The frontend proxies API requests to the backend application.

## Prerequisites

1. [Fly.io account](https://fly.io/app/sign-up)
2. [Fly.io CLI installed](https://fly.io/docs/hands-on/install-flyctl/)
3. Authenticated with Fly.io: `flyctl auth login`

## Initial Setup

### 1. Create Fly.io Applications

Create the API application:

```bash
flyctl apps create vandaemon-api
```

Create the Web frontend application:

```bash
flyctl apps create vandaemon-web
```

### 2. Configure Secrets (if needed)

If your application requires any secrets (database connection strings, API keys, etc.):

```bash
# For API
flyctl secrets set --app vandaemon-api \
  DATABASE_URL="your-database-url" \
  JWT_SECRET="your-jwt-secret"

# For Web
flyctl secrets set --app vandaemon-web \
  API_KEY="your-api-key"
```

### 3. Deploy Applications

Deploy the API backend:

```bash
flyctl deploy --config fly.api.toml
```

Deploy the Web frontend:

```bash
flyctl deploy --config fly.web.toml
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

## Configuration Files

### API Configuration (`fly.api.toml`)

- **App Name**: `vandaemon-api`
- **Region**: `iad` (Ashburn, Virginia)
- **Port**: 8080
- **Resources**: 1 shared CPU, 512MB RAM
- **Auto-scaling**: Stops when idle, starts on request

### Web Configuration (`fly.web.toml`)

- **App Name**: `vandaemon-web`
- **Region**: `iad` (Ashburn, Virginia)
- **Port**: 8080
- **Resources**: 1 shared CPU, 256MB RAM
- **Auto-scaling**: Stops when idle, starts on request
- **API URL**: Proxies to `https://vandaemon-api.fly.dev`

## Updating the Deployment

### Change App Names

If you want to use different app names, update:

1. `fly.api.toml` - Change the `app` field
2. `fly.web.toml` - Change the `app` field AND the `API_URL` environment variable
3. Create the new apps with `flyctl apps create`

### Change Region

To deploy to a different region:

1. Update `primary_region` in both `fly.api.toml` and `fly.web.toml`
2. See available regions: `flyctl platform regions`

### Scale Resources

To increase resources:

```bash
# Scale API
flyctl scale vm --app vandaemon-api shared-cpu-2x --memory 1024

# Scale Web
flyctl scale vm --app vandaemon-web shared-cpu-1x --memory 512
```

## Monitoring

### View Logs

```bash
# API logs
flyctl logs --app vandaemon-api

# Web logs
flyctl logs --app vandaemon-web
```

### Check Status

```bash
# API status
flyctl status --app vandaemon-api

# Web status
flyctl status --app vandaemon-web
```

### Access Dashboard

```bash
# Open API dashboard
flyctl dashboard --app vandaemon-api

# Open Web dashboard
flyctl dashboard --app vandaemon-web
```

## Accessing Your Application

After deployment, your application will be available at:

- **Web Frontend**: `https://vandaemon-web.fly.dev`
- **API Backend**: `https://vandaemon-api.fly.dev`

## Troubleshooting

### API Not Responding

1. Check if the app is running:
   ```bash
   flyctl status --app vandaemon-api
   ```

2. Check logs for errors:
   ```bash
   flyctl logs --app vandaemon-api
   ```

3. Restart the application:
   ```bash
   flyctl apps restart vandaemon-api
   ```

### Frontend Can't Connect to API

1. Verify the API URL in `fly.web.toml` matches your API app name
2. Check if CORS is properly configured in the API
3. Ensure the API is deployed and running

### Build Failures

1. Check the GitHub Actions workflow logs
2. Verify all Dockerfiles are present and correct
3. Ensure the `FLY_API_TOKEN` secret is set correctly

### Health Check Failures

The API includes a `/health` endpoint. If health checks fail:

1. Verify the endpoint exists in your API
2. Check if the port (8080) is correct
3. Review API logs for startup errors

## Cost Optimization

Fly.io offers:
- Free tier with resource limits
- Auto-scaling (scale to zero when idle)
- Pay-as-you-go pricing

Current configuration:
- **API**: 512MB RAM, 1 shared CPU, auto-stops when idle
- **Web**: 256MB RAM, 1 shared CPU, auto-stops when idle

This should fit within the free tier for light usage.

## Custom Domain

To use a custom domain:

1. Add certificate:
   ```bash
   flyctl certs create --app vandaemon-web your-domain.com
   ```

2. Configure DNS:
   ```bash
   flyctl ips list --app vandaemon-web
   ```
   Add the returned IP addresses to your DNS:
   - IPv4: A record
   - IPv6: AAAA record

## Database Setup (Optional)

If you need a database:

```bash
# Create Postgres database
flyctl postgres create --name vandaemon-db --region iad

# Attach to API
flyctl postgres attach --app vandaemon-api vandaemon-db
```

This will automatically set the `DATABASE_URL` secret.

## Further Reading

- [Fly.io Documentation](https://fly.io/docs/)
- [Fly.io .NET Guide](https://fly.io/docs/languages-and-frameworks/dotnet/)
- [Fly.io Pricing](https://fly.io/docs/about/pricing/)
