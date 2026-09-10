# LED Dimmer v2.0 - Visual Design Guide

## System Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────────┐
│                      VAN ELECTRICAL SYSTEM                           │
│                                                                       │
│  ┌──────────┐                                                        │
│  │ 12V      │                                                        │
│  │ Battery  │───────┬──────────────────────────────────────┐        │
│  │ Bank     │       │                                       │        │
│  └──────────┘       │                                       │        │
│                     │                                       │        │
│              ┌──────▼──────┐                         ┌──────▼──────┐ │
│              │  LED Strip  │                         │  LED Strip  │ │
│              │  Zone 1-4   │                         │  Zone 5-8   │ │
│              └──────────────┘                         └──────────────┘ │
└─────────────────────────────────────────────────────────────────────┘
                     │                                       │
                     │                                       │
┌────────────────────┼───────────────────────────────────────┼──────────┐
│                    │    LED DIMMER v2.0 ENCLOSURE          │          │
│  ┌─────────────────┼───────────────────────────────────────┼────────┐ │
│  │                 │                                       │        │ │
│  │  ╔══════════╗   │    ┌───────────────┐   ╔══════════╗ │        │ │
│  │  ║          ║───┘    │               │   ║          ║─┘        │ │
│  │  ║  Q1-Q4   ║        │   ESP32-WROOM │   ║  Q5-Q8   ║          │ │
│  │  ║  (LEFT)  ║        │               │   ║ (RIGHT)  ║          │ │
│  │  ║          ║        │   + AMS1117   │   ║          ║          │ │
│  │  ╚══════════╝        │               │   ╚══════════╝          │ │
│  │      ▲               └───────┬───────┘        ▲                │ │
│  │      │                       │                │                │ │
│  │      │     ┌─────────────────┘                │                │ │
│  │      │     │                                  │                │ │
│  │  ╔═══╧═════╧═════════════════════════════════╧═══╗            │ │
│  │  ║         12-24V DC POWER INPUT (J1)            ║            │ │
│  │  ╚═══════════════════════════════════════════════╝            │ │
│  │                                                                │ │
│  │  [RESET]  [TEST]     ● WS2812B Status LED                     │ │
│  │   SW1      SW2                                                 │ │
│  └────────────────────────────────────────────────────────────────┘ │
│                                                                      │
│  Ventilation ═══════════════════════════════════════════════════   │
└──────────────────────────────────────────────────────────────────────┘
                                   │
                                   │ WiFi / MQTT
                                   ▼
                          ┌─────────────────┐
                          │  MQTT Broker    │
                          │  (Mosquitto)    │
                          └────────┬────────┘
                                   │
                          ┌────────▼─────────┐
                          │   VanDaemon API  │
                          │   + Web UI       │
                          └──────────────────┘
```

## PCB Component Layout

### Top View - Component Placement

```
┌───────────────────────────────────────────────────────────────────────┐
│  100mm × 80mm PCB                                                     │
│                                                                        │
│  LEFT BANK              CENTER                    RIGHT BANK          │
│  ═════════              ══════                    ══════════          │
│                                                                        │
│  J2 ─ Q1 ─ R1,R9        ╔═══════════════╗         R5,R13 ─ Q5 ─ J8  │
│   │   │                 ║               ║                  │   │  │   │
│  CH1  IRLZ44N           ║   ESP32-WROOM ║            IRLZ44N  CH5    │
│                         ║               ║                            │
│  J3 ─ Q2 ─ R2,R10       ║    (U1)       ║         R6,R14 ─ Q6 ─ J9  │
│   │   │                 ║               ║                  │   │  │   │
│  CH2  IRLZ44N           ║               ║            IRLZ44N  CH6    │
│                         ╚═══════════════╝                            │
│  J4 ─ Q3 ─ R3,R11                                 R7,R15 ─ Q7 ─ J10 │
│   │   │                                                    │   │  │   │
│  CH3  IRLZ44N          ┌───────────┐              IRLZ44N  CH7      │
│                        │ AMS1117   │                                │
│  J5 ─ Q4 ─ R4,R12      │ 3.3V LDO  │             R8,R16 ─ Q8 ─ J11 │
│   │   │                │  (U2)     │                      │   │  │   │
│  CH4  IRLZ44N          └───────────┘              IRLZ44N  CH8      │
│                                                                        │
│                        C1  C2  C3                                     │
│                        □   □   □                                      │
│                                                                        │
│                                                                        │
│         ●─────────────────────●                                       │
│        H1 (M3)              H2 (M3)                                   │
│                                                                        │
│                                                                        │
│                    [SW1]      [SW2]          ● D1 (WS2812)           │
│                    RESET      TEST           Status LED               │
│                                                                        │
│         ●─────────────────────●                                       │
│        H3 (M3)              H4 (M3)                                   │
│                                                                        │
│                    ┌─────────┐                                        │
│                    │  J1     │                                        │
│                    │ 12-24V  │                                        │
│                    │  +  -   │                                        │
│                    └─────────┘                                        │
└───────────────────────────────────────────────────────────────────────┘

