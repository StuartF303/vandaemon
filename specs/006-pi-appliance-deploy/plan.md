# Implementation Plan: Headless Pi-5 Appliance Deployment

**Branch**: `006-pi-appliance-deploy` | **Date**: 2026-06-22 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `specs/006-pi-appliance-deploy/spec.md`

## Summary

Deliver a near-zero-touch, flashable arm64 Raspberry Pi OS image (built with pi-gen) that boots a Pi 5
from NVMe straight into a running VanDaemon stack (API + web + locally-hosted Mosquitto broker) on the
van LAN, per ADR-001. Approach: (1) consolidate to one authoritative root `docker-compose.yml`
(api + web + mqtt) and delete the divergent `docker/docker-compose.yml`; (2) make Mosquitto the owned
broker (persistence, off-by-default Cerbo bridge, single-flag auth); (3) publish prebuilt multi-arch
(`linux/arm64`) api/web images to GHCR via a buildx CI workflow; (4) a pi-gen custom stage that bakes
Docker + the stack + the pulled images + a one-shot first-boot configurator into the `.img`; (5) a
secondary SD-installer fallback; (6) `docs/deployment/pi-appliance-setup.md` cross-linked from
PROJECT_PLAN.md and ADR-001.

**Risk class (loop-playbook §4): mixed B/C, gated at strictest = C.** Class-B artifacts are validated by
static checks (the exit condition); all on-device behaviour is delivered as a Class-C human-verification
checklist. **No self-merge; no claim of on-device success.**

## Technical Context

**Language/Version**: Bash (pi-gen stage scripts, firstboot, SD-installer); YAML (Compose, GitHub Actions); Mosquitto config; Markdown docs. No changes to the .NET 10 / Blazor source.
**Primary Dependencies**: pi-gen (Raspberry Pi OS image builder, run via its Docker build path); Docker Engine + Compose v2 (on the appliance, installed at image-build time); `docker buildx` (multi-arch CI); GHCR (image registry); `eclipse-mosquitto:2.0`; `mcr.microsoft.com/dotnet/{sdk,aspnet}:10.0` (multi-arch, used by existing Dockerfiles).
**Storage**: Docker named volumes on the appliance — `api-data`, `api-logs`, `mqtt-data`, `mqtt-logs` (on the NVMe). No change to the app's JSON two-tier model.
**Testing**: No `dotnet test` coverage for this infra. Class-B static gate = `docker compose config`, `mosquitto -c <conf>` config test (via a mosquitto container), `shellcheck` on shell scripts, `actionlint`/`yamllint` on YAML, `docker buildx build --platform linux/arm64` dry build. Class-C = on-hardware checklist.
**Target Platform**: Raspberry Pi 5 (4 GB), arm64, Raspberry Pi OS Lite (Bookworm), booting from NVMe (NVMe HAT). Build host: Linux / WSL2 / Docker (pi-gen is **not** native to Windows — flagged).
**Project Type**: Deployment/infrastructure (no application source-layer changes). New top-level `deploy/` tree + `.github/workflows/` + `docs/`.
**Performance Goals**: First boot to all-services-healthy within a few minutes (human-observed, Class C). No change to the app's <500 ms real-time target (local broker only helps).
**Constraints**: Offline-first at runtime (GHCR is a **build-time-only** dependency — the appliance runs with no internet, satisfying Constitution III); reproducible & reversible build; no committed secrets (`.example` only); arm64 target; bare-metal dev workflow must keep working unchanged.
**Scale/Scope**: One controller per van; a handful of MQTT/Modbus streams + the small .NET app (ADR-001).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Verdict | Notes |
|-----------|---------|-------|
| I. Plugin-First Hardware Abstraction | ✅ N/A | No hardware access added to app code. Hosting a broker is infrastructure; the existing `MqttLedDimmer` plugin is unchanged and merely points at a local broker. |
| II. Real-Time Reliability | ✅ Pass | Unaffected; a local broker reduces messaging latency vs a remote one. No latency-critical path changed. |
| III. Offline-First & Local Storage | ✅ Pass (reinforced) | The appliance runs fully offline; the local Mosquitto is the on-LAN fabric. **GHCR is used only at build time**, not at runtime — documented explicitly so it doesn't become a runtime cloud dependency. |
| IV. Test-Driven Hardware Integration | ✅ N/A | No new sensor/control plugin. The Class-B static checks are the objective gate that substitutes for `dotnet test` here; Class-C work is human-gated, not auto-claimed. |
| V. Clean Architecture | ✅ Pass | No source layering touched. api→broker wiring is via environment override only; `appsettings.json` defaults unchanged. |
| Architecture: Safety/Fail-Safe | ✅ Pass | `restart: unless-stopped` + healthchecks give service-level resilience; persistence preserves retained/LWT state across restarts. No control-actuation change. |
| §IV.4 No autonomous irreversible actions | ✅ Pass | Flashing the NVMe, the one-time EEPROM `BOOT_ORDER` change, and SD-install are **human steps in the Class-C checklist** — never executed by the loop. The pi-gen build runs in a sandboxed Docker builder (no irreversible action on the host). |
| §XIII.5 No green-washing | ✅ Pass | On-device outcomes (SC-001/005/007) are explicitly human-verified; the loop stops at tested artifacts + checklist. |

