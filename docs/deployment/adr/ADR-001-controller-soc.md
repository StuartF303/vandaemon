# ADR-001: Headless central-controller hardware (SoC) and MQTT broker hosting

**Status:** Proposed
**Date:** 2026-06-22
**Deciders:** Stuart
**Track:** Core IoT (`PROJECT_PLAN.md`) — *not* the head-unit sub-project. The Teyes/FYT head unit and
its launcher shell (specs 004/005) are **UI clients** of this controller, not the controller itself.

## Context

VanDaemon's server (the .NET API + Blazor host) already plays the role of a central controller: it is
the MQTT client for the LED dimmers and the Victron Cerbo, the Modbus master for the relay board at
`192.168.1.230`, the SignalR real-time hub, and the JSON config store. The constitution assumes this
runs "on the local network only, default: Raspberry Pi on van's WiFi," and `PROJECT_PLAN.md` names a
"Raspberry Pi 4 (local deployment)" target. But that has only ever been a *default*, never a
deliberate hardware decision against the constraints that actually matter in a van.

Two questions have never been recorded as decisions:

1. **Which SoC / small box** should be the always-on headless controller, judged on van-specific
   forces — 12 V leisure-battery supply with ACC-off brownouts, idle power draw (every watt is amp-hours
   off the battery), storage that survives unclean power-downs, RS485/CAN IO for Modbus, and thermal in
   a sealed enclosure.
2. **Should the controller host the MQTT broker itself** (a local Mosquitto), or stay client-only as
   today (MQTTnet dialling out to existing brokers such as the Cerbo's)?

Forces at play:

- **Power budget.** A controller that's always on (or on whenever the van's 12 V is live) draws from
  the leisure battery. ~3 W vs ~8 W idle is ~0.25 A vs ~0.65 A at 12 V — over a multi-day stop with no
  charging, that difference is real.
- **Power *quality*.** Vehicle 12 V is noisy and sags hard on cranking / ACC transitions. Whatever box
  is chosen needs a proper automotive-grade 12 V→5 V (or →12 V) DC-DC buck with load-dump tolerance,
  and a clean-shutdown or always-on-with-hold-up story so the filesystem isn't corrupted by ACC-off.
- **Storage durability.** SD-card rot under frequent unclean power-downs is the classic Pi-in-a-vehicle
  failure mode. eMMC or an SSD (NVMe/USB) materially de-risks this.
- **IO.** Modbus RTU and any future CAN want native RS485/CAN or a well-supported HAT; USB-RS485 works
  but is one more thing to fall out.
- **Broker independence.** If dimmers/Victron/Modbus-bridge rendezvous at a broker the controller owns,
  the messaging fabric survives the .NET app restarting/crashing; today, if the app is the only MQTT
  participant, there is no persistent fabric.
- **Reversibility / cost.** This is a van hobby project; favour cheap, swappable, well-documented kit
  over anything exotic.

## Decision

**1. Controller hardware: Raspberry Pi 5 (4 GB) booting from eMMC/NVMe, not SD, on an automotive
12 V→5 V buck with hold-up.** It wins the power/IO/ecosystem trade for this use case (see analysis).
The single non-negotiable mitigation is **not booting from a bare microSD** — use an NVMe HAT or a CM5
+ carrier with onboard eMMC — to kill the SD-rot failure mode.