Legend:
  Q1-Q8: IRLZ44N MOSFETs (TO-220 package)
  R1-R8: 100Ω gate resistors (0805)
  R9-R16: 10KΩ pull-down resistors (0805)
  R17-R18: 10KΩ button pull-ups (0805)
  J1: Power input (screw terminal)
  J2-J5, J8-J11: LED outputs (screw terminals)
  SW1-SW2: Tactile switches (6mm)
  D1: WS2812B RGB LED (5050 package)
  H1-H4: Mounting holes (3.2mm for M3 screws)
```

## Schematic Block Diagram

```
                    ┌──────────────────────────────────────┐
                    │         POWER SECTION                │
                    │                                      │
    12-24V DC       │  ┌────────┐      ┌──────────┐       │  3.3V
    ──────────►J1──►│──┤  VIN   │      │ AMS1117  │       ├─────► ESP32
         │          │  │        ├─────►│   3.3V   ├───────┤       VCC
         │          │  │  C2    │      │          │  C1   │
         ─          │  └────────┘      └──────────┘       │
        GND         │                                      │
                    └──────────────────────────────────────┘
                                       │
                    ┌──────────────────┴──────────────────┐
                    │      MICROCONTROLLER SECTION        │
                    │                                     │
                    │        ┌────────────────┐           │
                    │        │  ESP32-WROOM   │           │
     GPIO16 ────────┼────────┤  WiFi + BT     │           │
     (WS2812)       │        │                │           │
                    │        │  GPIO25-27,14  ├───────────┼──► CH1-4
                    │        │  GPIO4,5,18,19 ├───────────┼──► CH5-8
     GPIO32 ────────┼────────┤                │           │
     (Button1)      │        │  GPIO32, 33    │           │
     GPIO33 ────────┼────────┤                │           │
     (Button2)      │        └────────────────┘           │
                    │                                     │
                    └─────────────────────────────────────┘
                                       │
                    ┌──────────────────┴──────────────────┐
                    │       MOSFET DRIVER SECTION         │
                    │                                     │
     CH1 ──► GPIO25 ├─[100R]─┬──[GATE]─►Q1──► J2 (LED+)  │
                    │        │                            │
                    │      [10K]                          │
                    │        │                            │
                    │       GND                           │
                    │                                     │
     CH2 ──► GPIO26 ├─[100R]─┬──[GATE]─►Q2──► J3 (LED+)  │
                    │        │                            │
                    │      [10K]                          │
                    │        │                            │
                    │       GND                           │
                    │                                     │
                    │   ... (Repeat for CH3-CH8) ...     │
                    │                                     │
                    └─────────────────────────────────────┘
                                       │
                                       │
                                      VIN (to LED strips)
                                       │
                         LED- (returns through MOSFETs to GND)
