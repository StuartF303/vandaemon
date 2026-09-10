# 8-Channel LED Dimmer Controller
## Specification & Requirements Document

**Document Version:** 1.0  
**Date:** January 2025  
**Project Code:** VANDIMMER-8CH

---

## 1. Overview

### 1.1 Purpose
A standalone 8-channel PWM LED dimmer controller for 12-24V LED strips, providing individual brightness control per channel with status indication and local button inputs.

### 1.2 Target Applications
- RV/Campervan interior lighting
- Cabinet and under-counter lighting
- Architectural accent lighting
- Home automation lighting zones

---

## 2. Electrical Specifications

### 2.1 Power Input

| Parameter | Min | Typical | Max | Unit |
|-----------|-----|---------|-----|------|
| Input Voltage | 10 | 12-24 | 30 | VDC |
| Quiescent Current (idle) | - | 50 | 80 | mA |
| Max Current (all channels full) | - | - | 12 | A |

**Requirements:**
- Reverse polarity protection (P-channel MOSFET or Schottky diode)
- Input capacitance: minimum 100µF electrolytic + 100nF ceramic
- Screw terminal connector, minimum 2.5mm² wire capacity

### 2.2 Output Channels

| Parameter | Min | Typical | Max | Unit |
|-----------|-----|---------|-----|------|
| Channels | 8 | 8 | 8 | - |
| Current per Channel | 0 | - | 1.5 | A |
| Total Output Current | 0 | - | 10 | A |
| PWM Frequency | 1 | 5 | 20 | kHz |
| PWM Resolution | 8 | 8 | 16 | bits |
| Output Voltage Drop | - | 50 | 100 | mV |

**Requirements:**
- Low-side N-channel MOSFET switching
- Logic-level gate drive (Vgs(th) < 2.5V at 3.3V drive)
- Gate resistor: 100Ω (limits di/dt, reduces EMI)
- Gate pull-down resistor: 10KΩ (ensures OFF state during boot)
- Screw terminal connectors for each channel output
- LED- connects to MOSFET drain, LED+ connects to VIN

### 2.3 Digital Inputs

| Parameter | Value | Unit |
|-----------|-------|------|
| Number of Inputs | 2 | - |
| Input Voltage (logic high) | 3.3 | V |
| Input Voltage (logic low) | 0 | V |
| Pull-up Resistance | 10K | Ω |
| Debounce Time | 50 | ms |

**Requirements:**
- Active LOW configuration (pressed = 0V, released = 3.3V)
- External 10K pull-up resistors to 3.3V rail
- 2-pin connectors (signal + GND) for each input
- Software debouncing required

### 2.4 Status Indicator

| Parameter | Value |
|-----------|-------|
| Type | WS2812B addressable RGB LED |
| Quantity | 1 |
| Supply Voltage | 5V (acceptable range 3.5-5.3V) |
| Data Protocol | Single-wire NRZ |

**Requirements:**
- 100nF decoupling capacitor adjacent to LED VDD/GND
- Operating from 3.3V rail acceptable (reduced brightness)
- Data pin requires 3.3V logic level (no level shifting needed)

### 2.5 Voltage Regulator

| Parameter | Value | Unit |
|-----------|-------|------|
| Output Voltage | 3.3 | V |
| Output Current Capacity | 800 | mA min |
| Dropout Voltage | 1.0 | V max |
| Input Voltage Range | 4.5 - 15 | V |

**Requirements:**
- AMS1117-3.3 or equivalent LDO
- Input capacitor: 10µF minimum
- Output capacitor: 10µF + 100nF
- Thermal pad connection to ground plane if available

**Note:** If VIN > 15V, use a DC-DC buck converter (e.g., MP1584) to step down to 5-12V before the LDO, or use a wide-input LDO.

---

## 3. Controller Specification

### 3.1 Microcontroller

| Parameter | Requirement |
|-----------|-------------|
| Module | ESP32-WROOM-32 or ESP32-WROOM-32E |
| Clock Speed | 240 MHz |
| Flash | 4 MB minimum |
| RAM | 520 KB |
| Operating Voltage | 3.3V |

### 3.2 GPIO Assignments

| Function | GPIO | Notes |
|----------|------|-------|
| PWM Channel 1 | GPIO25 | LEDC peripheral |
| PWM Channel 2 | GPIO26 | LEDC peripheral |
| PWM Channel 3 | GPIO27 | LEDC peripheral |
| PWM Channel 4 | GPIO14 | LEDC peripheral |
| PWM Channel 5 | GPIO4 | LEDC peripheral |
| PWM Channel 6 | GPIO5 | LEDC peripheral |
| PWM Channel 7 | GPIO18 | LEDC peripheral |
| PWM Channel 8 | GPIO19 | LEDC peripheral |
| WS2812 Data | GPIO16 | RMT peripheral |
| Button 1 | GPIO32 | ADC1 compatible |
| Button 2 | GPIO33 | ADC1 compatible |
| TX (debug) | GPIO1 | UART0 |
| RX (debug) | GPIO3 | UART0 |

