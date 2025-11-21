# VanDaemon - Camper Van Control System

A comprehensive IoT control system for camper vans built with .NET 10 and Blazor WebAssembly. Monitor and control your van's systems including water tanks, LPG, lighting, heating, and more - all from your phone, tablet, or computer.

![License](https://img.shields.io/badge/license-MIT-blue.svg)
![.NET](https://img.shields.io/badge/.NET-10.0-purple.svg)
![Docker](https://img.shields.io/badge/Docker-Ready-blue.svg)

## Features

- **Real-time Monitoring**: Track water tanks, waste water, LPG, fuel, and battery levels in real-time
- **Interactive Controls**: Control lights, water pump, heater, and other systems with a touch-friendly interface
- **Configurable Alerts**: Get notified when tanks are running low or waste tanks are full
- **Multiple Van Types**: Support for different van models with customizable diagrams (default: Mercedes Sprinter LWB)
- **Modular Plugin System**: Easy integration with various hardware systems (Modbus, I2C, Victron Cerbo, etc.)
- **Web-Based Interface**: Access from any device with a web browser
- **Containerized Deployment**: Easy deployment with Docker on Raspberry Pi or other Linux systems
- **Offline-First**: Works without internet connectivity

## Quick Start

### Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (for development)
- [Docker](https://docs.docker.com/get-docker/) (for containerized deployment)
- Raspberry Pi 4 (2GB+ RAM recommended) or similar Linux system

### Running with Docker (Recommended)

1. Clone the repository:
```bash
git clone https://github.com/yourusername/vandaemon.git
cd vandaemon
```

2. Start the services:
```bash
docker compose up -d
```

For detailed Docker usage, configuration, and troubleshooting, see [DOCKER.md](DOCKER.md).

3. Access the web interface:
- Web UI: http://localhost:8080
- API: http://localhost:5000
- API Documentation: http://localhost:5000/swagger

### Running for Development

1. Build the solution:
```bash
./build.sh          # Linux/Mac
build.bat           # Windows
```

2. Run the API:
```bash
cd src/Backend/VanDaemon.Api
dotnet run
```

3. Run the Web UI (in a separate terminal):
```bash
cd src/Frontend/VanDaemon.Web
dotnet run
```

## Project Structure

```
VanDaemon/
├── src/
│   ├── Backend/
│   │   ├── VanDaemon.Api/              # REST API and SignalR hubs
│   │   ├── VanDaemon.Core/             # Domain models and interfaces
│   │   ├── VanDaemon.Application/      # Business logic services
│   │   ├── VanDaemon.Infrastructure/   # Data access and external services
│   │   └── VanDaemon.Plugins/          # Hardware integration plugins
│   │       ├── Abstractions/           # Plugin interfaces
│   │       ├── Simulated/              # Simulated hardware (for testing)
│   │       ├── Modbus/                 # Modbus integration
│   │       ├── I2C/                    # I2C sensor integration
│   │       └── Victron/                # Victron Cerbo integration
│   └── Frontend/
│       └── VanDaemon.Web/              # Blazor WebAssembly application
├── tests/                              # Unit and integration tests
├── docs/                               # Documentation
├── docker/                             # Docker configuration
└── .github/workflows/                  # CI/CD pipelines
```

## Architecture

VanDaemon follows a clean architecture pattern with clear separation of concerns:

- **Frontend**: Blazor WebAssembly with MudBlazor UI components
- **Backend API**: ASP.NET Core Web API with SignalR for real-time updates
- **Plugin System**: Modular hardware integration layer supporting multiple protocols
- **Data Storage**: JSON file storage for configuration, in-memory cache for real-time data

See [PROJECT_PLAN.md](PROJECT_PLAN.md) for detailed architecture documentation.

## Hardware Integration

VanDaemon supports multiple hardware integration methods through its plugin system:

### Supported Plugins

1. **Simulated Plugin** (Default) - For testing and development
2. **Modbus Plugin** - For Modbus TCP/RTU devices
3. **I2C Plugin** - For direct I2C sensor integration
4. **Victron Plugin** - For Victron Cerbo GX integration via MQTT

### Adding a New Plugin

See [docs/deployment/plugin-development.md](docs/deployment/plugin-development.md) for instructions on creating custom hardware plugins.

## Configuration

### System Settings

Configure the system through the web interface Settings page or by editing the configuration files:

- `appsettings.json` - API configuration
- Environment variables - Runtime configuration

### Van Diagram Configuration

Customize the van diagram to match your vehicle:

1. Create an SVG diagram of your van (facing left)
2. Add the diagram to `src/Frontend/VanDaemon.Web/wwwroot/diagrams/`
3. Configure overlay positions in the Settings page

## Deployment

VanDaemon supports multiple deployment options:

### Cloud Deployment (Fly.io)

Deploy VanDaemon to the cloud for remote access as a single combined container:

```bash
# Install Fly.io CLI
curl -L https://fly.io/install.sh | sh

# Login and deploy
flyctl auth login
flyctl apps create vandaemon
flyctl deploy
```

Access your application at:
- **Single URL**: https://vandaemon.fly.dev
  - Web Frontend: `/`
  - API Backend: `/api`
  - SignalR WebSocket: `/hubs`
  - Health Check: `/health`

#### Architecture
The Fly.io deployment uses a single container with:
- **Nginx** (Port 8080) - Serves Blazor WASM static files and proxies API/WebSocket requests
- **.NET API** (Port 5000 internal) - Handles REST API and SignalR connections
- **Supervisor** - Manages both nginx and API processes

#### Key Features
- **Single Endpoint**: All services accessible from one URL
- **WebSocket Support**: Nginx configured for long-lived SignalR connections
- **Health Checks**: Automatic monitoring via `/health` endpoint
- **Auto-Scaling**: Machines stop when idle to save costs
- **GitHub Actions**: Automatic deployment on push to main branch

For detailed instructions including GitHub Actions auto-deployment, see **[DEPLOYMENT.md](DEPLOYMENT.md)**

### Local/Raspberry Pi Deployment

### Step 1: Prepare the Raspberry Pi

```bash
# Update system
sudo apt update && sudo apt upgrade -y

# Install Docker (includes Docker Compose V2)
curl -fsSL https://get.docker.com -o get-docker.sh
sudo sh get-docker.sh

# Add user to docker group
sudo usermod -aG docker $USER
```

### Step 2: Clone and Deploy

```bash
git clone https://github.com/yourusername/vandaemon.git
cd vandaemon
docker compose up -d
```

### Step 3: Configure Hardware Access

For GPIO/I2C access, you may need to:

```bash
# Enable I2C
sudo raspi-config
# Select: Interface Options -> I2C -> Enable

# Add user to i2c group
sudo usermod -aG i2c $USER
```

### Step 4: Set Up Auto-Start

```bash
# Create systemd service
sudo nano /etc/systemd/system/vandaemon.service
```

Add the following content:
```ini
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

Enable and start:
```bash
sudo systemctl enable vandaemon
sudo systemctl start vandaemon
```

## API Documentation

The API is documented with OpenAPI/Swagger. Access the interactive documentation at:
- http://localhost:5000/swagger (when running locally)
- https://vandaemon.fly.dev/swagger (when deployed)

### Key Endpoints

- `GET /api/tanks` - Get all tanks
- `GET /api/tanks/{id}/level` - Get current tank level
- `POST /api/tanks/refresh` - Refresh all tank levels
- `PUT /api/tanks/{id}` - Update tank configuration
- `GET /health` - Health check endpoint (returns status and timestamp)

### Real-Time Updates

SignalR hub for real-time telemetry updates:
- **Local**: ws://localhost:5000/hubs/telemetry
- **Cloud**: wss://vandaemon.fly.dev/hubs/telemetry

The hub broadcasts tank levels, control states, and alerts to all connected clients.

## Development

### Building from Source

```bash
# Build solution
dotnet build VanDaemon.sln

# Run tests
dotnet test VanDaemon.sln

# Run specific project
cd src/Backend/VanDaemon.Api
dotnet run
```

### Running Tests

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

## Roadmap

See [PROJECT_PLAN.md](PROJECT_PLAN.md) for the detailed development roadmap.

## Troubleshooting

### Common Issues

**Issue**: Cannot connect to API from web interface
- Check that API is running on http://localhost:5000
- Verify CORS settings in `appsettings.json`
- Check browser console for errors
- The frontend automatically uses the current host URL (no configuration needed)

**Issue**: Docker containers won't start
- Run `docker compose logs` to check logs
- Ensure ports 5000 and 8080 are not in use
- Verify Docker daemon is running

**Issue**: Sensors not reading values
- Check plugin configuration in Settings
- Verify hardware connections
- Check API logs for errors

**Issue**: SignalR WebSocket connections failing
- Verify health endpoint is accessible: `curl https://your-app.fly.dev/health`
- Check nginx logs for WebSocket upgrade errors
- Ensure nginx is configured with proper WebSocket headers
- Check for firewall blocking WebSocket connections (port 443 for wss://)

**Issue**: Fly.io deployment health checks failing
- Verify the `/health` endpoint returns 200 OK
- Check logs: `flyctl logs --app vandaemon`
- Ensure nginx and API are both running: `flyctl ssh console -C "supervisorctl status"`

**Issue**: Frontend making requests to localhost in production
- This is now fixed automatically - the frontend uses `builder.HostEnvironment.BaseAddress`
- In production, it will use the deployed URL (e.g., https://vandaemon.fly.dev)
- In development, it will use http://localhost:8080

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- [MudBlazor](https://mudblazor.com/) - Material Design components for Blazor
- [NModbus](https://github.com/NModbus/NModbus) - Modbus communication library
- [Victron Energy](https://www.victronenergy.com/) - For their excellent products and documentation

---

Built with ❤️ for the camper van community
