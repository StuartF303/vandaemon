# Cerbo GX MQTT Test Tool

A command-line tool for testing and verifying MQTT connectivity with Victron Cerbo GX devices before integrating into VanDaemon.

## Features

- Connect to Cerbo GX MQTT broker
- Auto-discover all MQTT topics or filter by VRM Portal ID
- Color-coded output for different data types (voltage, current, power, tanks, etc.)
- Real-time data display with human-readable formatting
- Automatic topic description lookup for common Victron parameters
- Periodic statistics display (every 30 seconds)
- Support for both raw values and JSON-wrapped payloads

## Prerequisites

- .NET 10 SDK
- Cerbo GX device on the same network
- MQTT broker enabled on Cerbo GX (enabled by default)

## Building

```bash
# From the project root
cd tools/CerboGXTest
dotnet build

# Or from the VanDaemon root
dotnet build tools/CerboGXTest/CerboGXTest.csproj
```

## Usage

### Basic Usage
```bash
# Connect to Cerbo GX at IP address
dotnet run --project tools/CerboGXTest/CerboGXTest.csproj -- 192.168.1.100
```

### Specify Custom MQTT Port
```bash
# Default port is 1883, but you can override it
dotnet run --project tools/CerboGXTest/CerboGXTest.csproj -- 192.168.1.100 1883
```

### Filter by VRM Portal ID
```bash
# Subscribe only to topics for a specific VRM Portal ID
dotnet run --project tools/CerboGXTest/CerboGXTest.csproj -- 192.168.1.100 1883 c0619ab22c89
```

## Finding Your Cerbo GX IP Address

### Method 1: Via Cerbo GX Display
1. On the Cerbo GX touchscreen, go to **Settings** → **Ethernet** (or **Wi-Fi**)
2. Note the IP address shown

### Method 2: Via Router Admin Panel
1. Log into your router's admin interface
2. Look for connected devices
3. Find the device named "Venus" or "Cerbo GX"

### Method 3: Network Scan
```bash
# Linux/Mac
arp -a | grep -i victron

# Windows
arp -a
```

## Understanding the Output

### Color Coding
- **Cyan**: State of Charge (battery level)
- **Yellow**: Voltage measurements
- **Magenta**: Current measurements
- **Green**: Power measurements
- **Blue**: Tank levels
- **Red**: Temperature readings
- **Dark Yellow**: State/Mode information
- **White**: Other parameters

### Example Output
```
===========================================
   Victron Cerbo GX MQTT Test Tool
===========================================

Cerbo GX IP:      192.168.1.100
MQTT Port:        1883
VRM Portal ID:    (auto-discover)

Press Ctrl+C to exit
===========================================

[CONNECTING] Attempting to connect to 192.168.1.100:1883...
[CONNECTED] Successfully connected to Cerbo GX at 192.168.1.100:1883

[SUBSCRIBE] Subscribing to N/# (all devices)

[14:23:15] N/c0619ab22c89/battery/258/Soc
  Description: State of Charge (%)
  Value: 85.3 %

[14:23:16] N/c0619ab22c89/battery/258/Dc/0/Voltage
  Description: Battery Voltage (V)
  Value: 13.45 V

[14:23:17] N/c0619ab22c89/tank/24/Level
  Description: Fresh Water Tank Level (%)
  Value: 67.5 %
```

### Statistics Display
Every 30 seconds, the tool displays connection statistics:
```
=== STATISTICS ===
Uptime:              00:05:30
Total Messages:      1,234
Messages/Second:     3.74
Unique Topics:       87

Recent Topics:
  N/c0619ab22c89/battery/258/Soc = 85.3
  N/c0619ab22c89/battery/258/Dc/0/Voltage = 13.45
  ...
```

## Common MQTT Topics

The Cerbo GX publishes data using a hierarchical topic structure:

```
N/{portal-id}/{device-type}/{instance}/{parameter}
```

### Battery Topics
- `N/{portal-id}/battery/{instance}/Soc` - State of Charge (%)
- `N/{portal-id}/battery/{instance}/Dc/0/Voltage` - Battery Voltage (V)
- `N/{portal-id}/battery/{instance}/Dc/0/Current` - Battery Current (A)
- `N/{portal-id}/battery/{instance}/Dc/0/Power` - Battery Power (W)
- `N/{portal-id}/battery/{instance}/Dc/0/Temperature` - Temperature (°C)

### Tank Topics
- `N/{portal-id}/tank/{instance}/Level` - Tank Level (%)
- `N/{portal-id}/tank/{instance}/Capacity` - Tank Capacity (m³)
- `N/{portal-id}/tank/{instance}/FluidType` - Fluid Type (0=Fuel, 1=Water, 2=Grey, 3=LPG, etc.)

### Solar Charger Topics
- `N/{portal-id}/solarcharger/{instance}/Yield/Power` - Current Solar Power (W)
- `N/{portal-id}/solarcharger/{instance}/Pv/V` - Panel Voltage (V)
- `N/{portal-id}/solarcharger/{instance}/Pv/I` - Panel Current (A)

### System Topics
- `N/{portal-id}/system/0/Dc/Battery/Voltage` - System Battery Voltage
- `N/{portal-id}/system/0/Dc/Battery/Current` - System Battery Current
- `N/{portal-id}/system/0/Dc/Battery/Power` - System Battery Power

## Troubleshooting

### Connection Timeout
- Verify the Cerbo GX IP address is correct
- Ensure your computer and Cerbo GX are on the same network
- Check firewall settings (allow port 1883)
- Verify MQTT is enabled on Cerbo GX: **Settings** → **Services** → **MQTT on LAN**

### No Data Received
- Check if MQTT broker is enabled: **Settings** → **Services** → **MQTT on LAN** (should be ON)
- Verify the VRM Portal ID is correct (if specified)
- Try without specifying Portal ID to see all topics
- Check Cerbo GX logs for errors

### Finding VRM Portal ID
1. On Cerbo GX: **Settings** → **VRM Portal** → **VRM Portal ID**
2. Via MQTT: Connect without Portal ID filter and observe topic prefixes

## Next Steps

Once you've verified connectivity and identified the topics you need:

1. Note the MQTT topic structure for tanks and controls
2. Document the VRM Portal ID format
3. Test the data refresh rate (usually 1-5 seconds)
4. Identify which topics are relevant for VanDaemon integration
5. Use this information to implement the `VictronSensorPlugin` in VanDaemon

## References

- [Victron MQTT Documentation](https://github.com/victronenergy/dbus-mqtt)
- [Cerbo GX Manual](https://www.victronenergy.com/live/cerbo-gx:start)
- [Venus OS Data Paths](https://github.com/victronenergy/venus/wiki/dbus-API)
