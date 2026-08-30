# VANDIMMER-4CH+2A — Hardware Specification v2.0

**Board:** 4-channel PWM LED dimmer + 2 addressable LED outputs
**MCU:** ESP32-S3-WROOM-1U
**Target:** JLCPCB 2-layer, top-side SMD assembly
**Status:** Specification — pre-schematic
**Supersedes:** VANDIMMER-8CH v1.1

> **As-built corrections (2026-08-29).** This document was written before layout and
> several figures in it no longer describe the board. The corrected values are inline
> below; the reasoning is in `DECISIONS-2026-08-29.md`, which is authoritative where the
> two disagree. The substantive changes: the board is **12 V only** (was "10–30 V"),
> D5 is an **SMBJ18A** (was SMBJ33A), F1 is an **8 A** fuse (was 7 A), and the per-channel
> FET loss is **0.116 W** (was 0.06 W). See also §12's parts list.

---

## 1. Changes from v1.1

| Area | v1.1 | v2.0 | Rationale |
|---|---|---|---|
| Channels | 8× PWM | 4× PWM + 2× addressable | Matches actual van lighting topology |
| MCU | ESP32-WROOM-32 | ESP32-S3-WROOM-1U | Native USB, external antenna, pin headroom |
| USB | CH340C + auto-reset (10 parts) | Native USB-Serial-JTAG (0 parts) | BOM reduction, no driver issues |
| 3.3V supply | AMS1117 linear from VIN | Buck → 5V → LDO → 3.3V | **v1.1 linear reg was thermally non-viable** |
| MOSFETs | 8× IRLZ44N TO-220 | 4× logic-level DPAK | Assembly-compatible, current derated |
| Stackup | 2-layer | 2-layer (retained) | See §8.1 — 4-layer evaluated and rejected |
| Assembly | Mixed THT/SMD | Top-side SMD, connectors hand-fitted | JLC Economic assembly tier |
| Protection | Reverse-polarity Schottky | Fuse + TVS + P-FET | Schottky loss unacceptable at 5A |

---

## 2. Electrical Envelope

| Parameter | Value | Notes |
|---|---|---|
| Input voltage | **12 V nominal, 10–16 V DC** | **12 V-only board.** D5 is an SMBJ18A (~20 V breakdown) and U2 an AP63301 (32 V abs max) — see §4.1 |
| Input transient | **29.2 V clamp**, 400 W TVS | SMBJ18A, chosen to clamp *below* the AP63301's 32 V absolute maximum |
| Total board current | **5 A max** | Hard design cap |
| PWM channel current | 2 A max each | 4 channels; aggregate must not exceed 5 A |
| Addressable V+ current | 2 A per output | Polyfuse limited |
| 5 V rail | 2 A continuous (3 A part) | Shared with addressable outputs |
| 3.3 V rail | 500 mA | ESP32-S3 only |
| Quiescent (WiFi idle) | ~85 mA @ 12 V | ~1 W |
| Operating temperature | −20 to +60 °C ambient | Van cupboard / bulkhead mount |

**Aggregate constraint:** 4 × 2 A + 2 × 2 A = 12 A theoretical, but the board is
fused and specified at 5 A total. This is a documented user constraint, not a
hardware interlock. Silkscreen must state it.

---

## 3. Architecture

```
                 ┌─[F1 7A]─[TVS]─[Q_REV P-FET]─┐
  VIN 10-30V ────┤                              ├── VIN_PROT
                 └──────────────────────────────┘
                                │
        ┌───────────────┬───────┴────────┬──────────────────┐
        │               │                │                  │
   4× PWM ch       ADDR V+ jumper    BUCK 5V/3A         VIN sense
   (low-side)      (12V position)         │              (ADC)
        │                                 │
   LED strips                    ┌────────┼────────┐
                                 │        │        │
                          ADDR V+ jumper  │    74AHCT125
                          (5V position)   │    (level shift)
                                          │        │
                                      LDO 3.3V   ADDR DATA out
                                          │
                                    ESP32-S3-WROOM-1U
```

---

## 4. Power Tree

### 4.1 Input Protection

