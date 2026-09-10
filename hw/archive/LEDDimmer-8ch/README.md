# MQTT LED Dimmer - VanDaemon Integration

## Overview

The MQTT LED Dimmer is an ESP32-based 8-channel PWM LED controller that integrates with the VanDaemon control system via MQTT. It supports multiple devices on the network, automatic discovery, and real-time state synchronization.

## Features

- **8-Channel PWM Control**: Individual brightness control (0-255) for each channel
- **MQTT Communication**: Bidirectional control and state reporting
- **WiFi Configuration**: Captive portal setup via WiFiManager
- **Auto-Discovery**: Automatic device registration in VanDaemon
- **State Persistence**: NVS storage for channel states across reboots
- **Status LED**: WS2812 RGB LED for visual device status feedback
- **OTA Updates**: Over-the-air firmware updates support
- **Multi-Device Support**: Multiple LED dimmers on the same network

## Hardware

### Supported Variants

- **4-Channel**: 4 PWM outputs (reduced version)
- **8-Channel**: Full 8 PWM outputs (default)

### Pin Configuration (ESP32)

```cpp
// PWM Outputs (8 channels)
GPIO 25, 26, 27, 14, 4, 5, 18, 19

// Status LED (WS2812)
GPIO 16

// Buttons
GPIO 32 (Button 1) - Fade demo
GPIO 33 (Button 2) - Toggle all channels
Hold both 3+ seconds - Reset WiFi
```

### Schematic

See `hw/LEDDimmer/schematic/` for complete circuit diagrams.

## Firmware

### Building and Uploading

```bash
cd hw/LEDDimmer/firmware

# Build for 8-channel variant (default)
pio run -e 8ch

# Upload via USB
pio run -e 8ch -t upload

# Build for 4-channel variant
pio run -e 4ch -t upload

# Build with OTA support
pio run -e 8ch-ota -t upload
```

### WiFi Configuration

1. Power on the device
2. Look for WiFi network: `VanDaemon-LED-XXXX`
3. Connect with password: `vandaemon`
4. Configure your WiFi credentials in the captive portal
5. Device will auto-connect and appear in VanDaemon

### MQTT Topic Structure

The device communicates using the following MQTT topic hierarchy:

**Base Topic**: `vandaemon/leddimmer/`

#### Device Topics

| Topic | Direction | Description | Payload |
|-------|-----------|-------------|---------|
| `{base}/{deviceId}/status` | Publish | Device online/offline status | `online` or `offline` |
| `{base}/{deviceId}/config` | Publish | Device configuration | JSON object |
| `{base}/{deviceId}/heartbeat` | Publish | Periodic heartbeat | JSON object |

#### Channel Topics

| Topic | Direction | Description | Payload |
|-------|-----------|-------------|---------|
| `{base}/{deviceId}/channel/{N}/state` | Publish | Current brightness (0-255) | Integer |
| `{base}/{deviceId}/channel/{N}/set` | Subscribe | Set brightness command | Integer or JSON |

**Example deviceId**: `cabin-lights`, `galley-leds`, `bedroom-dimmer`

### Configuration Message

Published to `vandaemon/leddimmer/{deviceId}/config` on startup:

```json
{
  "deviceId": "cabin-lights",
  "deviceName": "Cabin LED Dimmer",
  "channels": 8,
  "version": "1.0.0",
  "variant": "8CH"
}
```

### Heartbeat Message

Published to `vandaemon/leddimmer/{deviceId}/heartbeat` every 60 seconds:

```json
{
  "uptime": 3600,
  "freeHeap": 245000,
  "rssi": -65
}
```

### Control Commands

**Set Channel Brightness** (Integer format):
```
Topic: vandaemon/leddimmer/cabin-lights/channel/0/set
Payload: 128
```

**Set Channel Brightness** (JSON format):
```
Topic: vandaemon/leddimmer/cabin-lights/channel/0/set
Payload: {"brightness": 128}
```

## VanDaemon Backend Integration

### Architecture

The integration consists of two main components:

1. **MqttLedDimmerPlugin** (`src/Backend/VanDaemon.Plugins/VanDaemon.Plugins.MqttLedDimmer/MqttLedDimmerPlugin.cs`)
   - Implements `IControlPlugin` interface
   - Manages MQTT connection to broker
   - Handles device discovery via MQTT messages
   - Processes control commands and state updates

2. **MqttLedDimmerService** (`src/Backend/VanDaemon.Plugins/VanDaemon.Plugins.MqttLedDimmer/MqttLedDimmerService.cs`)
   - Background service that runs continuously
   - Discovers new devices every 10 seconds
   - Refreshes control states every 5 seconds
   - Automatically registers Control entities in VanDaemon

### Configuration

Edit `src/Backend/VanDaemon.Api/appsettings.json`:

```json
{
  "MqttLedDimmer": {
    "MqttBroker": "localhost",
    "MqttPort": 1883,
    "MqttUsername": "",
    "MqttPassword": "",
    "BaseTopic": "vandaemon/leddimmer",
    "AutoDiscovery": true,
    "Devices": []
  }
}
```

**Configuration Options**:

