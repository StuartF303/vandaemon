# VANDIMMER-4CH+2A — Rev B design decisions

**Date:** 2026-09-12
**Status:** Approved, not yet implemented. No KiCad file has been touched.
**Supersedes:** nothing. Amends `VANDIMMER-4CH-2ADDR-SPEC-v2.0.md`, which remains the
base specification. Where the two disagree, this document wins for Rev B; v2.0 continues
to describe Rev A as built.

Rev A was ordered 2026-09-03 (tag `ordered-revA`, commit `37c12ab`) and brought up on
2026-09-10 and 12. It works — WiFi, MQTT, all four PWM channels, both addressable
outputs. This document records what the bring-up and the subsequent design review decided
to change, and why.

---

## 1. Why there is a Rev B

Three things, in order of how much they constrain the board.

**The floorplan cannot route the power rail.** §8.3 of v2.0 puts power in at the north
edge and outputs at the south with the MCU between, so on two layers the ~5 A `VIN_PROT`
rail has to cross the board. The only path was a 4.1 × 39 mm, 155 mm² strip of B.Cu
through the MCU/USB region, and that strip is the direct cause of:

| measurement | value | tool |
|---|---|---|
| F.Cu nets crossing a gap in the B.Cu GND pour | **42 of 60**, worst 6.50 mm (`DRAIN1`) | `tools/emc.py` |
| Return vias within 1.0 mm | 15 of 43; **12 with nothing inside 2 mm** | `tools/emc.py` |

The handoff is explicit that this is *"a floorplan consequence, not a routing mistake"*.

**Three pads shipped unrouted**, all of which are dead features on the delivered hardware:
`J11.1` (BTN1 — and therefore the only button-based recovery), `J9.1` and `J10.1` (the
12 V position of both strip selectors). `D9.1` is also unrouted but harmless.

**The 5 V rail is undersized for the intended lighting.** See §3.

---

## 2. Stackup — 4 layers

| layer | contents |
|---|---|
| L1 | signal |
| **L2** | **GND — continuous, unbroken reference** |
| L3 | power: `VIN_PROT` region + `+5V` region |
| L4 | signal |

GND sits directly under L1 so every fast signal has a tight return. That retires the
42-of-60 finding structurally rather than by careful routing, and `VIN_PROT` gets a plane
instead of a corridor.

**Cost, quoted 2026-09-12 (JLCPCB, 100 × 84 mm, 98 components):**

| qty | 2-layer PCB | 4-layer PCB | total 2L | total 4L | delta per board |
|---:|---:|---:|---:|---:|---:|
| 5 | $2 | $7 | $35.97 | $40.97 | **+$1.00** |
| 30 | $12 | $42 | $175.82 | $205.82 | **+$1.00** |

Bare PCB is 6% of build cost at 2 layers and 17% at 4. Components and assembly are
identical either way. Figures are indicative — confirm against JLC's quoting tool before
ordering, though the shape of the answer will not change.

**Board size stays ≤100 × 100 mm** for that price bracket. Within the bracket price is
flat, so a smaller board saves enclosure space, not money — and shrinking spends the
routing freedom the fourth layer was bought to provide. Design at the size the layout
wants, up to the bracket.

---

## 3. Power architecture for the addressable outputs

**Decision: 12 V is the primary path for full-length runs; 5 V remains for short accents.**

The intended load is 2 × 2 m at 60 LEDs/m — 120 LEDs per output, mood lighting. On 5 V
that does not fit:

| master brightness | per output | both outputs | + board (0.4 A) |
|---|---:|---:|---:|
| 40 % | 2.0 A | 4.0 A | **4.4 A** |
| 20 % | 1.0 A | 2.0 A | **2.4 A** |

against a buck **design point of 2 A and a part rating of 3 A** (AP64350). Warm white on
an RGB strip lights all three channels, averaging ~41 mA/LED at full brightness. Even
gentle use is at or past the rail; the symptom would be strips that will not go bright,
not an obvious fault. A 2 m 5 V run also sags end to end and normally needs power
injection every 1–1.5 m.