| Ref | Part | Spec |
|---|---|---|
| F1 | SMD fuse, 2410 | **8 A**, 250 V, 50 A interrupt — 6 A usable after 25 % derating, against a 5 A board cap. Backup to external fusing |
| D_TVS | **SMBJ18A** | 18 V standoff, 22.1 V breakdown, **29.2 V clamp**, 400 W |
| Q_REV | P-channel MOSFET, DPAK | Vds ≥ 60 V, Rds(on) ≤ 30 mΩ @ Vgs −10 V |
| R_G | 100 kΩ | Gate–source pulldown |
| D_Z | 12 V Zener, SOD-123 | Gate clamp — **required**, VIN exceeds Vgs(max) |

> **Part selection note:** Verify Q_REV against live JLCPCB stock before
> committing. Requirement is 60 V / ≤30 mΩ in DPAK. Candidates to check:
> NVD5117PL, SUD50P06-15, AOD407-class. Do not substitute a 30 V part —
> 30 V input plus transients will exceed it.

External fusing at the distribution panel remains the primary protection.
Recommend 7.5 A blade fuse.

**Loss:** 5 A × 5 A × 0.030 Ω = 0.75 W. DPAK with 645 mm² pour ≈ 34 °C rise.

### 4.2 Buck Regulator (5 V)

| Option | Part | Vin max | Use case |
|---|---|---|---|
| **A (default)** | AP64350 | 40 V | 12 V systems — cheaper, low EMI |
| B | TPS54360 | 60 V | 24 V-capable commercial variant |

Design point: 5 V @ 2 A continuous, 3 A part rating.

**Loss at full load:** 10 W out at 88–90 % efficiency → **1.1–1.4 W dissipated.**
This is the dominant heat source on the board and drives the thermal design.

Requirements:
- Input cap: 2× 10 µF / 50 V X7R (1210) + 100 nF, placed tight to the IC
- Inductor: 10 µH, Isat ≥ 4 A, shielded
- Output cap: 2× 22 µF / 16 V X7R
- Feedback divider routed away from the switch node
- Switch-node copper kept **minimum viable area** — it is the primary EMI radiator

### 4.3 LDO (3.3 V)

| Ref | Part | Spec |
|---|---|---|
| U_LDO | AMS1117-3.3 or equivalent | 5 V → 3.3 V, 800 mA, SOT-223 |

**Loss:** 1.7 V × 150 mA typical = 0.26 W; 0.6 W on WiFi TX bursts.
Negligible with a 645 mm² pour (~12 °C rise).

Buck→LDO rather than a second buck is deliberate: it costs 0.26 W and keeps
switching ripple off the ESP32 supply, which matters for WiFi receive sensitivity.

---

## 5. PWM Channels (×4)

Low-side N-channel switching. Load connects between VIN_PROT and channel drain.

```
GPIO ──[R_gate 100Ω]──┬── MOSFET gate
                      │
                   [R_pd 10k]
                      │
                     GND

VIN_PROT ──┬── terminal V+
           │
        [D_fly]      (cathode to VIN_PROT, anode to drain)
           │
MOSFET drain ── terminal LED−
MOSFET source ── GND
```

| Ref | Part | Spec |
|---|---|---|
| Q1–Q4 | N-channel MOSFET, DPAK | Vds ≥ 60 V, **Rds(on) characterised at Vgs = 2.5 V** |
| R1–R4 | 100 Ω, 0805 | Gate series — EMI damping |
| R5–R8 | 10 kΩ, 0805 | Gate pulldown, safe state on reset |
| D_fly1–4 | SS34, SMA | Flyback clamp for cable inductance |

**Critical:** the MOSFET must be specified at Vgs = 2.5 V, not 4.5 V. Driving a
4.5 V-rated part from 3.3 V puts it in partial enhancement — Rds(on) rises and
dissipation is no longer predictable. AO3400A-class silicon in a DPAK body is
the target.

**Thermal:** 2 A × 2 A × 0.029 Ω = **0.116 W per channel**. Approximately 6 °C rise.
The earlier 0.015 Ω assumed a 10 V gate drive. The fitted 20N06 is **29 mΩ at the
4.5 V the 74HCT125 actually delivers**, so the real loss is about double. Still
comfortable, but the original number was optimistic.
DPAK is thermal overkill here and is chosen for assembly robustness and future
headroom, not dissipation.

**PWM frequency:** 1–2 kHz default. Flicker-free, negligible switching loss,
low EMI. Configurable in firmware up to 20 kHz if camera-artefact-free dimming
is needed for interior filming.

---

## 6. Addressable Outputs (×2)

### 6.1 Voltage Selection