**Reserved/Avoided GPIOs:**
- GPIO0: Boot mode select (avoid or use with care)
- GPIO2: Boot mode select (avoid or use with care)
- GPIO12: Boot voltage select (must be LOW at boot)
- GPIO6-11: Connected to internal flash (do not use)
- GPIO34-39: Input only (no internal pull-up)

### 3.3 Programming Interface

| Parameter | Requirement |
|-----------|-------------|
| Interface | USB-UART via external programmer or on-board USB-C |
| Auto-reset | DTR/RTS to EN/GPIO0 circuit |
| Baud Rate | 115200 (programming), 921600 (optional) |

**Requirements:**
- 6-pin programming header (3.3V, GND, TX, RX, EN, GPIO0)
- OR on-board USB-UART bridge (CP2102, CH340, or ESP32-S3 native USB)

---

## 4. Software Specification

### 4.1 Development Environment

| Parameter | Requirement |
|-----------|-------------|
| Framework | Arduino or ESP-IDF |
| Arduino Core | ESP32 Arduino Core 3.x |
| IDE | Arduino IDE 2.x, PlatformIO, or VS Code |

### 4.2 Core Functions

#### 4.2.1 PWM Control
```
- Initialize 8 LEDC channels at specified frequency and resolution
- Set individual channel brightness (0-255 for 8-bit, 0-65535 for 16-bit)
- Set all channels simultaneously
- Smooth fade transitions (configurable rate)
- Gamma correction lookup table (optional)
```

#### 4.2.2 Button Input
```
- Debounced reading with 50ms minimum debounce
- Detect press, release, long-press (>1 second)
- Configurable actions per button
- Interrupt-driven or polled (developer choice)
```

#### 4.2.3 Status LED
```
- Indicate power-on state (steady green)
- Indicate WiFi connecting (slow blue blink)
- Indicate WiFi connected (steady blue)
- Indicate error condition (red)
- Indicate button press acknowledgement (yellow flash)
- Indicate OTA update in progress (purple pulse)
```

#### 4.2.4 Configuration Storage
```
- Store channel brightness levels in NVS (non-volatile storage)
- Store WiFi credentials in NVS
- Store user preferences (default levels, fade rate, etc.)
- Factory reset capability (hold both buttons for 10 seconds)
```

### 4.3 Communication Interfaces

#### 4.3.1 WiFi (Optional Feature)
```
- Station mode for home network connection
- AP mode for initial configuration
- mDNS for device discovery (e.g., dimmer.local)
```

#### 4.3.2 MQTT (Optional Feature)
```
- Connect to configurable broker
- Subscribe to command topics
- Publish status/telemetry topics
- Topic structure: vandimmer/{device_id}/ch{1-8}/set
- Payload: 0-255 brightness value
- QoS 0 for commands, QoS 1 for status
```

#### 4.3.3 Serial Console
```
- 115200 baud, 8N1
- Command interface for testing/debug
- Commands: CHx:nnn (set channel), ALL:nnn (set all), STATUS, REBOOT
```

### 4.4 Timing Requirements

| Function | Requirement |
|----------|-------------|
| PWM Update Rate | < 10ms latency |
| Button Response | < 100ms |
| Status LED Update | < 50ms |
| MQTT Publish | < 1 second after change |
| Boot to Operational | < 3 seconds |

---

## 5. PCB Specification

### 5.1 Board Parameters

| Parameter | Requirement |
|-----------|-------------|
| Dimensions | 100mm x 80mm (±0.5mm) |
| Layers | 2 (minimum) |
| Copper Weight | 1oz (35µm) minimum, 2oz preferred for power |
| Board Thickness | 1.6mm |
| Surface Finish | HASL or ENIG |
| Solder Mask | Any colour (green default) |
| Silkscreen | White, top side minimum |

### 5.2 Mounting

| Parameter | Requirement |
|-----------|-------------|
| Mounting Holes | 4x M3 (3.2mm diameter) |
| Hole Locations | 3.5mm from board edges, corners |
| Keepout | 5mm radius around mounting holes |

### 5.3 Trace Widths

| Signal Type | Width | Clearance |
|-------------|-------|-----------|
| Power (VIN, GND) | 1.5mm minimum | 0.3mm |
| MOSFET Drain | 1.0mm minimum | 0.3mm |
| 3.3V Rail | 0.5mm minimum | 0.3mm |
| Signal/Logic | 0.25mm minimum | 0.2mm |

### 5.4 Layout Requirements

**Power:**
- Wide traces or copper pours for VIN distribution
- Ground plane on bottom layer (preferred)
- Star topology for ground connections to MOSFETs
- Bulk capacitor near power input connector