WS2815 at 12 V draws **~1.7 A per output at full white**, from `VIN_PROT` rather than the
buck, and needs no injection over 2 m.

Consequences:

- **`J9.1` / `J10.1` must be routed.** Without them the 12 V position does not exist, which
  is the Rev A defect that forces 5 V today.
- **The buck stays at its present 2 A design point.** No larger inductor, no extra copper,
  thermal design unchanged. `+5V` is already at 571.8 mm² against a 645 mm² gate.
- **`VIN_PROT` carries more current than before** — up to ~3.4 A of strip load on top of
  the PWM channels. This *raises* the stakes on the floorplan rather than relieving them,
  and is part of the case for 4 layers.
- **J7/J8's spare pin carries WS2815 backup-data.** The connectors are already 4-way
  (V+, DATA, CLK, GND) and WS2815 is a 4-wire strip, so its single-LED-failure tolerance
  comes free.

### 3.1 Polyfuse re-rating — PF1/PF2 from 2 A to 3 A

1.7 A against a 2 A hold is marginal, and polyfuse hold current derates with ambient
temperature. In a warm enclosure the present parts would nuisance-trip. 30 V rating is
retained, since these sit downstream of the jumper and may see `VIN_PROT`.

### 3.2 Firmware — the 5 V cap must become a rail budget

The current cap is **1.8 A per strip**, so two strips are permitted to draw 3.6 A — over
the 3 A part rating. It must become a **combined** budget across both outputs. This is a
live defect in the shipped firmware, independent of the board.

---

## 4. Module and antenna

**Default: `ESP32-S3-WROOM-1-N16R8`** (LCSC `C2913202`) — on-board PCB antenna.
**Fallback: `ESP32-S3-WROOM-1U-N16`** (LCSC `C2980298`) + external antenna.

| | `-1U` (Rev A) | `-1` (Rev B default) |
|---|---|---|
| size | 19.2 × 18 mm | 25.5 × 18 mm |
| price | $5.03 | $5.14 |
| stock | 4,246 | 37,918 |

The two share an identical pad layout — the `-1U` is the same module with the antenna
section removed — so **one footprint accepts either part**. Laying out for the `-1` leaves
the keepout as empty board if a `-1U` is fitted.

The N16R8 adds 8 MB octal PSRAM that the firmware does not use. Its pins (IO35–37) do not
collide with the pin map (IO1, 2, 3, 9, 11, 13, 21, 39, 40, 45, 47, 48).

**Placement: antenna overhanging the board edge.** Espressif's order of preference is
(1) antenna overhanging the PCB, (2) board cut away beneath it, (3) an all-layer keepout.
On a 4-layer board (3) means punching through both plane layers, so (1) is chosen — it
also costs zero board area, keeping the outline inside the price bracket. Exact keepout
dimensions to be taken from the WROOM-1 datasheet at layout time, not from memory.

**Rationale for on-board as default:** the Rev A bring-up lost a debugging round to a
board that could hear nothing because no antenna was fitted — the scan found a single AP
at **−94 dBm**, the noise floor. An on-board antenna removes that failure mode, one BOM
line and one assembly step. The deployment is favourable: the enclosure is plastic, and
the AP sits inside the same metal vehicle body at ≤5 m, so the metal is not between the
two.

---

## 5. Floorplan

```
 ┌─○──────────────── ≤100 mm ─────────────────○──────┐
 │  [J11][J12] buttons      [D7]                     │ ← user face
 │                                          ╔════════╡← antenna
 │  ┌────┐   ┌────┐    ┌──────────┐         ║   U1   │   overhangs
 │  │ U2 │   │ U3 │    │ Q1..Q4   │         ╚════════╡
 ╞══╡buck│   │LDO │    │ D1..D4   │        [J13][J14]│
 │  └────┘   └────┘    └──────────┘                  │
 ╞J2╡  F1 D5 Q_REV    U4/U5      PF1 PF2             │
 │  └─○ ← corner hole separates J2 from J3           │
 ├────┬─────┬─────┬─────┬───────┬───────┬───────┬────┤
 │ J3 │ J4  │ J5  │ J6  │  J7   │  J8   │  USB  │    │
 └────┴─────┴─────┴─────┴───────┴───────┴───────┴─○──┘
        ── 4 × PWM ──   ── addressable ──
```