```

## MOSFET Driver Circuit (Detail)

```
                   VIN (12-24V)
                       │
                       ├──────────────────────► J2 Pin1 (LED+)
                       │
                   [DRAIN]
                       │
    GPIO25 ────[100R]─┬┤ GATE
               R1     ││
                      ││ Q1 (IRLZ44N)
                   [10K]│
                   R9  ││ SOURCE
                      ││    │
                      └┴────┼─────────────────► J2 Pin2 (LED-)
                       GND  │
                            └──────────────────► GND

Operating Principle:
- GPIO HIGH (3.3V) → MOSFET ON → LED strip conducts
- GPIO LOW (0V)    → MOSFET OFF → LED strip off
- PWM signal (0-100%) → Variable LED brightness
- R1 (100Ω) limits gate current during switching
- R9 (10K) ensures MOSFET stays OFF when GPIO floating
```

## Enclosure Cross-Section

```
                    TOP LID (3D Printed)
    ═══════════════════════════════════════════════════
    ║  ╔═══╗ ╔═══╗ ╔═══╗ ╔═══╗ ╔═══╗  Ventilation     ║
    ║  ║░░░║ ║░░░║ ║░░░║ ║░░░║ ║░░░║  Grid (35 slots) ║
    ║  ╚═══╝ ╚═══╝ ╚═══╝ ╚═══╝ ╚═══╝                  ║
    ═══════════════════════════════════════════════════
                          ▲
                          │ Hot air exits
                          │
    ┌─────────────────────┴─────────────────────────┐
    │  Case Height: 35mm                            │
    │                                               │
    │     ╔══════╗    ESP32     ╔══════╗           │
    │     ║ Q1-4 ║    Module    ║ Q5-8 ║           │
    │     ║BANK1 ║               ║BANK2 ║           │
    │═════╣      ╠═════════════════════╬═════       │
    │     ║      ║               ║      ║           │
    │     ╚══════╝               ╚══════╝           │
    ├───────────────────────────────────────────────┤
    │  Standoff: 10mm                               │
    ├───────────────────────────────────────────────┤
    │  ▓▓▓▓▓▓▓▓▓▓▓▓▓ PCB (1.6mm) ▓▓▓▓▓▓▓▓▓▓▓▓▓     │
    ├───────────────────────────────────────────────┤
    │  Bottom Clearance: 5mm (for solder joints)    │
    └───────────────────────────────────────────────┘
       ▲                                     ▲
       │                                     │
       │  Cool air enters through slots     │
       │                                     │
    ═══╬═════════════════════════════════════╬═══
       Vent Slots (5× per side)
```

## Airflow Pattern (Thermal Design)

```
                    ┌─────── TOP LID ───────┐
                    │   ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲   │
                    │   Hot air exits (50°C) │
                    └───────────────────────┘
                              ▲
                              │
     ════════════════════════════════════════
     LEFT SIDE                    RIGHT SIDE
     ══════════                   ══════════

       ──►    Q1 (42°C)             Q5 (41°C)   ◄──
       ──►    Q2 (43°C)             Q6 (42°C)   ◄──
       ──►    Q3 (44°C)             Q7 (43°C)   ◄──
       ──►    Q4 (45°C)  ESP32  Q8 (44°C)   ◄──
                          (40°C)

     Cool air (25°C)                Cool air (25°C)
     ──►                                     ◄──

     Ventilation slots              Ventilation slots
     (5× 40mm × 3mm)                (5× 40mm × 3mm)

Natural Convection Flow:
1. Cool ambient air enters through side slots
2. Air warms as it passes over MOSFETs
3. Warm air rises due to lower density
4. Hot air escapes through top ventilation grid
5. Fresh cool air drawn in (continuous cycle)

