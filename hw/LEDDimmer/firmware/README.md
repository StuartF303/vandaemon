# VanDaemon LED Dimmer Firmware

ESP32-based 8-channel PWM LED controller with MQTT integration for VanDaemon camper van control system.

## Features

- **8 Independent PWM Channels** (or 4-channel variant)
- **WiFi Connectivity** with WPS/captive portal setup
- **MQTT Integration** for VanDaemon control
- **WS2812 Status LED** for visual feedback
- **2 Physical Buttons** for local control
- **Persistent State** saved to ESP32 NVS
- **Smooth Transitions** (optional fade effects)
- **OTA Updates** (optional, for remote firmware updates)

## Hardware Requirements

- ESP32-WROOM-32 development board or custom PCB
- 8-channel LED dimmer board (see `../` for schematics)
- LED strips or lighting loads
- 12-24V DC power supply

## Development Environment

### PlatformIO (Recommended)

1. **Install PlatformIO:**
   - VS Code: Install "PlatformIO IDE" extension
   - Or standalone: https://platformio.org/install/cli

2. **Open Project:**
   ```bash
   cd hw/LEDDimmer/firmware
   pio run  # Build
   ```

3. **Build for 4-channel variant:**
   ```bash
   pio run -e 4ch
   ```

4. **Upload to ESP32:**
   ```bash
   pio run -t upload
   ```

5. **Monitor serial output:**
   ```bash
   pio device monitor
   ```

### Arduino IDE (Alternative)

1. Install ESP32 board support
2. Install libraries:
   - Adafruit NeoPixel
   - PubSubClient
   - WiFiManager
   - ArduinoJson
3. Open `src/main.cpp`
4. Select "ESP32 Dev Module" board
5. Upload

## First Time Setup

### 1. Flash Firmware

```bash
pio run -t upload
```

### 2. WiFi Configuration

On first boot, the device creates a WiFi access point:

- **SSID:** `VanDaemon-LEDDimmer`
- **Password:** `vandaemon123`

Connect to this AP and configure your WiFi credentials via the captive portal.

### 3. MQTT Configuration

The device stores MQTT broker settings in NVS. You can configure via:

**Option A: Serial Commands** (not yet implemented in base firmware)

**Option B: MQTT Discovery**
- Device will use default broker from VanDaemon network
- Or manually configure via serial/web interface

**Option C: Code Configuration**
Edit `src/main.cpp` and add during setup:
```cpp
mqtt_setConfig("192.168.1.100", 1883, "username", "password");
```

## MQTT Topics

The device uses the following topic structure:

```
vandaemon/leddimmer/{deviceId}/status           → "online"/"offline" (LWT)
vandaemon/leddimmer/{deviceId}/config           → Device capabilities (JSON)
vandaemon/leddimmer/{deviceId}/heartbeat        → Periodic health status
vandaemon/leddimmer/{deviceId}/channel/0/set    ← Command: Set channel 0 (0-255)
vandaemon/leddimmer/{deviceId}/channel/0/state  → State: Channel 0 brightness
vandaemon/leddimmer/{deviceId}/all/set          ← Command: Set all channels
```

### Example MQTT Commands

**Set channel 0 to 50% brightness:**
```bash
mosquitto_pub -t "vandaemon/leddimmer/leddimmer-abc123/channel/0/set" -m "128"
```

**Set all channels to max:**
```bash
mosquitto_pub -t "vandaemon/leddimmer/leddimmer-abc123/all/set" -m "255"
```

**JSON format (alternative):**
```bash
mosquitto_pub -t "vandaemon/leddimmer/leddimmer-abc123/channel/0/set" \
              -m '{"brightness": 128}'
```

## Button Functions

- **Button 1:** Run fade demo (all channels fade sequentially)
- **Button 2:** Toggle all channels ON/OFF (50% brightness)
- **Both buttons (hold 3s):** Reset WiFi credentials and restart

## Status LED Colors

| Color | Meaning |
|-------|---------|
| Green | Ready/idle |
| Blue | Active/working |
| Red | Error |
| Purple | WiFi setup mode |
| Cyan | MQTT connecting |
| Yellow | Button pressed |

## Configuration Options

Edit `include/config.h` or set via `platformio.ini` build flags:

```cpp
#define NUM_CHANNELS 8          // 4 or 8 channels
#define PWM_FREQ 5000           // PWM frequency (Hz)
#define ENABLE_SERIAL           // Enable serial debug output
#define ENABLE_TRANSITIONS      // Enable smooth fading
#define ENABLE_OTA              // Enable OTA updates
```

## Build Variants

Configured in `platformio.ini`:

- **4ch:** 4-channel variant
- **8ch:** 8-channel variant (default)
- **8ch-ota:** 8-channel with OTA enabled

```bash
# Build specific variant
pio run -e 4ch
pio run -e 8ch-ota
```

## Pin Mapping

| Function | GPIO | Notes |
|----------|------|-------|
| WS2812 Status LED | 16 | Addressable RGB |
| Button 1 | 32 | Active LOW, internal pull-up |
| Button 2 | 33 | Active LOW, internal pull-up |
| PWM Channel 1 | 25 | |
| PWM Channel 2 | 26 | |
| PWM Channel 3 | 27 | |
| PWM Channel 4 | 14 | |
| PWM Channel 5 | 4 | 8-channel only |
| PWM Channel 6 | 5 | 8-channel only |
| PWM Channel 7 | 18 | 8-channel only |
| PWM Channel 8 | 19 | 8-channel only |

## State Persistence

Channel states are automatically saved to ESP32 NVS and restored on boot.

To manually save:
```cpp
pwm_saveState();
```

To load:
```cpp
pwm_loadState();
```

## OTA Updates

If `ENABLE_OTA` is defined, you can update firmware wirelessly:

```bash
# Build and upload via OTA
pio run -e 8ch-ota -t upload --upload-port leddimmer.local
```

Default OTA password: `vandaemon`

## Troubleshooting

### Device won't connect to WiFi
- Hold both buttons for 3 seconds to reset credentials
- Device will restart in AP mode

### MQTT not working
- Check broker address in configuration
- Verify network connectivity
- Check MQTT logs on broker side

### Channels not responding
- Verify GPIO connections
- Check PWM frequency (some LEDs need higher freq)
- Test with fade demo (Button 1)

### Status LED shows red
- WiFi connection failed
- MQTT broker unreachable
- Check serial output for error details

## Serial Debug Output

Connect via USB and monitor at 115200 baud:

```bash
pio device monitor
# Or
screen /dev/ttyUSB0 115200
```

## Integration with VanDaemon

See main VanDaemon documentation for:
- MQTT broker setup
- Backend plugin configuration
- Dashboard UI integration
- Multiple device management

## License

Part of the VanDaemon project.

## Version History

- **1.0.0** - Initial release with MQTT integration