Each output has a 3-pin jumper header selecting V+ source:

```
VIN_PROT ──○  pin 1
             ○  pin 2 ── polyfuse ── terminal V+
+5V      ──○  pin 3
```

Shunt on pins 1–2 = 12 V strips (WS2815, 12 V WS2811).
Shunt on pins 2–3 = 5 V strips (WS2812B, SK6812).

**Silkscreen must label this unambiguously.** A shunt in the wrong position
puts 12 V into a 5 V strip.

### 6.2 Level Shifting

Data is **always** shifted to 5 V regardless of jumper position.

| Ref | Part | Notes |
|---|---|---|
| U_LS | 74AHCT125, SOIC-14 | Quad buffer, HCT thresholds (VIH 2.0 V) |
| R_s1–2 | 47 Ω, 0805 | Series termination on data lines |

Two of the four buffer channels drive DATA1 and DATA2. **The remaining two are
broken out to unpopulated pads** — these become clock lines if APA102/SK9822
support is ever wanted.

> **WS2815 caveat:** the datasheet specifies VIH = 0.7 × VDD, which at 12 V
> would be 8.4 V. Real silicon accepts 5 V logic reliably and effectively every
> commercial 12 V controller does exactly this. Treat as *tested, not
> spec-guaranteed*. If a batch of strips misbehaves, the fallback is a
> discrete MOSFET shifter to VIN.

### 6.3 Protection

| Ref | Part | Spec |
|---|---|---|
| PF1, PF2 | Polyfuse, 1812 | 2 A hold, 30 V |

A shorted strip behind a wall panel must not take the board with it.

### 6.4 Firmware Driver

Use the ESP32-S3 **RMT peripheral** (`led_strip` component, ESP-IDF). Four RMT
TX channels available. No bit-banging, no interrupt-disable windows, no timing
sensitivity to WiFi activity.

### 6.5 Documented Capacity Limit

The 5 V rail at 2 A supports approximately:
- **33 LEDs** at full white (60 mA/LED)
- **~100 LEDs** at realistic mixed-colour use

Beyond this, external power injection is required. **This limit must be
silkscreened next to the addressable terminals.**

12 V strips draw from VIN_PROT and are limited only by the 2 A polyfuse and the
5 A board total.

---

## 7. Pin Assignment — ESP32-S3-WROOM-1U

| GPIO | Function | Notes |
|---|---|---|
| 4 | PWM CH1 | LEDC |
| 5 | PWM CH2 | LEDC |
| 6 | PWM CH3 | LEDC |
| 7 | PWM CH4 | LEDC |
| 15 | ADDR1 DATA | via 74AHCT125 |
| 16 | ADDR2 DATA | via 74AHCT125 |
| 17 | ADDR1 CLK (reserved) | APA102 future — pad only |
| 18 | ADDR2 CLK (reserved) | APA102 future — pad only |
| 48 | Onboard status RGB | WS2812B, matches dev-board convention |
| 10 | BTN1 | Active-low, 10 k pull-up |
| 11 | BTN2 | Active-low, 10 k pull-up |
| 8 | I²C SDA | Expansion header |
| 9 | I²C SCL | Expansion header |
| 1 | VIN sense | ADC1_CH0, 100 k/10 k divider |
| 2 | Board temp | ADC1_CH1, 10 k NTC |
| 41 | UART1 TX | Truma LIN / expansion |
| 42 | UART1 RX | Truma LIN / expansion |
| 19, 20 | USB D−, D+ | Native USB-Serial-JTAG |

**Avoided:** GPIO0, 3, 45, 46 (strapping); GPIO26–37 (flash/PSRAM);
GPIO43, 44 (UART0 console).

**VIN divider:** 100 kΩ / 10 kΩ gives 30 V → 2.73 V, within ADC range.
Add 100 nF to ground at the ADC pin.

---

## 8. PCB Design

### 8.1 Stackup — 2 Layer

| Layer | Content |
|---|---|
| L1 | Components, signal routing, power pours (VIN_PROT, +5V, +3V3) |
| L2 | **Solid GND pour — no splits under the buck or the module** |

JLCPCB standard 2-layer, 1.6 mm, 1 oz both sides.

**4-layer was evaluated and rejected.** The reasoning, recorded so it isn't
re-litigated later:

