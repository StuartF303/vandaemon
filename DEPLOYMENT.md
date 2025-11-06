# VanDaemon Deployment

Quick deployment guide for VanDaemon Camper Van Control System.

## Deployment Options

VanDaemon supports multiple deployment methods:

1. **Fly.io** (Recommended for cloud hosting) - See below
2. **Docker Compose** (For local/Raspberry Pi) - See [docker/docker-compose.yml](docker/docker-compose.yml)
3. **Kubernetes** - Coming soon

## Fly.io Deployment

### Quick Start

1. **Install Fly.io CLI**
   ```bash
   curl -L https://fly.io/install.sh | sh
   ```

2. **Login to Fly.io**
   ```bash
   flyctl auth login
   ```

3. **Create Apps**
   ```bash
   flyctl apps create vandaemon-api
   flyctl apps create vandaemon-web
   ```

4. **Deploy**
   ```bash
   # Deploy API
   flyctl deploy --config fly.api.toml

   # Deploy Web
   flyctl deploy --config fly.web.toml
   ```

5. **Access Your Application**
   - Web: https://vandaemon-web.fly.dev
   - API: https://vandaemon-api.fly.dev

### GitHub Actions Auto-Deploy

The repository includes automatic deployment via GitHub Actions.

**Setup:**
1. Get your Fly.io token: `flyctl auth token`
2. Add to GitHub Secrets: `FLY_API_TOKEN`
3. Push to `main` branch to auto-deploy

See [docs/deployment/fly-io-deployment.md](docs/deployment/fly-io-deployment.md) for detailed instructions.

## Raspberry Pi Deployment

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

- `fly.api.toml` - Fly.io configuration for API backend
- `fly.web.toml` - Fly.io configuration for Web frontend
- `docker/docker-compose.yml` - Docker Compose for local deployment
- `docker/Dockerfile.api.prod` - Production API Docker image
- `docker/Dockerfile.web.prod` - Production Web Docker image

## Environment Variables

### API Backend
- `ASPNETCORE_URLS` - URL bindings (default: http://0.0.0.0:8080)
- `ASPNETCORE_ENVIRONMENT` - Environment (Development/Production)

### Web Frontend
- `API_URL` - Backend API URL (e.g., https://vandaemon-api.fly.dev)

## Monitoring

### Fly.io
```bash
# View logs
flyctl logs --app vandaemon-api
flyctl logs --app vandaemon-web

# Check status
flyctl status --app vandaemon-api

# Open dashboard
flyctl dashboard --app vandaemon-api
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
- Ensure Docker build works locally: `docker build -f docker/Dockerfile.api.prod .`
- Check GitHub Actions logs for detailed errors
- Verify `FLY_API_TOKEN` secret is set

### Connection Issues
- API not responding: Check `flyctl status --app vandaemon-api`
- CORS errors: Verify API URL in `fly.web.toml` matches deployed API
- SignalR failures: Check WebSocket support is enabled

### Local Deployment Issues
- Port conflicts: Change ports in `docker-compose.yml`
- Build errors: Run `dotnet restore` and `dotnet build`
- Permission issues: Add user to docker group

## Support

For detailed documentation, see:
- [Fly.io Deployment Guide](docs/deployment/fly-io-deployment.md)
- [Raspberry Pi Setup](docs/deployment/raspberry-pi-setup.md)
- [API Reference](docs/api/api-reference.md)

For issues, please visit: https://github.com/StuartF303/vandaemon/issues