**2. Host the MQTT broker on the controller (Mosquitto).** The controller owns a local broker; LED
dimmers, the Victron bridge, the Modbus bridge, and the .NET app all connect to it. This decouples the
messaging fabric from the .NET app's lifecycle and gives one authoritative broker on the van LAN. The
existing MQTTnet usage changes from "client of assorted brokers" to "client of our own local broker"
(the Victron path may still bridge from the Cerbo's broker into ours).

Both decisions are **reversible** — the .NET app is containerised and architecture-agnostic, so a later
move to an N100 or CM5 is a redeploy, and Mosquitto can be dropped if the broker-hosted model
disappoints.

## Options Considered

### Option A: Raspberry Pi 5 (4 GB), eMMC/NVMe boot — **chosen**

| Dimension | Assessment |
|-----------|------------|
| Idle power | ~3 W headless/WLAN (~0.25 A @12 V) — best of the capable options |
| Power quality | Needs a quality 12 V→5 V buck; Pi 5 is fussier about supply than Pi 4, so spec the DC-DC properly |
| Storage durability | **Good only if NVMe/eMMC** — bare microSD is the known failure mode; mandate non-SD boot |
| IO (RS485/CAN) | HATs widely available; USB-RS485 fallback well-supported |
| Ecosystem / familiarity | Huge; matches existing `raspberry-pi-setup.md`, Docker images already target it |
| Cost | Low; board + NVMe HAT + buck is modest |

**Pros:** lowest idle draw of the capable boards; best-documented; .NET/Docker path already exists;
cheap and swappable; GPIO/HAT ecosystem for Modbus/CAN.
**Cons:** ARM (fine for .NET, but check any native deps); microSD trap must be deliberately avoided;
Pi 5 wants a solid 5 V/5 A-class supply — the automotive buck must be sized for it.

### Option B: Raspberry Pi CM4 / CM5 on a carrier (onboard eMMC)

| Dimension | Assessment |
|-----------|------------|
| Idle power | CM4 ~2–3 W; CM5 between CM4 and Pi 5 |
| Power quality | Carrier board can integrate the 12 V front-end cleanly — arguably the tidiest in-dash build |
| Storage durability | **Best** — onboard eMMC, no SD at all |
| IO | Depends on carrier; many expose RS485/CAN/PCIe directly |
| Ecosystem / familiarity | Good, but carrier selection adds a choice and slightly more niche |
| Cost | Higher (module + carrier); availability historically patchier than full Pis |

**Pros:** eMMC kills SD-rot by default; a purpose-built carrier is the cleanest permanent install;
PCIe/NVMe and CAN often first-class.
**Cons:** more integration work and cost; carrier is another part to pick/support; less "grab one off
the shelf" than a Pi 5.

### Option C: Intel N100 mini-PC (x86)

| Dimension | Assessment |
|-----------|------------|
| Idle power | ~6–8 W (~0.5–0.65 A @12 V) — roughly 2× the Pi 5 |
| Power quality | Many N100 boxes already take ~12 V DC input — *can* simplify the front-end, but still need load-dump protection |
| Storage durability | **Excellent** — NVMe/SATA SSD standard |
| IO | No native RS485/CAN; rely on USB adapters (more so than the Pi HAT route) |
| Ecosystem / familiarity | x86 + Docker is the most frictionless for .NET; no ARM caveats at all |
| Cost | Higher unit cost; but a complete, cased, SSD-equipped box |

**Pros:** x86 removes any ARM/native-dep risk; SSD storage; genuinely more CPU headroom; some accept
~12 V input directly.
**Cons:** ~2× idle draw matters on a leisure battery; weaker native industrial IO (USB-RS485/CAN);
bigger/hotter; overkill for the current workload.

### Option D: Le Potato / Orange Pi / other budget SBC

| Dimension | Assessment |
|-----------|------------|
| Idle power | Comparable to Pi, board-dependent |
| Power quality | Same buck requirement |
| Storage durability | Usually microSD/eMMC depending on board |
| IO | Variable; CAN/RS485 support patchy |
| Ecosystem / familiarity | **Weakest** — smaller community, more driver/OS friction |
| Cost | Lowest |

**Pros:** cheapest.
**Cons:** software/driver support and longevity are the risk; not worth it for an always-on controller
you don't want to babysit. Listed for completeness; not recommended.

## Trade-off Analysis

The decision is dominated by **idle power on a battery** and **storage survival across unclean
power-downs**, with **native industrial IO** as the tie-breaker.

- Pi 5 vs N100: the N100's x86 convenience and SSD are attractive, but ~2× idle draw (~4–5 W more,
  ~0.35 A @12 V continuously) is the wrong direction for an off-grid leisure battery, and the current
  workload (a handful of MQTT/Modbus streams + a small .NET app) doesn't need x86 horsepower. The Pi's
  HAT ecosystem also gives cleaner RS485/CAN than the N100's USB-adapter route.