- *Thermal:* JLC's 4-layer stackup uses 1 oz outer / **0.5 oz inner**. A thermal
  via array into a full 1 oz bottom pour on 2-layer gives more copper mass than
  vias into a half-ounce inner plane. There is no meaningful thermal advantage.
- *Routing density:* 4 PWM channels, 2 data lines and one module is not a
  congested board. No pressure here.
- *EMI:* this is the only real 4-layer advantage — reference plane ~0.2 mm below
  the switch node versus ~1.5 mm on 2-layer, so smaller return loop area. At
  this switching frequency it is engineerable around, and §8.5 exists for
  exactly that reason.
- *Cost:* trivial delta on 5 prototypes, roughly 2× bare-board cost at volume.

**Decision:** prototype 2-layer. If §11 item 10 (EMI sweep) fails, respin to
4-layer — a respin after first silicon is likely regardless.

The WROOM-**1U** materially de-risks this. The external antenna on a pigtail can
be positioned well away from the board, so buck harmonics desensing the 2.4 GHz
front end is far less likely than it would be with a PCB-antenna module.

### 8.2 Board Outline

100 × 80 mm, 4× M3 mounting holes 3.5 mm from edges (retains v1.1 mounting).

### 8.3 Placement Zones

```
┌────────────────────────────────────────────┐
│ [VIN term]  [Protection]  [BUCK+inductor]  │  ← Power, top edge
│                                            │
│ [USB-C]  [ESP32-S3-WROOM-1U]  [antenna →]  │  ← Digital, centre
│          [74AHCT125]  [buttons]            │
│                                            │
│ [Q1..Q4 + flyback]      [ADDR jumpers]     │  ← Output, bottom edge
│ [CH1][CH2][CH3][CH4]    [A1][A2]           │
└────────────────────────────────────────────┘
```

- Buck switch node: minimum copper area, tight loop to input caps
- Keep the buck **≥ 20 mm** from the WROOM module — increased from 15 mm as a
  2-layer concession
- WROOM-1U has an IPEX connector — no antenna keepout needed, but leave
  clearance for the pigtail and route it away from the buck
- Screw terminals along board edges only
- Do not route any signal across the bottom layer beneath the buck; that pour
  must stay unbroken (see §8.5)

### 8.4 Copper and Track Rules

| Net | Width / treatment |
|---|---|
| VIN_PROT | Pour, not track. Min equivalent 3 mm |
| GND | L2 pour, stitched every 10 mm around board perimeter |
| +5V | 1.5 mm |
| +3V3 | 0.5 mm |
| PWM channel drains (2 A) | 1.0 mm |
| Signals | 0.25 mm |
| USB D±| 0.25 mm, matched length, 90 Ω differential |

**Thermal reference — 1 oz pour, still air:**

| Pour area | Rth(j-a) |
|---|---|
| 300 mm² | ~65 °C/W |
| 645 mm² (1 in²) | ~45 °C/W |
| 1300 mm² | ~35 °C/W |
| 645 mm² + thermal via array to L2 pour | ~28 °C/W |

Returns diminish sharply past ~1.5 in² — heat spreading becomes limited by
copper's own conductance, not area. **Thermal vias buy more than extra area.**
Use a 3×3 grid of 0.3 mm vias under the buck IC and the P-FET tab.

**Track current, 1 oz external, 10 °C rise:** ≈ 2 A per mm.

### 8.5 Buck Layout Rules — Mandatory on 2 Layer

These are not preferences. On 4-layer the plane forgives sloppy work here; on
2-layer it does not. This section is the price of the stackup decision in §8.1.

**1. The hot loop is everything.**

During the high-side FET's on-phase, current flows Cin(+) → VIN pin →
high-side FET → SW → inductor. When the FET turns off, that current path
collapses. The loop carrying the resulting di/dt is:

**Cin(+) → VIN pin → GND pin → Cin(−) → Cin(+)**

Its physical enclosed area, times the di/dt, sets radiated emissions. Nothing
else in the buck layout matters as much.

```
        ┌──────────────┐
   Cin ═╡ VIN      SW  ╞═══ L1 ──→ Vout
    ║   │              │
    ╚═══╡ GND          │      ← THIS loop must be as small
        └──────────────┘        as physically achievable
```

- Cin placed on the **same side as the IC**, directly abutting VIN and GND pins
- Loop area target: **< 15 mm²**
- **No via in the hot loop.** A via between Cin ground and the IC ground pin
  adds ~1 nH and defeats the whole exercise. Connect on the top layer with
  copper, then stitch to the bottom pour *outside* the loop.