Estimated Heat Dissipation: ~400mW total (very low)
```

## Wiring Diagram

```
                    EXTERNAL CONNECTIONS

    ┌──────────────┐
    │  12V Battery │
    │   (House)    │
    └──┬───────┬───┘
       │       │
       │+12V   │GND
       │       │
    ┌──▼───────▼────────────────────────────┐
    │  J1: Power Input                      │
    │  [+12V]  [GND]                        │
    │                                       │
    │         LED DIMMER v2.0               │
    │                                       │
    │  LEFT BANK          RIGHT BANK        │
    │  ──────────          ──────────       │
    │  J2  [+] [-] CH1     J8  [+] [-] CH5  │
    │  J3  [+] [-] CH2     J9  [+] [-] CH6  │
    │  J4  [+] [-] CH3     J10 [+] [-] CH7  │
    │  J5  [+] [-] CH4     J11 [+] [-] CH8  │
    └───┬───┬───┬───┬───────┬───┬───┬───┬───┘
        │   │   │   │       │   │   │   │
        ▼   ▼   ▼   ▼       ▼   ▼   ▼   ▼
      LED LED LED LED     LED LED LED LED
    Strip Strip Strip Strip Strip Strip Strip Strip
     (1)  (2)  (3)  (4)   (5)  (6)  (7)  (8)

Wire Specifications:
- Power input (J1): 18 AWG (1.0mm²) minimum
- LED outputs: 20-22 AWG (0.5-0.75mm²)
- Max length: 5m per channel (voltage drop <0.5V)

Polarity:
  J1 (+) → Red wire → Battery positive
  J1 (-) → Black wire → Battery negative/ground
  J2-J11 (+) → Connects to +12V rail internally
  J2-J11 (-) → MOSFET switched output to LED strip
```

## WiFi Network Topology

```
                  ┌─────────────────┐
                  │  WiFi Router    │
                  │  192.168.1.1    │
                  └────────┬────────┘
                           │
         ┌─────────────────┼─────────────────┐
         │                 │                 │
    ┌────▼────┐      ┌─────▼────┐     ┌─────▼────┐
    │ LED     │      │ MQTT     │     │ VanDaemon│
    │ Dimmer  │      │ Broker   │     │ Server   │
    │ .101    │      │ .100     │     │ .102     │
    └─────────┘      └──────────┘     └──────────┘
         │                 │                 │
         └─────────────────┴─────────────────┘
                    MQTT Protocol

         Topic: vandaemon/leddimmer/cabin-lights/channel/0/set
         Payload: 128 (50% brightness)
```

## Power Budget

```
Component            Current @ 12V    Power
──────────────────────────────────────────────
ESP32 (WiFi active)  150mA           1.8W
AMS1117 quiescent    10mA            0.12W
WS2812B (full white) 60mA            0.72W
MOSFET drivers       negligible      <0.1W
──────────────────────────────────────────────
Controller Total:    ~220mA          ~2.7W

LED Loads (per channel):
  1.5A @ 12V = 18W per channel
  8 channels × 18W = 144W maximum total

Total System Power:
  Controller + LEDs = 2.7W + 144W = 146.7W max
  @ 12V = 12.2A maximum draw
```

## Status LED Color Codes

```
    ╔════════════════════════════════════════╗
    ║        D1 (WS2812B) Indicator          ║
    ╠════════════════════════════════════════╣
    ║  Color          │ Status                ║
    ╠═════════════════╪═══════════════════════╣
    ║  🔵 Blue         │ WiFi connecting...    ║
    ║  (slow pulse)   │                       ║
    ╠═════════════════╪═══════════════════════╣
    ║  🔷 Cyan         │ MQTT connecting...    ║
    ║  (slow pulse)   │                       ║
    ╠═════════════════╪═══════════════════════╣
    ║  🟢 Green        │ Connected & Ready     ║
    ║  (solid)        │                       ║
    ╠═════════════════╪═══════════════════════╣
    ║  🔴 Red          │ Error / Fault         ║
    ║  (fast blink)   │                       ║
    ╠═════════════════╪═══════════════════════╣
    ║  🟡 Yellow       │ Config Mode (AP)      ║
    ║  (solid)        │                       ║
    ╠═════════════════╪═══════════════════════╣
    ║  🟣 Purple       │ Firmware Update       ║
    ║  (pulse)        │ (OTA in progress)     ║
    ╚════════════════════════════════════════╝