- Pi 5 vs CM4/CM5: CM is arguably the *better permanent install* (eMMC, integrated carrier front-end),
  and is the natural **upgrade path** once the build is proven. The Pi 5 wins now on cost,
  off-the-shelf availability, and matching the existing Pi-targeted docs/images — lower friction to get
  a first controller running on the bench.
- The microSD failure mode is real enough that "Pi 5 **on NVMe/eMMC**" is part of the decision, not an
  afterthought. A Pi 5 booting from a bare microSD would not have been chosen.

On the **broker**: hosting Mosquitto locally costs one managed service but buys a messaging fabric that
outlives app restarts and gives a single source of truth on the LAN — directly useful for the
ACC-off/sleep survival concerns already on the head-unit roadmap and in the project brief. The downside
(another moving part) is small and well-trodden.

## Consequences

**Easier:**
- One authoritative broker on the van LAN; devices keep talking even when the .NET app restarts.
- Lowest sustainable idle draw among capable options; better multi-day-stop battery life.
- Reuses the existing `raspberry-pi-setup.md` / Docker path; minimal new ground.

**Harder / new work:**
- Must design/spec an **automotive 12 V front-end**: load-dump-tolerant DC-DC sized for Pi 5, plus a
  clean-shutdown-on-ACC-off (or always-on with hold-up) strategy. This is its own hardware task.
- Must **not** ship on microSD — adds an NVMe HAT (or CM5+eMMC) to the BOM and image/boot setup.
- Adds **Mosquitto** as a managed service (config, auth, retained topics, bridge from the Cerbo).
- Verify no .NET native dependency misbehaves on ARM (expected fine, but check before committing).

**To revisit:**
- Re-evaluate **CM5-on-carrier** when moving from bench to permanent install (cleaner integration).
- Reconsider **N100** only if the workload grows CPU-bound or x86-only deps appear.
- Broker auth/TLS posture once more devices join (currently a trusted private van LAN).

## Action Items

> Items 1–3 are implemented by feature **006-pi-appliance-deploy** — see
> [`docs/deployment/pi-appliance-setup.md`](../pi-appliance-setup.md) and
> `specs/006-pi-appliance-deploy/`. Item 5 (ARM64 sanity) is exercised by that feature's CI/build.

1. [x] Ratify SoC choice (Pi 5 4 GB, NVMe/eMMC boot) — recorded in `PROJECT_PLAN.md` deployment-targets
   (no longer a bare default). *(006)*
2. [~] Spec the **automotive 12 V power front-end** (DC-DC + load-dump + clean-shutdown/hold-up) as a
   `hw/` hardware task — **still open** (referenced by 006 as out-of-scope hardware; not yet built).
3. [x] **"Host Mosquitto on the controller"** delivered by 006: single owned broker in the consolidated
   `docker-compose.yml`, persistence + last-will-friendly config, off-by-default **Cerbo bridge** stanza
   (Victron draft 0003 hook), and a single-flag passwd/ACL auth path. *(006)*
4. [ ] Confirm RS485/CAN approach for the Modbus relay (HAT vs USB-RS485) against the chosen board.
5. [x] Validate the .NET stack + plugins build/run on ARM64 — covered by 006's multi-arch buildx CI
   (`linux/arm64`) and the local arm64 build check. *(006)*
6. [ ] Note the CM5-on-carrier upgrade path in the permanent-install plan.

## Notes / sources

Idle-power figures used in the analysis (Jun 2026): Pi 5 ~3.0 W headless/WLAN (~3.6 W with
Ethernet/HDMI/USB); Intel N100 ~6–8 W idle. At 12 V that is roughly 0.25 A vs 0.5–0.65 A continuous —
the basis for the battery-life argument above. Verify against your own meter once a candidate board is
on the bench, as figures vary with peripherals and OS tuning.