**MOSFETs:**
- Gate resistor within 5mm of gate pin
- Pull-down resistor within 10mm of gate
- Short, wide traces from source to ground
- Thermal relief for drain connections

**ESP32:**
- Antenna area clear of ground plane and traces (follow module datasheet)
- Decoupling capacitors within 3mm of power pins
- Crystal/oscillator area clear (if using bare chip)

**Connectors:**
- Output terminals along board edge for easy wiring
- Power input at one end, outputs distributed
- Programming header accessible

### 5.5 Component Placement Zones

```
┌────────────────────────────────────────────────────┐
│ [M3]  PWR     REGULATOR                       [M3] │
│       IN      C1 C2 U2                             │
│                                                    │
│       STATUS          ┌──────────────┐    CH1-CH4 │
│       LED D1          │              │    OUTPUT  │
│       C3              │    ESP32     │    TERMINALS│
│                       │     U1       │            │
│       BUTTONS         │              │            │
│       J6 J7           └──────────────┘    CH5-CH8 │
│       R17 R18                             OUTPUT  │
│                       MOSFET ARRAY        TERMINALS│
│ [M3]                  Q1-Q8, R1-R16           [M3] │
└────────────────────────────────────────────────────┘
```

---

## 6. Bill of Materials

### 6.1 Semiconductors

| Ref | Qty | Value | Package | Description |
|-----|-----|-------|---------|-------------|
| U1 | 1 | ESP32-WROOM-32 | Module | WiFi/BT MCU module |
| U2 | 1 | AMS1117-3.3 | SOT-223 | 3.3V LDO regulator |
| Q1-Q8 | 8 | IRLZ44N | TO-220 | Logic-level N-MOSFET |
| D1 | 1 | WS2812B | 5050 | Addressable RGB LED |

### 6.2 Passives

| Ref | Qty | Value | Package | Description |
|-----|-----|-------|---------|-------------|
| R1-R8 | 8 | 100Ω | 0805 | Gate resistors |
| R9-R16 | 8 | 10KΩ | 0805 | Gate pull-down resistors |
| R17-R18 | 2 | 10KΩ | 0805 | Button pull-up resistors |
| C1 | 1 | 10µF | 0805 | Regulator input cap |
| C2 | 1 | 100nF | 0805 | Regulator output cap |
| C3 | 1 | 100nF | 0805 | WS2812 decoupling |
| C4 | 1 | 100µF | Electrolytic 8x10mm | Input bulk capacitor |

### 6.3 Connectors

| Ref | Qty | Value | Pitch | Description |
|-----|-----|-------|-------|-------------|
| J1 | 1 | 2-pos | 5.08mm | Power input screw terminal |
| J2-J9 | 8 | 2-pos | 5.08mm | LED output screw terminals |
| J10-J11 | 2 | 2-pos | 2.54mm | Button input headers |
| J12 | 1 | 6-pos | 2.54mm | Programming header (optional) |

### 6.4 Mechanical

| Item | Qty | Description |
|------|-----|-------------|
| Standoff | 4 | M3 x 10mm nylon or metal |
| Screw | 8 | M3 x 6mm pan head |

---

## 7. Testing Specification

### 7.1 Power-On Tests
- [ ] 3.3V rail within ±5% (3.135V - 3.465V)
- [ ] Quiescent current < 80mA
- [ ] No thermal issues at idle after 5 minutes

### 7.2 Channel Tests
- [ ] Each channel switches cleanly (no oscillation)
- [ ] PWM frequency within ±10% of target
- [ ] Full brightness (100%) voltage drop < 100mV at 1A
- [ ] Channel isolation (no crosstalk)

### 7.3 Input Tests
- [ ] Button press detected within 100ms
- [ ] Button release detected within 100ms
- [ ] No false triggers from noise

### 7.4 Communication Tests
- [ ] Serial console responds to commands
- [ ] WiFi connects to known network within 10 seconds
- [ ] MQTT publishes and subscribes correctly

### 7.5 Thermal Tests
- [ ] MOSFET temperature < 60°C at 1A continuous per channel
- [ ] Regulator temperature < 80°C under load
- [ ] No thermal shutdown in normal operation

---

## 8. Revision History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | Jan 2025 | Initial release - 8-channel fixed configuration |

---

## 9. References

- ESP32-WROOM-32 Datasheet: https://www.espressif.com/sites/default/files/documentation/esp32-wroom-32_datasheet_en.pdf
- IRLZ44N Datasheet: https://www.infineon.com/dgdl/irlz44n.pdf
- AMS1117 Datasheet: http://www.advanced-monolithic.com/pdf/ds1117.pdf
- WS2812B Datasheet: https://cdn-shop.adafruit.com/datasheets/WS2812B.pdf