**Harness face (bottom).** All LED and data cables exit one face — one harness, one
gland, one opening. The board is wall-mounted, so these point downward.

**J2 (12 V in) on the left edge**, immediately above J3. This is a space decision and an
electrical one:

- *Space.* Eight connectors do not fit a 100 mm edge. Measured courtyards from the
  fabricated board: J2 11.28, J3–J6 8.40 each, J7/J8 13.40 each, J1 10.64 — **82.32 mm of
  bodies**, leaving under 2 mm per gap with nothing for a mounting hole. Moving J2 off
  leaves **71.04 mm**, giving **4.16 mm** between every connector, or 3.09 mm if an inline
  hole is also wanted.
- *Electrical.* The 12 V input now lands directly beside F1, D5 and Q_REV with the buck
  immediately inboard, so the full 5 A never crosses the board. This is the direct inverse
  of the Rev A failure.

**J1 (USB-C) on the harness face**, far right. Moving it off the left edge is required:
J1's through-hole shield pads are what blocked `VIN_PROT` from the left edge on Rev A
(y 107.18–116.82). It sits at the opposite end of U1 from the antenna — a USB-C shell is
metal and detunes a PCB antenna. Diagonal separation ~25–30 mm, to be verified against the
datasheet keepout at layout.

**U1 at the right edge, antenna overhanging.** Furthest point from the buck and the switch
node.

**Human interface grouped on the top face:** buttons J11/J12 with the status LED D7. These
are one interaction — press and watch — and share a panel cutout. J13/J14 are I²C and UART
expansion, not user-facing, and sit near U1 to keep those MCU signals short.

### 5.1 Mounting holes

Four near the corners, positions free to move to suit components and to keep the plane
necks healthy. Rev A has form here: the right edge pinched to **1.9 mm ≈ 3.8 A at the
H2/H3 holes**. Necks around every hole are to be measured, not assumed — especially the
bottom-left, which sits where `VIN_PROT` carries full input current before it fans out.

**The bottom-left corner hole provides the J2-to-J3 separation** that was originally going
to need a fifth, inline hole. It does that job without consuming harness-edge length.

**M2.5 fixings** — 2.7 mm clearance hole, ~5.9 mm keepout diameter allowing a 1.6 mm
annulus. Chosen over M3 partly because the smaller keepout leaves more copper at the plane
necks.

**Electrically isolated** — no copper connection, keepout around each hole. The enclosure
is plastic, so there is no chassis path to worry about; this also avoids any future
enclosure creating a second return path in parallel with J2's negative wire.

---

## 6. Output connectors J3–J6

**Pin order swaps: pin 1 becomes LED−, pin 2 becomes V+.** See §5.1 of the base spec.

**Pin 1 is not ground**, on either revision. It is the MOSFET drain — open when the channel
is off, floating to roughly +12 V through the load, and to ~7 V on an open terminal from
flyback-diode and FET leakage alone (measured). Silkscreen **−** and **+**, never `GND`.
All four channels share pin 2, so the outputs are common-positive.

**Rev A is the reverse** — pin 1 is `VIN_PROT`. Grounding pin 1 on a Rev A board is a dead
short across the supply.

**Footprint: JST XH S2B-XH-A retained.** The connector *family* is an open question
deferred to inventory and cost, not a closed decision — see §8.

---

## 7. Firmware changes required

Independent of the board, and worth landing regardless of what Rev B's copper looks like.

1. **Portal fallback after N failed WiFi attempts.** `net_tick()` currently has none: when
   WiFi never comes up, `s_wifiUp` is already false so the retry block never arms and the
   board sits red indefinitely. With BTN1 dead and `forget-wifi` needing a broker, a wrong
   SSID strands the board. On the bench that is an NVS erase over USB; in the van, where
   there is no USB by design, it is a dead board.
2. **Combined 5 V rail budget** replacing the per-strip 1.8 A cap (§3.2).
3. Already landed 2026-09-12: a **serial console** over USB (`all`, `ch`, `off`, `bright`,
   `portal`), which gives the first Rev A recovery route that does not need an NVS erase.

