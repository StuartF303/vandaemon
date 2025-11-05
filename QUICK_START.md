# VanDaemon - Quick Start Guide

## 🚀 What's Been Built

VanDaemon is now a **fully functional** real-time camper van control system with:

### Backend Features ✅
- **Tank Monitoring**: Real-time monitoring of Fresh Water, Waste Water, LPG
- **Control System**: Lights, Dimmers, Water Pump, Heater controls
- **Alert System**: Automatic alerts for low/high tank levels
- **Settings Management**: Configurable thresholds and van diagrams
- **SignalR Hub**: WebSocket real-time updates (auto-refresh every 5s)
- **Background Service**: Continuous monitoring and alert checking
- **Simulated Hardware**: Test without actual sensors

### Frontend Features ✅
- **Dashboard**: Real-time tank levels with live connection status
- **Tanks Page**: Detailed monitoring with manual refresh
- **Controls Page**: Interactive switches and dimmers
- **Settings Page**: System configuration
- **Real-Time Updates**: Auto-updating UI via WebSocket
- **Responsive Design**: Works on mobile, tablet, desktop

### API Endpoints ✅
```
Tanks:     GET/POST/PUT/DELETE /api/tanks
           POST /api/tanks/refresh
           GET /api/tanks/{id}/level

Controls:  GET/POST/PUT/DELETE /api/controls
           POST /api/controls/{id}/state

Alerts:    GET/POST/DELETE /api/alerts
           POST /api/alerts/check
           POST /api/alerts/{id}/acknowledge

Settings:  GET/PUT /api/settings
           GET /api/settings/van-diagrams

SignalR:   /hubs/telemetry (WebSocket)
```

## 📦 Running the Application

### Option 1: Docker (Recommended)

```bash
# Navigate to project
cd vandaemon/docker

# Start all services
docker compose up -d

# Watch logs
docker compose logs -f

# Stop services
docker compose down
```

**Access Points:**
- **Web UI**: http://localhost:8080
- **API**: http://localhost:5000
- **Swagger Docs**: http://localhost:5000/swagger
- **SignalR Hub**: ws://localhost:5000/hubs/telemetry

### Option 2: Development Mode

**Terminal 1 - API:**
```bash
cd src/Backend/VanDaemon.Api
dotnet run
```

**Terminal 2 - Web UI:**
```bash
cd src/Frontend/VanDaemon.Web
dotnet run
```

## 🧪 Testing the System

### 1. View Dashboard
- Open http://localhost:8080
- See 3 default tanks with current levels
- Watch "Real-time updates active" indicator
- Tank levels update automatically every 5 seconds

### 2. Test Tank Monitoring
- Navigate to **Tanks** page
- Click "Refresh All Tanks" button
- See detailed view with capacity, thresholds, warnings
- Watch levels change in real-time

### 3. Test Controls
- Navigate to **Controls** page
- Toggle "Main Lights" switch (turns on/off)
- Adjust "Dimmer Lights" slider (0-100%)
- Control "Water Pump" and "Heater"
- Changes happen instantly via SignalR

### 4. Configure Settings
- Navigate to **Settings** page
- Change van model (5 options available)
- Adjust alert thresholds (Low/High)
- Enable/disable audio alerts
- Click "Save Settings"

### 5. Test Real-Time Updates
- Open Dashboard in **two browser windows**
- In one window, go to Tanks and click "Refresh All"
- Watch **both windows** update simultaneously
- This demonstrates WebSocket real-time sync!

### 6. Test API Directly

```bash
# Get all tanks
curl http://localhost:5000/api/tanks

# Get specific tank level
curl http://localhost:5000/api/tanks/{tank-id}/level

# Refresh all tanks
curl -X POST http://localhost:5000/api/tanks/refresh

# Get all controls
curl http://localhost:5000/api/controls

# Set control state (turn on lights)
curl -X POST http://localhost:5000/api/controls/{control-id}/state \
  -H "Content-Type: application/json" \
  -d '{"state": true}'

# Get alerts
curl http://localhost:5000/api/alerts

# Get settings
curl http://localhost:5000/api/settings
```

## 🎯 What to Expect