**Result: PASS, no violations.** Complexity Tracking table not required.

## Project Structure

### Documentation (this feature)

```text
specs/006-pi-appliance-deploy/
├── plan.md              # This file
├── research.md          # Phase 0 — decisions + rationale (pi-gen, NVMe boot, image pre-load, firstboot)
├── data-model.md        # Phase 1 — config-file schema, volumes, image-naming entities
├── quickstart.md        # Phase 1 — build + flash + verify walkthrough
├── contracts/
│   ├── stack-contract.md       # Compose services/ports/env/volumes + GHCR image names
│   ├── boot-config.md          # boot-partition config-file field contract
│   └── mqtt-broker-contract.md # broker config: persistence, bridge stanza, auth toggle
└── tasks.md             # Phase 2 (/speckit.tasks — NOT created here)
```

### Source Code (repository root)

```text
docker-compose.yml                      # CONSOLIDATED authoritative stack (api+web+mqtt) — EDIT
docker/docker-compose.yml               # DELETE (duplicate second stack)
docker/mosquitto/config/mosquitto.conf  # EDIT — persistence/LWT, off-by-default Cerbo bridge, auth pointers
docker/mosquitto/config/*.example       # KEEP (passwd.example, acl.conf.example) — referenced by auth toggle
docker/Dockerfile.api, Dockerfile.web   # UNCHANGED (already multi-arch capable); used by CI buildx

.github/workflows/
└── publish-images.yml                  # NEW — buildx multi-arch (linux/arm64,amd64) → GHCR

deploy/pi/                              # NEW — appliance build assets
├── build.sh                            # pi-gen wrapper (Docker build path); pulls/saves arm64 images
├── README.md                           # how to build the image (Linux/WSL2/Docker; NOT Windows-native)
├── config/
│   └── pi-gen-config.example           # pi-gen top-level config (img name, locale, base) — no secrets
├── stage-vandaemon/                    # custom pi-gen stage
│   ├── prerun.sh
│   ├── 00-install-docker/
│   │   ├── 00-packages                 # docker + compose plugin prerequisites
│   │   └── 00-run-chroot.sh            # install Docker Engine + compose, enable on boot
│   ├── 01-vandaemon-stack/
│   │   ├── files/                      # docker-compose.yml, mosquitto config, image tarballs slot
│   │   └── 00-run.sh                   # stage compose+config into /opt/vandaemon; load image tarballs
│   └── 02-firstboot/
│       ├── files/
│       │   ├── firstboot.sh            # one-shot: read boot-partition config, apply, compose up
│       │   ├── vandaemon-firstboot.service
│       │   └── vandaemon.service        # compose up -d on every boot (oneshot, RemainAfterExit)
│       └── 00-run.sh                   # install units, enable, write config.txt NVMe params
├── boot/
│   └── vandaemon-config.txt.example    # the single FAT-partition config file template (no secrets)
└── sd-installer/                       # SECONDARY fallback
    ├── README.md                       # clearly-marked fallback
    └── install-to-nvme.sh              # boot-from-SD → image rootfs onto NVMe → reboot to NVMe

docs/deployment/pi-appliance-setup.md   # NEW — flash/verify/update/backup + Class-C checklist
docs/deployment/adr/ADR-001-controller-soc.md  # EDIT — tick action items 1–3, cross-link
PROJECT_PLAN.md                         # EDIT — record Pi 5/NVMe target, cross-link the guide
DEPLOYMENT.md, DOCKER.md, hw/LEDDimmer/README.md, docker/mosquitto/README.md  # EDIT — repoint compose refs
```

**Structure Decision**: A new `deploy/pi/` tree holds all appliance build assets, isolated from the
app source (Clean Architecture untouched). The Compose stack is consolidated to the **repo-root**
`docker-compose.yml` so the same file serves dev and appliance; the `docker/` duplicate is removed.
CI image-publishing lives under `.github/workflows/`. Feature docs live under the spec dir; the
operator-facing guide lives in `docs/deployment/` next to ADR-001.

## Complexity Tracking

*No constitution violations — table intentionally omitted.*
