# VANDIMMER v2.0 — Claude Code Implementation Prompt

## Before you start

Install and verify, in this order:

1. **KiCad 9 or 10** — the MCP servers below need a real install
2. **One MCP server:**
   - `oaslananka/kicad-mcp-pro` — KiCad 10.x, back-compatible to 8. Ships worked
     examples for buck converters and USB-C power, which is exactly this board.
   - `mixelpixx/Konnect` — Rust, native KiCad 10 IPC plugin, ~171 tools, JLCPCB
     parts catalog with live stock and pricing. AGPL, free for individuals.
   - Konnect's JLC integration is the stronger option if you want the parts
     verification step automated.
3. **Confirm the MCP server is connected** before issuing the prompt — ask it to
   list its tools.

Put `VANDIMMER-4CH-2ADDR-SPEC-v2.0.md` in the working directory.

---

## The prompt

Copy everything below the line into Claude Code.

---

I'm building a 4-channel PWM LED dimmer with 2 addressable LED outputs for a
campervan. The full hardware specification is in
`VANDIMMER-4CH-2ADDR-SPEC-v2.0.md` in this directory. Read it fully before
doing anything else.

Context you need:

- I'm a software engineer with solid electronics and mechanical background.
  Don't explain fundamentals. Do push back if you think something in the spec
  is wrong — it was written without KiCad tooling available and hasn't been
  validated against real libraries or real part availability.
- Target is JLCPCB, 2-layer, Economic assembly (top-side SMD only).
- This goes in a Mercedes Sprinter. Vibration, thermal cycling and 12 V
  automotive transients are real constraints, not theoretical ones.
- There are older `vandimmer-8ch` KiCad files from a previous attempt.
  **Ignore them entirely.** They were hand-written s-expressions produced
  without KiCad, have unresolved footprint references and no connectivity.
  Start clean.

### Phase 1 — Parts verification (do this first, do not skip)

Section 12 of the spec lists open items. Two are blocking:

1. **Buck regulator.** AP64350 (40 V) vs TPS54360 (60 V). Check both against
   live JLCPCB stock, including whether they're Basic or Extended parts. I need
   a recommendation with the 24 V-capability tradeoff stated.
2. **Q_REV reverse-polarity P-FET.** Requirement is Vds ≥ 60 V, Rds(on) ≤ 30 mΩ
   at Vgs = −10 V, DPAK. The spec lists candidates I'm not confident about.
   Find what JLC actually stocks.

Also verify:
- ESP32-S3-WROOM-1U availability and whether the N16R8 variant is what's
  stocked (if so, GPIO33–37 are consumed by octal PSRAM and the §7 pin map
  needs revising — flag this, don't silently change it)
- 4× N-channel MOSFET, DPAK, **characterised at Vgs = 2.5 V not 4.5 V**. This
  matters: a 4.5 V-rated part driven from 3.3 V sits in partial enhancement and
  its dissipation stops being predictable.
- 74AHCT125 in SOIC-14

**Stop after Phase 1 and give me the results.** Do not start the schematic
until I've confirmed the part choices.

### Phase 2 — Schematic

Hierarchical sheets: Power, MCU, PWM, Addressable.

Build to spec sections 4, 5, 6, 7. Points I want particular care on:

- The §6.1 jumper selects V+ between VIN and 5 V per addressable output. Data
  is **always** shifted to 5 V regardless of jumper position.
- Two of the four 74AHCT125 channels go to unpopulated pads for future
  APA102 clock lines (§6.2)
- §12 lists an optional input LC filter as unpopulated footprints — include it
- ERC must be clean. Don't suppress warnings without telling me why.

### Phase 3 — Layout

Follow §8. Treat **§8.5 (Buck Layout Rules) as a gate, not a guideline** — it
exists because we consciously chose 2-layer over 4-layer and that decision is
only safe if the hot loop is right.

Specifically, before you tell me layout is done, verify and report:

- Hot loop enclosed area (Cin+ → VIN → GND → Cin−). Target < 15 mm². **Measure
  it and give me the number.**
- Zero vias inside that loop
- Bottom GND pour continuous under the buck, keepout enforced
- Switch node copper area minimised, no parallel signal within 3 mm
- Buck ≥ 20 mm from the WROOM module
- Thermal via arrays under buck and Q_REV tabs
- Feedback divider not routed under inductor or switch node

Also confirm the §8.6 thermal assumptions hold given actual pour areas achieved
— if you can't get 645 mm² on the buck, tell me and we'll recalculate rather
than quietly accept a hotter board.

### Phase 4 — Manufacturing output

- DRC clean, plus JLC DFM check
- Gerbers, drill, BOM, CPL in JLCPCB format
- **Verify part rotations against the JLC library** — they differ from KiCad
  defaults for several packages and this is a common cause of assembled boards
  with reversed parts
- Connectors are hand-fitted after assembly (§10), so exclude them from the CPL
  but keep them in the schematic and on the board

### Working style

- Stop at each phase boundary for my review
- If the spec is wrong, say so rather than implementing it faithfully
- Numbers over adjectives — "hot loop is 11 mm²" not "hot loop is tight"

---

## Things to watch for

Flag these back to me if the implementation turns them up:

| Risk | Why |
|---|---|
| WROOM-1U N16R8 pin conflict | Octal PSRAM eats GPIO33–37, breaks §7 UART1 assignment |
| WS2815 at 5 V logic | Datasheet says VIH = 0.7 × VDD = 8.4 V. Real silicon accepts 5 V. Tested-not-guaranteed — §11 item 8 |
| Buck pour area shortfall | §8.6 thermal budget assumes 645 mm². Less means recalculate |
| Extended-part fees | ~$3 per part type at JLC. Adds up if several parts aren't Basic |
| JLC part rotations | Differ from KiCad defaults, causes reversed placements |