### Default Data
- **3 Tanks**: Fresh Water (75%), Waste Water (25%), LPG (60%)
- **4 Controls**: Main Lights, Dimmer, Water Pump, Heater
- **Simulated Sensors**: Levels slowly change over time
- **Automatic Alerts**: Generated when thresholds breached

### Real-Time Behavior
- Dashboard updates every **5 seconds** automatically
- Tank levels slowly drift (simulating usage)
- Fresh water/LPG decrease over time
- Waste water increases over time
- Alerts appear when tanks reach thresholds

### Connection Status
- Green "Connected" badge = SignalR active
- "Real-time updates active" text appears
- Last update timestamp shows sync time
- Auto-reconnect if connection drops

## 📊 Testing Scenarios

### Scenario 1: Low Tank Alert
1. Go to Settings, set "Low Level Threshold" to 80%
2. Save settings
3. Wait for background service to check (5s intervals)
4. Check Alerts API: `curl http://localhost:5000/api/alerts`
5. Should see warning for Fresh Water (currently at 75%)

### Scenario 2: High Waste Tank Alert
1. The simulated Waste Water slowly increases
2. When it reaches 90%, alert automatically generated
3. View in Dashboard (if alert component added)
4. Acknowledge via: `POST /api/alerts/{id}/acknowledge`

### Scenario 3: Multi-Client Sync
1. Open http://localhost:8080 in Chrome
2. Open http://localhost:8080 in Firefox
3. In Chrome, toggle a control
4. Watch Firefox update **instantly**
5. Both clients share real-time state!

## 🐛 Troubleshooting

### Port Already in Use
```bash
# Check what's using port 5000
sudo lsof -i :5000

# Or use different ports in docker-compose.yml
```

### SignalR Not Connecting
- Check CORS settings in Program.cs
- Verify API is running: `curl http://localhost:5000/api/tanks`
- Check browser console for WebSocket errors
- Ensure firewall allows WebSocket connections

### Docker Build Fails
```bash
# Clean and rebuild
docker compose down -v
docker compose build --no-cache
docker compose up -d
```

### Frontend Can't Reach API
- Check `appsettings.json` has correct API URL
- Verify API is accessible: `curl http://localhost:5000/api/tanks`
- Check browser network tab for CORS errors

## 📈 Next Steps

### Extend the System
1. **Add Real Hardware**:
   - Implement Modbus plugin for actual sensors
   - Connect I2C sensors to Raspberry Pi
   - Integrate Victron Cerbo GX

2. **Enhance UI**:
   - Add interactive SVG van diagram
   - Implement alert notifications panel
   - Add historical charts/graphs
   - Create mobile-optimized layouts

3. **Add Features**:
   - User authentication/authorization
   - Data logging and history
   - Export functionality
   - Voice control integration

4. **Deploy to Production**:
   - Follow `docs/deployment/raspberry-pi-setup.md`
   - Set up HTTPS with Let's Encrypt
   - Configure systemd auto-start
   - Set up monitoring/alerts

## 📝 System Architecture

```
Browser (Port 8080)
    ↓ HTTP/WebSocket
Nginx Reverse Proxy
    ↓
Blazor WebAssembly App
    ↓ REST API / SignalR
.NET API (Port 5000)
    ↓
Background Service (5s polling)
    ↓
Simulated Plugins
    ↓
(Future: Real Hardware)
```

## 🎉 Success Criteria

You'll know it's working when:
- ✅ Dashboard shows 3 tanks with percentages
- ✅ "Connected" badge is green
- ✅ "Real-time updates active" appears
- ✅ Last update timestamp changes every 5s
- ✅ Controls toggle immediately
- ✅ Swagger docs load at /swagger
- ✅ Multiple browsers stay in sync

## 💡 Tips

- **Use Swagger**: Best way to explore/test API
- **Watch Logs**: `docker compose logs -f api`
- **Browser DevTools**: Network tab shows SignalR messages
- **Test in Incognito**: Verify multi-client sync
- **Mobile Test**: Works great on phones/tablets!

---

**Built with .NET 8, Blazor WebAssembly, SignalR, and MudBlazor**

For detailed documentation, see:
- [PROJECT_PLAN.md](PROJECT_PLAN.md) - Architecture details
- [README.md](README.md) - Full documentation
- [docs/](docs/) - Deployment guides

Happy van monitoring! 🚐✨