```

## Mechanical Assembly Sequence

```
Step 1: Install Heat-Set Inserts
┌─────────────────────────────────┐
│  ┌───┐  ┌───┐                   │
│  │ ● │  │ ● │  Standoffs        │
│  └───┘  └───┘                   │
│                                 │
│  [Use 200°C soldering iron]    │
│  [M3×4.6mm brass inserts]      │
└─────────────────────────────────┘

Step 2: Mount PCB
┌─────────────────────────────────┐
│    ▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓          │
│    ▓ PCB (populated) ▓          │
│    ▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓          │
│  ┌───┐          ┌───┐           │
│  │ ⚙ │          │ ⚙ │           │
│  └───┘          └───┘           │
│  M3×6mm screws                  │
└─────────────────────────────────┘

Step 3: Connect Wiring
┌─────────────────────────────────┐
│  Power: [+12V] [GND]            │
│  ──►──────►                     │
│                                 │
│  CH1-8 Outputs                  │
│  ◄──◄──◄──◄──                  │
└─────────────────────────────────┘

Step 4: Test Before Closing
┌─────────────────────────────────┐
│  ✓ Power LED on                 │
│  ✓ Status LED green             │
│  ✓ All channels respond         │
│  ✓ Buttons functional           │
└─────────────────────────────────┘

Step 5: Install Lid
┌─────────────────────────────────┐
│  ╔═════════════════════════╗    │
│  ║  Top Lid                ║    │
│  ║  [Align & Press]        ║    │
│  ╚═════════════════════════╝    │
│  Secure with 4× M3×8mm screws   │
└─────────────────────────────────┘
```

## Size Comparison

```
┌────────────────────────────────────────────────────┐
│  Actual Size Comparison (scale: 1:2)               │
│                                                    │
│  LED Dimmer v2.0 Enclosure:                       │
│  ┌──────────────────────────────────────────┐     │
│  │                                          │     │
│  │         100mm × 80mm × 50mm              │     │
│  │                                          │     │
│  │         (Similar to deck of cards)       │     │
│  │                                          │     │
│  └──────────────────────────────────────────┘     │
│                                                    │
│  For Reference:                                   │
│  Raspberry Pi 4:  85mm × 56mm × 20mm              │
│  Deck of Cards:   89mm × 64mm × 19mm              │
│  iPhone 13:       146mm × 71mm × 7.7mm            │
└────────────────────────────────────────────────────┘
```

## Integration Example: Van Installation

```
                  ┌─────────────────────────────────┐
                  │  VAN CEILING                    │
                  │                                 │
                  │  ┌─────┐ ┌─────┐ ┌─────┐       │
                  │  │ LED │ │ LED │ │ LED │       │
                  │  │ CH1 │ │ CH2 │ │ CH3 │       │
                  │  └──┬──┘ └──┬──┘ └──┬──┘       │
                  └─────┼───────┼───────┼───────────┘
                        │       │       │
                        └───┬───┴───┬───┘
                            │       │
                  ┌─────────▼───────▼──────────────┐
                  │  Wall-Mounted Cabinet          │
                  │                                │
                  │  ┌──────────────────────────┐  │
                  │  │ LED Dimmer v2.0          │  │
                  │  │ ┌────────┐               │  │
                  │  │ │12V Batt├──────►        │  │
                  │  │ └────────┘               │  │
                  │  └──────────────────────────┘  │
                  │                                │
                  │  ┌──────────────────────────┐  │
                  │  │ Victron Cerbo GX         │  │
                  │  │ (MQTT Broker)            │  │
                  │  └──────────────────────────┘  │
                  │                                │
                  │  ┌──────────────────────────┐  │
                  │  │ Raspberry Pi 4           │  │
                  │  │ (VanDaemon Server)       │  │
                  │  └──────────────────────────┘  │
                  └────────────────────────────────┘
                              │
                              │ WiFi
                              ▼
                  ┌─────────────────────┐
                  │  Tablet Dashboard   │
                  │  (Web UI)           │
                  └─────────────────────┘
```

---

**This visual guide complements the technical documentation.**
**Refer to detailed design files for exact measurements and specifications.**

**Ready to build?** Print this guide for reference during assembly!
