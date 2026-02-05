# PlatformIO Workflows Reference

## Contents
- Development Workflow
- Deployment Workflow
- Troubleshooting Workflow
- Testing with MQTT

## Development Workflow

### Initial Setup

Copy this checklist and track progress:
- [ ] Install PlatformIO CLI or VS Code extension
- [ ] Connect ESP32 via USB
- [ ] Identify serial port: `pio device list`
- [ ] Build firmware: `pio run -e 8ch`
- [ ] Upload: `pio run -e 8ch -t upload`
- [ ] Open monitor: `pio device monitor -e 8ch`

### Code-Build-Test Cycle

```bash
# Terminal 1: Watch and rebuild on changes
cd hw/LEDDimmer/firmware
pio run -e 8ch

# Terminal 2: Monitor serial output
pio device monitor -e 8ch
```

**Iterate until firmware works:**
1. Edit source files in `src/` or `include/`
2. Build: `pio run -e 8ch`
3. If build fails, fix errors and repeat step 2
4. Upload: `pio run -e 8ch -t upload`
5. Monitor: `pio device monitor -e 8ch`
6. Test functionality
7. If issues found, return to step 1

### WiFi Configuration Reset

When device needs reconfiguration:

```bash
# Monitor serial to see status
pio device monitor -e 8ch

# On device: Hold both buttons (GPIO 32 + 33) for 3+ seconds
# Device restarts in AP mode: "VanDaemon-LED-XXXX"
# Connect and configure via captive portal
```

## Deployment Workflow

### USB Deployment

```bash
cd hw/LEDDimmer/firmware

# Clean build for production
pio run -e 8ch -t clean
pio run -e 8ch

# Upload and verify
pio run -e 8ch -t upload
pio device monitor -e 8ch
```

### OTA Deployment

**Prerequisites:**
- Device connected to WiFi
- Device IP known (check router or serial output)

```bash
# Update platformio.ini with device IP
# [env:8ch-ota]
# upload_port = 192.168.1.100

# Deploy via OTA
pio run -e 8ch-ota -t upload
```

**OTA Troubleshooting:**
1. Verify device is reachable: `ping 192.168.1.100`
2. Check device is in correct mode (not AP mode)
3. Ensure firmware has OTA enabled
4. Check firewall allows UDP port 3232

## Troubleshooting Workflow

### Device Not Detected

```bash
# List available devices
pio device list

# Expected output:
# /dev/ttyUSB0 (or COM3 on Windows)
# Hardware ID: USB VID:PID=10C4:EA60

# If not detected:
# - Check USB cable (must be data cable, not charge-only)
# - Install CP210x or CH340 drivers
# - On Linux: add user to dialout group
sudo usermod -aG dialout $USER
# Then logout/login
```

### Upload Fails

```bash
# Error: "Failed to connect to ESP32"
# Solution 1: Hold BOOT button during upload start
pio run -e 8ch -t upload  # Hold BOOT when "Connecting..." appears

# Solution 2: Lower upload speed in platformio.ini
# upload_speed = 115200

# Solution 3: Check port permissions
ls -la /dev/ttyUSB0
# Should show rw for your group
```

### Build Errors

```bash
# Clean and rebuild
pio run -e 8ch -t clean
pio run -e 8ch

# Update platform and libraries
pio pkg update -g  # Global update
pio pkg update     # Project update

# Check library versions
pio pkg list
```

### Memory Issues

```bash
# Check memory usage after build
pio run -e 8ch

# Look for output like:
# RAM:   [====      ]  42.1% (used 138040 bytes from 327680 bytes)
# Flash: [======    ]  63.5% (used 832456 bytes from 1310720 bytes)

# If RAM > 80%, optimize:
# - Reduce String usage
# - Use PROGMEM for constants
# - Check for memory leaks
```

## Testing with MQTT

### Start MQTT Broker

See the **docker** skill for running Mosquitto:

```bash
# From project root
docker compose up -d mqtt

# Or standalone
docker run -d -p 1883:1883 -p 9001:9001 eclipse-mosquitto:2.0
```

### Monitor MQTT Traffic

```bash
# Subscribe to all dimmer topics
mosquitto_sub -h localhost -t "vandaemon/leddimmer/#" -v

# Expected messages when device connects:
# vandaemon/leddimmer/cabin-lights/status online
# vandaemon/leddimmer/cabin-lights/config {"deviceId":"cabin-lights",...}
```

### Test Control Commands

```bash
# Set channel 0 to 50% brightness
mosquitto_pub -h localhost -t "vandaemon/leddimmer/cabin-lights/channel/0/set" -m "128"

# Set all channels off
for i in {0..7}; do
    mosquitto_pub -h localhost -t "vandaemon/leddimmer/cabin-lights/channel/$i/set" -m "0"
done
```

### Integration with VanDaemon

1. Ensure MQTT broker running
2. Flash and configure ESP32 device
3. Start VanDaemon API: `cd src/Backend/VanDaemon.Api && dotnet run`
4. Check API logs for "Device discovered" messages
5. Verify controls appear in web UI at `/controls`

See the **mqttnet** skill for backend MQTT configuration.