- `MqttBroker`: MQTT broker hostname or IP (default: `localhost`)
- `MqttPort`: MQTT broker port (default: `1883`)
- `MqttUsername`: MQTT authentication username (optional)
- `MqttPassword`: MQTT authentication password (optional)
- `BaseTopic`: Base MQTT topic prefix (default: `vandaemon/leddimmer`)
- `AutoDiscovery`: Enable automatic device discovery (default: `true`)
- `Devices`: Pre-configured device list (empty for auto-discovery)

### Control Entity Registration

When a device is discovered, the service automatically creates Control entities:

- **Type**: `ControlType.Dimmer`
- **Name**: `{DeviceName} - Channel {N}`
- **State**: Current brightness (0-255)
- **Icon**: `mdi-lightbulb-outline`
- **Plugin**: `MqttLedDimmer`
- **ControlId**: `{deviceId}-CH{N}` (e.g., `cabin-lights-CH0`)

### Docker Deployment

The MQTT broker is included in the Docker Compose setup:

```yaml
# docker/docker-compose.yml
mqtt:
  image: eclipse-mosquitto:2.0
  ports:
    - "1883:1883"      # MQTT
    - "9001:9001"      # WebSocket
  volumes:
    - ./mosquitto/config:/mosquitto/config
    - mqtt-data:/mosquitto/data
```

Start services:
```bash
cd docker
docker compose up -d
```

## Status LED Indicators

The WS2812 RGB LED provides visual feedback:

| Color | Status |
|-------|--------|
| Blue (slow pulse) | WiFi connecting |
| Cyan (slow pulse) | MQTT connecting |
| Green (solid) | Connected and operational |
| Red (fast blink) | Error state |
| Yellow (solid) | Configuration mode |

## Button Functions

- **Button 1 (GPIO 32)**:
  - Short press: Run fade demo on all channels

- **Button 2 (GPIO 33)**:
  - Short press: Toggle all channels on/off

- **Both buttons (3+ seconds)**:
  - Reset WiFi credentials and restart in AP mode

## Troubleshooting

### Device Not Appearing in VanDaemon

1. **Check MQTT broker is running**:
   ```bash
   docker ps | grep mqtt
   ```

2. **Check device WiFi connection**:
   - Verify status LED is green
   - Check router/access point for device IP

3. **Check MQTT messages**:
   ```bash
   # Subscribe to all LED dimmer topics
   mosquitto_sub -h localhost -t "vandaemon/leddimmer/#" -v
   ```

4. **Check VanDaemon logs**:
   ```bash
   docker logs vandaemon-api
   ```
   Look for "Device discovered" or "MQTT connection" messages

### Channel Not Responding

1. **Check control state in VanDaemon dashboard**
2. **Publish test command**:
   ```bash
   mosquitto_pub -h localhost -t "vandaemon/leddimmer/cabin-lights/channel/0/set" -m "255"
   ```

3. **Check device serial monitor** (if connected via USB):
   ```bash
   pio device monitor -e 8ch
   ```

### WiFi Connection Issues

1. **Reset WiFi credentials**:
   - Hold both buttons for 3+ seconds
   - Device will restart in AP mode

2. **Check WiFi signal strength**:
   - Heartbeat message includes RSSI value
   - Values below -80 dBm indicate weak signal

### MQTT Authentication

If your broker requires authentication:

1. Update firmware `config.h`:
   ```cpp
   #define MQTT_USERNAME   "your-username"
   #define MQTT_PASSWORD   "your-password"
   ```

2. Update VanDaemon `appsettings.json`:
   ```json
   "MqttUsername": "your-username",
   "MqttPassword": "your-password"
   ```

## Development

### Firmware Development

```bash
cd hw/LEDDimmer/firmware

# Monitor serial output
pio device monitor -e 8ch

# Clean build
pio run -e 8ch -t clean
pio run -e 8ch

# Upload filesystem (if needed)
pio run -e 8ch -t uploadfs
```

### Testing MQTT Communication

**Subscribe to all topics**:
```bash
mosquitto_sub -h localhost -t "vandaemon/leddimmer/#" -v
```

**Simulate device online**:
```bash
mosquitto_pub -h localhost -t "vandaemon/leddimmer/test-device/status" -m "online"
```

**Simulate config message**:
```bash
mosquitto_pub -h localhost -t "vandaemon/leddimmer/test-device/config" \
  -m '{"deviceId":"test-device","deviceName":"Test Dimmer","channels":8,"version":"1.0.0"}'
```

**Set channel brightness**:
```bash
mosquitto_pub -h localhost -t "vandaemon/leddimmer/test-device/channel/0/set" -m "128"
```

## Future Enhancements

- [ ] Web-based configuration interface
- [ ] Custom PWM frequency configuration
- [ ] Smooth transitions with configurable duration
- [ ] Scene/preset support
- [ ] Integration with VanDaemon automation rules
- [ ] MQTT TLS/SSL support
- [ ] HomeAssistant MQTT Discovery protocol support
- [ ] Current/power monitoring for channels

## License

Part of the VanDaemon project.

## Support

For issues and questions:
- Check logs: `docker logs vandaemon-api`
- MQTT monitoring: `mosquitto_sub -h localhost -t "vandaemon/leddimmer/#" -v`
- Serial monitor: `pio device monitor -e 8ch`