- Use two input caps of different value (10 µF + 100 nF), 100 nF closest to
  the pin

**2. Switch node.**

- Minimum viable copper area — just enough for current, no more. This node is
  the primary dV/dt radiator.
- Never pour it. Never run it on the bottom layer.
- No signal trace may run parallel to it within 3 mm on either layer.

**3. Bottom pour integrity.**

- The GND pour beneath the buck must be **continuous and unbroken**. A signal
  trace crossing under the buck forces return current to detour around it,
  enlarging the loop by exactly the amount you were trying to avoid.
- Enforce with a keepout on the bottom layer covering the buck footprint plus
  5 mm margin.

**4. Feedback network.**

- FB divider close to the IC, referenced to IC ground, routed on the top layer
- Route away from the switch node and the inductor — never underneath either
- Keep the FB trace short; it is high-impedance and will pick up noise

**5. Part selection.**

- Prefer a buck with **spread-spectrum / frequency dithering**. This is a
  meaningful advantage on 2-layer and costs nothing.
- Provision an **optional input LC filter** (10 µH + 10 µF) as unpopulated
  footprints. Cheap insurance if §11 item 10 fails — populate rather than
  respin.

**6. Stitching.**

- Ground stitching vias every 10 mm around the board perimeter
- Dense stitching around the buck ground pour, but placed **outside** the hot
  loop, not within it

### 8.6 Thermal Budget Summary

| Source | Dissipation | Pour | Rise |
|---|---|---|---|
| Buck (5 V @ 2 A) | 1.1–1.4 W | 645 mm² + vias | ~33 °C |
| Q_REV @ 5 A | 0.75 W | 645 mm² | ~34 °C |
| LDO | 0.55 W | 645 mm² | ~25 °C (Tj 57.9 °C, 67 °C margin) |
| Q1–Q4 @ 2 A each | 0.116 W each | 300 mm² | ~6 °C |
| **Board total** | **~2.4–2.7 W** | | **~15–20 °C enclosure rise** |

At 60 °C ambient worst case, hottest junction ≈ 95 °C. Acceptable margin.

---

## 9. Manufacturing — JLCPCB

| Setting | Value |
|---|---|
| Layers | 2 |
| Dimensions | 100 × 80 mm |
| Thickness | 1.6 mm |
| Copper | 1 oz both sides |
| Surface finish | HASL adequate (no pitch below 0.5 mm); ENIG if buck is QFN |
| Assembly | **Economic — top side SMD only** |
| Min track/clearance | 0.15 mm (well within capability) |

**Assembly rules:**
- Every machine-placed part on L1 (top). Nothing on bottom.
- Prefer JLC **Basic** parts where possible — Extended parts incur a per-type
  loading fee (~$3 each).
- **Screw terminals and jumper headers are hand-fitted after delivery.** THT
  at JLC is hand-soldered and priced per joint; ~12 connectors is ~20 minutes
  of your own time.
- Provide BOM and CPL in JLC format; verify part rotations against the
  JLC library, which differs from KiCad defaults for several packages.

---

## 10. Bill of Materials (indicative)

| Qty | Ref | Value / Part | Package |
|---|---|---|---|
| 1 | U1 | ESP32-S3-WROOM-1U | Module |
| 1 | U2 | AP64350 (or TPS54360) | SOIC-8 / QFN |
| 1 | U3 | AMS1117-3.3 | SOT-223 |
| 1 | U4 | 74AHCT125 | SOIC-14 |
| 4 | Q1–Q4 | N-FET, 60 V, Vgs 2.5 V rated | DPAK |
| 1 | Q5 | P-FET, 60 V, ≤30 mΩ | DPAK |
| 4 | D1–D4 | SS34 flyback | SMA |
| 1 | D5 | SMBJ18A TVS | SMB |
| 1 | D6 | 12 V Zener | SOD-123 |
| 1 | D7 | WS2812B status | 5050 |
| 1 | L1 | 10 µH, 4 A shielded | 6×6 mm |
| 1 | F1 | 8 A fuse | 2410 |
| 2 | PF1–2 | Polyfuse 2 A | 1812 |
| 2 | C1–2 | 10 µF / 50 V X7R | 1210 |
| 2 | C3–4 | 22 µF / 16 V X7R | 0805 |
| ~8 | — | 100 nF | 0805 |
| 4 | R1–4 | 100 Ω gate | 0805 |
| 4 | R5–8 | 10 kΩ pulldown | 0805 |
| 2 | R9–10 | 47 Ω data series | 0805 |
| 1 | R11 | 100 kΩ divider | 0805 |
| 1 | R12 | 10 kΩ divider | 0805 |
| 1 | R13 | 100 kΩ P-FET gate | 0805 |
| 1 | R14 | 10 kΩ NTC | 0805 |
| 2 | R15–16 | 10 kΩ button pull-up | 0805 |
| 1 | J1 | USB-C receptacle | SMD |