---

## 8. Open items

| item | note |
|---|---|
| Connector family for J3–J6 | JST XH for now. Molex was fitted to the XH land by hand without difficulty, so a 2.54 mm Molex is viable in practice; the pitch error is 0.04 mm per pin and cumulative, reaching ~0.12 mm across the 4-way J7/J8. Revisit on inventory and cost. |
| ~~Mounting hardware~~ | **Resolved 2026-09-12: M2.5.** 2.7 mm clearance hole, ~5.9 mm keepout diameter with a 1.6 mm annulus. Slightly gentler on the plane necks than M3's 6.4 mm. |
| Enclosure | PLA for the prototype with vents; PETG (~80 °C) or ASA (~100 °C, UV-stable) for the vehicle. Board dissipates ~2.5–3 W, concentrated top-left — U2 at 1.1–1.4 W and Q_REV at 0.75 W are ~70 % of it. PLA's glass transition is ~60 °C and a van interior reaches 40 °C+, so vents belong over that corner and the buck wants to be uppermost if the case mounts vertically. RT1 derates from 70 °C, which protects the board, not the case. |
| ~~Stale `VANDIMMER-4CH2A.net`~~ | **Resolved 2026-09-12.** Was dated 17 Aug and predated two GPIO map changes, still showing gate inputs on IO4–IO7. Regenerated from `VANDIMMER-4CH2A.kicad_sch` and now agrees with the firmware's `PIN_GATE = {3, 9, 11, 13}`. Regenerate it again after any schematic change — it is an export, not a source. |

---

## 9. Verification gates

Placement and routing are not done until these pass. DRC alone is **not** sufficient —
KiCad reports a split net as an unconnected *item*, never an error, which is exactly how
Rev A shipped three dead pads.

| gate | tool | requirement |
|---|---|---|
| Courtyard overlap | `fpspace.py --courtyards` | after **any** footprint move; an overlap is a DRC error and is not predicted by pad clearance |
| Clearance | `route.py` | before pushing traces; note its blind spot — it does not synthesise a via at layer transitions, so sweep proposed vias separately |
| **Connectivity** | `connect.py` | **every net one connected group.** Run after the final zone refill, not before. A split reaching a connector pin is a blocker, not a note |
| DRC | `drc.sh` | 0 errors, with severity overrides reviewed rather than trusted |
| Buck integrity | `measure.py` | hot loop still < 15 mm² (Rev A: 2.51 mm²) |
| Plane necks | manual | at every mounting hole, against the current it carries |
| EMC | `emc.py` | expect the 42-of-60 finding to collapse once L2 is a continuous plane; confirm rather than assume |

Work with KiCad **closed** for file-based operations. `HANDOFF.md` records a session where
a later KiCad save wrote back its in-memory copy and silently reverted an on-disk board
outline edit, invisible to `git diff` because the save landed 53 seconds before the commit.

---

## 10. Decision log

| # | decision | date |
|---|---|---|
| 1 | 4-layer, L1 sig / L2 GND / L3 power / L4 sig | 2026-09-12 |
| 2 | 12 V primary for addressable runs; 5 V for short accents | 2026-09-12 |
| 3 | PF1/PF2 2 A → 3 A | 2026-09-12 |
| 4 | On-board antenna (`-1`) default, `-1U` + external as fallback | 2026-09-12 |
| 5 | Antenna overhangs the board edge | 2026-09-12 |
| 6 | Single harness face; J2 relocated to the left edge | 2026-09-12 |
| 7 | J1 (USB-C) on the harness face, off the left edge | 2026-09-12 |
| 8 | Mounting holes isolated; bottom-left corner separates J2 from J3 | 2026-09-12 |
| 9 | J3–J6 pin order swaps, pin 1 = LED− | 2026-09-12 |
| 10 | Route `J11.1`, `J9.1`, `J10.1` | 2026-09-12 |
| 11 | Board stays ≤100 × 100 mm; not shrunk below what the layout wants | 2026-09-12 |
