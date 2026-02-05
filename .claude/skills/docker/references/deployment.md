# Deployment Reference

## Contents
- Deployment Targets
- Docker Compose (Local/Raspberry Pi)
- Fly.io (Cloud)
- Environment Configuration
- Common Anti-Patterns

## Deployment Targets

| Target | Container Setup | Use Case |
|--------|-----------------|----------|
| Local dev | docker-compose (2 containers) | Development and testing |
| Raspberry Pi | docker-compose (2 containers) | Offline van deployment |
| Fly.io | Single combined container | Cloud/remote access |

## Docker Compose (Local/Raspberry Pi)

### Start Services

```bash
# Start in background
docker compose up -d

# View logs
docker compose logs -f

# Rebuild and start
docker compose up --build -d

# Stop and remove
docker compose down

# Stop, remove, AND delete volumes
docker compose down -v
```

### Raspberry Pi Specific Setup

```bash
# Install Docker
curl -fsSL https://get.docker.com | sh
sudo usermod -aG docker $USER

# Enable I2C for sensor access
sudo raspi-config  # Interface Options → I2C → Enable

# Clone and deploy
git clone https://github.com/StuartF303/vandaemon.git
cd vandaemon
docker compose up -d
```

### Auto-Start on Boot (systemd)

```ini
# /etc/systemd/system/vandaemon.service
[Unit]
Description=VanDaemon Control System
Requires=docker.service
After=docker.service

[Service]
Type=oneshot
RemainAfterExit=yes
WorkingDirectory=/home/pi/vandaemon
ExecStart=/usr/bin/docker compose up -d
ExecStop=/usr/bin/docker compose down

[Install]
WantedBy=multi-user.target
```

```bash
sudo systemctl enable vandaemon
sudo systemctl start vandaemon
```

## Fly.io (Cloud)

### Initial Deployment

```bash
# Install CLI
curl -L https://fly.io/install.sh | sh

# Login and create app
flyctl auth login
flyctl apps create vandaemon

# Deploy
flyctl deploy
```

### fly.toml Configuration

```toml
app = "vandaemon"
primary_region = "lhr"

[build]
  dockerfile = "docker/Dockerfile.combined"

[http_service]
  internal_port = 8080
  force_https = true
  auto_stop_machines = true
  auto_start_machines = true

[[services.http_checks]]
  interval = "30s"
  timeout = "5s"
  path = "/health"
```

### Access URLs

| Path | Purpose |
|------|---------|
| `/` | Blazor WASM frontend |
| `/api/*` | REST API endpoints |
| `/hubs/*` | SignalR WebSocket |
| `/health` | Health check |
| `/swagger` | API documentation |

## Environment Configuration

### Production Environment Variables

```yaml
# docker-compose.prod.yml
services:
  api:
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - VanDaemon__RefreshIntervalSeconds=5
      - MqttLedDimmer__MqttBroker=mosquitto
      - MqttLedDimmer__MqttPort=1883
```

### Secrets Management

```bash
# Fly.io secrets
flyctl secrets set MQTT_PASSWORD=supersecret

# .env file for local
cp .env.example .env
# Edit .env with real values
```

## Common Anti-Patterns

### WARNING: Exposing Internal Ports

**The Problem:**

```yaml
# BAD - exposes API directly to internet
services:
  api:
    ports:
      - "5000:80"  # Accessible from anywhere
```

**Why This Breaks:**
1. No TLS termination
2. No rate limiting
3. Bypasses nginx security headers

**The Fix:**

```yaml
# GOOD - only nginx exposed, API internal only
services:
  api:
    expose:
      - "80"  # Internal only, no host binding
  
  web:
    ports:
      - "8080:80"  # Only nginx exposed
    depends_on:
      - api
```

### WARNING: No Volume Backups

**The Problem:**

```yaml
# BAD - data only exists in volume
volumes:
  api-data:
# No backup strategy defined
```

**Why This Breaks:**
1. `docker compose down -v` deletes all data
2. Volume corruption = total data loss
3. No disaster recovery option

**The Fix:**

```bash
# Backup volumes regularly
docker run --rm \
  -v vandaemon_api-data:/data \
  -v $(pwd)/backups:/backup \
  alpine tar czf /backup/api-data-$(date +%Y%m%d).tar.gz -C /data .
```

## Deployment Validation

Iterate until all checks pass:

1. Deploy changes
2. Validate health: `curl https://vandaemon.fly.dev/health`
3. If health check fails:
   - Check logs: `flyctl logs --app vandaemon`
   - Fix issues and redeploy
4. Only proceed when health returns `{"status":"healthy"}`