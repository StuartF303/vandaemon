# VDDimmer: possible future features

Ideas worth keeping that are not scheduled. Each entry says enough to be picked
up cold: what it is, why it works on this hardware, and what it would touch.

---

## Tunable white (CCT) on the PWM channels

**Raised:** 2026-10-05 · **Status:** idea, not scheduled · **Hardware change:** none, works on Rev A

### What

Warm-to-cool colour temperature control, not just brightness, using 12 V
tunable-white strip ("CCT" or "dual white", typically about 2700 K + 6000 K).

### Why it fits the board

A CCT strip has three wires: **V+, warm−, cool−**, common-positive with the two
white channels switched on the negative. The board's PWM outputs are exactly
that: all four share +12 V on pin 1 and the FET switches pin 2 (spec §5.1).

| Strip wire | Board pin |
|---|---|
| V+ | J3 pin 1 |
| warm− | J3 pin 2 (CH1) |
| cool− | J4 pin 2 (CH2) |

Channels 3+4 (J5/J6) make a second pair, so **one board drives two tunable-white
runs**.

With no firmware change this already works, but as two independent dimmers
("Warm", "Cool"), so changing brightness means moving both. Usable, clumsy.

### The feature

A per-pair setting that groups CH1+CH2 (and CH3+CH4) into one light with
**brightness** and **colour temperature**.

- **Constant-power mix:** `warm + cool = level`. The colour changes smoothly
  with no brightness jump. Total current stays at one channel's worth, so the
  2 A per-channel rating and the shared V+ pin never carry double current.
- **Gamma once, then split linearly.** Apply the CIE curve to the overall level,
  then divide the resulting duty between warm and cool in proportion to the
  colour temperature. Gamma-correcting each channel separately makes the
  midpoint visibly dim and shifts the colour.
- Kelvin endpoints are settings (default 2700 / 6000 K), since strips vary.

### What it touches

- **Firmware:** pairing settings in NVS; a `cct` path in `control.*` that sets
  both channels at once; both channel states still published to MQTT.
- **Bluetooth protocol** (see the local-control spec): one extra op, e.g.
  `{"op":"cct","light":0,"level":180,"kelvin":3500}`, and the `state` reply
  reporting pairs.
- **App:** a colour-temperature slider under the brightness slider for paired
  channels.
- **VanDaemon:** nothing required, since it keeps seeing two plain channels and
  stays consistent because the board publishes both levels. A proper CCT
  control in the backend and UI is a separate later step.

### Not this: the addressable outputs

RGB WS2812 strip can imitate warm or cool white by mixing, but the light is
tinted with poor colour rendering, which is fine for accents and not for room
lighting. RGBW or addressable CCT strip (SK6812 RGBW, SK6812 WWA) gives real
white, but the firmware would need a 4-byte pixel mode, and on Rev A the
addressable outputs are 5 V only (J9/J10's 12 V position is unrouted), capped
at about 33 LEDs at full white. Keep those outputs for accent and colour.

### Related

- `docs/superpowers/specs/2026-09-26-vddimmer-local-control-design.md`: the
  Bluetooth protocol and app this would extend. Design it into the protocol
  before the app ships, rather than retrofitting it.
- `VANDIMMER-4CH-2ADDR-SPEC-v2.0.md` §5.1: output pinout and current limits.