**Hand-fitted after assembly:**

| Qty | Ref | Part |
|---|---|---|
| 1 | J2 | 2-pos screw terminal, 5.08 mm — VIN |
| 4 | J3–J6 | 2-pos screw terminal, 5.08 mm — PWM out |
| 2 | J7–J8 | 3-pos screw terminal, 5.08 mm — addressable out |
| 2 | J9–J10 | 3-pin header + shunt — V+ select |
| 2 | J11–J12 | 2-pin header — buttons |
| 1 | J13 | 4-pin header — I²C expansion |
| 1 | J14 | 3-pin header — UART1 expansion |

---

## 11. Test Plan

1. **Bare board:** continuity VIN→GND (open), 5V→GND (open), 3V3→GND (open)
2. **Power-up, no load:** 12 V in → verify 5.00 V ±2 %, 3.30 V ±2 %
3. **Reverse polarity:** apply −12 V, verify no current draw, no damage
4. **Transient:** 40 V, 100 ms pulse — verify TVS clamps, board survives
5. **USB enumeration:** device appears as ESP32-S3 USB-Serial-JTAG, no drivers
6. **PWM channels:** 2 A resistive load each, verify duty linearity, measure
   MOSFET case temp after 30 min
7. **Addressable, 5 V:** jumper to 5 V, 50× WS2812B, verify no data corruption
   at 5 m cable
8. **Addressable, 12 V:** jumper to 12 V, WS2815 strip, verify 5 V logic drives
   reliably — **this is the highest-risk item in the design**
9. **Thermal soak:** 5 A total load, 60 °C ambient chamber (or enclosed), 2 h,
   thermal camera survey
10. **EMI sanity:** AM radio near board at 1 kHz and 20 kHz PWM

---

## 12. Open Items

| Item | Decision needed |
|---|---|
| Buck part | **Settled: AP63301WU-7** — 12 V only, 3 A, 500 kHz, internally compensated |
| Q_REV part | Verify 60 V / ≤30 mΩ DPAK P-FET against JLC live stock |
| Enclosure | Affects thermal rise assumptions — currently modelled as enclosed still air |
| 24 V variant | **Not a drop-in.** Needs D5 *and* U2 changed together — an SMBJ18A breaks down at ~20 V, and an SMBJ33A clamps at ~53 V, above the AP63301's 32 V limit |
| Conformal coating | Consider for condensation resistance in van environment |
| EMI contingency | If §11 item 10 fails: populate optional input LC filter first; respin to 4-layer only if that is insufficient |

---

## 13. Next Steps

Implementation happens on a separate machine running Claude Code with a KiCad
MCP server (Konnect or kicad-mcp-pro) against KiCad 9/10. See the companion
implementation prompt.

1. **Parts verification first.** Resolve every §12 open item against live
   JLCPCB stock before drawing anything. Prefer Basic parts. The two blocking
   unknowns are the buck (AP64350 vs TPS54360) and Q_REV.
2. Schematic — hierarchical sheets: Power, MCU, PWM, Addressable
3. ERC clean, zero warnings suppressed without written justification
4. Footprint assignment, verify JLC rotations differ from KiCad defaults
5. Layout to §8, with §8.5 treated as a gate not a guideline
6. DRC clean + JLC DFM check
7. Gerbers, drill, BOM, CPL
8. Order 5 assembled, hand-fit connectors per §10
9. Execute §11 test plan — items 8 and 10 are the risk concentration

**Prior-art warning:** the v1.0/v1.1 KiCad files were hand-written s-expressions
produced without any KiCad tooling to validate them. Footprint references were
never resolved against a library and the boards have no connectivity or
ratsnest. **Do not use them as a starting point.** Start from this specification
and a clean KiCad project.
