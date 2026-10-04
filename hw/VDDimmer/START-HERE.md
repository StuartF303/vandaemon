# VANDIMMER v2 — session kickoff prompt

Paste the block below into a fresh Claude Code session in `C:\Projects\vandaemon`
after restarting (so the `konnect` and `pcbparts` MCP servers load).

---

Design the VANDIMMER-4CH+2A board end to end, through to manufacturing files I can verify.

**First, verify the toolchain and report the result before anything else:**

1. Run `/mcp` — `konnect` and `pcbparts` must both show connected. If konnect is missing, its binary is `C:\Projects\konnect\bin\konnect.exe`.
2. Confirm KiCad 10 is the active install and that Preferences → Plugins → "Enable KiCad API" is ticked (`api.enable_server` in `%APPDATA%\kicad\10.0\kicad_common.json`). Konnect's PCB tools need it, and most need KiCad open with the board loaded. Tell me if I need to do either.
3. Ask konnect to list its toolsets so we both know what's available.

**Then read fully, before doing anything else:**

- `hw/VDDimmer/VANDIMMER-4CH-2ADDR-SPEC-v2.0.md` — the hardware spec
- `hw/VDDimmer/VANDIMMER-v2-IMPLEMENTATION-PROMPT.md` — my brief: phases 1–4 and the risk table at the end. **Ignore its "Before you start" section** — it recommends kicad-mcp-pro and a KiCad 9 setup that are both superseded; we're on KiCad 10 with Konnect already installed.

**Context:**

- I'm a software engineer with a solid electronics and mechanical background. Don't explain fundamentals.
- The spec was written without KiCad tooling and has never been validated against real libraries or live part stock. **Push back where it's wrong** rather than implementing it faithfully.
- Target is JLCPCB, 2-layer, Economic assembly (top-side SMD only). This goes in a Mercedes Sprinter — vibration, thermal cycling and 12 V automotive transients are real constraints, not theoretical ones.
- `hw/LEDDimmer-8ch/` is a failed earlier attempt: hand-written s-expressions, unresolved footprint references, no connectivity. **Ignore it entirely.** Start clean.
- Create the new KiCad project in `hw/VDDimmer/`.

**How I want you to work:**

Run phases 1–4 straight through. Don't stop at each phase boundary for sign-off — I want a complete design to verify at the end. Do stop and ask only if a decision is genuinely blocking: nothing suitable in JLC stock, or a spec contradiction you can't resolve. For choices that are just a tradeoff I should know about (AP64350 vs TPS54360 and the 24 V question is the obvious one), make a recommendation, state the tradeoff in one line, and carry on with your recommendation rather than waiting on me.

Report at each phase boundary so I can follow along, but keep going. Numbers over adjectives throughout — "hot loop is 11 mm²", not "hot loop is tight".

**Phase 1 — parts verification.** Use `pcbparts` for live JLC stock and Basic/Extended status, cross-checked against konnect's catalog. Resolve the §12 blockers (buck regulator, Q_REV P-FET at Vds ≥ 60 V / Rds(on) ≤ 30 mΩ at Vgs = −10 V, DPAK). Verify the ESP32-S3-WROOM-1U variant actually stocked, the 4× N-channel MOSFETs — **characterised at Vgs = 2.5 V, not 4.5 V** — and 74AHCT125 in SOIC-14. If the N16R8 octal-PSRAM GPIO33–37 conflict with the §7 pin map is real, flag it and propose a revised map; don't silently rewrite.

**Phase 2 — schematic.** Hierarchical sheets: Power, MCU, PWM, Addressable. Build to §4–§7. Particular care on: the §6.1 jumper selecting V+ between VIN and 5 V per addressable output, with data **always** shifted to 5 V regardless of jumper position; two of the four 74AHCT125 channels going to unpopulated pads for future APA102 clock lines (§6.2); the §12 optional input LC filter as unpopulated footprints. ERC clean — if you suppress a warning, tell me why.

**Phase 3 — layout.** Follow §8. Treat **§8.5 as a gate, not a guideline** — 2-layer over 4-layer was a conscious choice and is only safe if the hot loop is right. Before you tell me layout is done, measure and report: hot loop enclosed area Cin+ → VIN → GND → Cin− (target < 15 mm²), vias inside that loop (must be zero), bottom GND pour continuity under the buck with keepout enforced, switch-node copper area and clearance to parallel signals (≥ 3 mm), buck-to-WROOM distance (≥ 20 mm), thermal via arrays under buck and Q_REV tabs, and feedback divider not routed under the inductor or switch node. If §8.6's 645 mm² buck pour isn't achievable, say so and recalculate rather than quietly accepting a hotter board.

**Phase 4 — manufacturing.** DRC clean plus a JLC DFM check. Gerbers, drill, BOM and CPL in JLCPCB format. **Verify part rotations against the JLC library** — they differ from KiCad defaults for several packages and are a common cause of assembled boards with reversed parts. Connectors are hand-fitted after assembly (§9/§10), so exclude them from the CPL but keep them in the schematic and on the board.

**Deliverable:** a finished KiCad project in `hw/VDDimmer/`, plus a short verification report giving me the §8.5 numbers, ERC/DRC status, the final BOM with LCSC part numbers and the Basic/Extended split with cost, and an explicit list of everything you changed from the spec and why.
