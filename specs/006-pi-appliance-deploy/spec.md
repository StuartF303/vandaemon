# Feature Specification: Headless Pi-5 Appliance Deployment

**Feature Branch**: `006-pi-appliance-deploy`
**Created**: 2026-06-22
**Status**: Draft
**Input**: User description: Headless-appliance deployment of VanDaemon onto the ADR-001 controller (Raspberry Pi 5, 4GB, NVMe boot — never microSD — hosting Mosquitto locally). Implementation of ADR-001 action items 1–3.

## Context & Governance

- **Track**: Core IoT (`PROJECT_PLAN.md`) — NOT the head-unit sub-project. The head unit is a UI *client* of this controller.
- **Source of decision**: `docs/deployment/adr/ADR-001-controller-soc.md`. This feature implements ADR-001 action items 1 (record the Pi 5 / NVMe-boot choice in deployment targets), partially 2 (reference the automotive 12 V front-end as a separate `hw/` task — not built here), and 3 (host Mosquitto on the controller).
- **Risk class (loop-playbook §4)**: **Mixed B/C, gated at the strictest = C.**
  - **Class B (build-only, statically verifiable)**: the consolidated compose file, Mosquitto config, GHCR buildx CI workflow, pi-gen stage scripts, first-boot/SD-installer scripts, and documentation.
  - **Class C (human-verified on hardware)**: that the produced `.img` actually boots the Pi 5 from NVMe, brings all services up on the van LAN, and survives ACC power cycles.
  - **No `dotnet test` exit condition exists** for this work, so even the buildable parts do not qualify for Class A / self-merge. Deliverable is a tested, reproducible build **plus** an on-hardware verification checklist. **No self-merge. No claim of on-device success.**

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Flash NVMe on a bench machine and boot a working controller (Priority: P1)

The installer (Stuart) has a Pi 5 with an NVMe drive in a USB-NVMe adapter on a bench computer. They edit one plain-text config file (hostname, network, SSH public key, broker credentials), flash the prebuilt VanDaemon appliance image to the NVMe, move the NVMe into the Pi, and power it on. With no keyboard, monitor, or interactive setup, the Pi joins the van LAN and serves a running VanDaemon (API, web UI, and the local MQTT broker) within a few minutes.

**Why this priority**: This is the headline near-zero-touch deliverable and the whole reason for the feature. Without it, nothing else matters.

**Independent Test**: Build the image (`build.sh`) and confirm it produces a flashable `.img`; on hardware (Class C), flash → boot → reach the web UI and `/health`, and confirm `1883` is reachable on the LAN.

**Acceptance Scenarios**:

1. **Given** the appliance image and an edited boot-partition config file, **When** the image is flashed to NVMe and the Pi is powered on, **Then** the Pi comes up with the configured hostname on the configured network, reachable over SSH using the supplied public key, with no interactive prompt.
2. **Given** a freshly booted appliance, **When** first boot completes, **Then** the API health endpoint returns healthy, the web UI loads, and the MQTT broker accepts a connection on port 1883 — all started automatically and set to restart on reboot/power loss.
3. **Given** the appliance has been power-cycled (simulating ACC-off/on), **When** it boots again, **Then** all services come back up automatically without manual intervention and persisted data (config, retained MQTT messages) survives.

---

### User Story 2 - One authoritative stack that doesn't break local dev (Priority: P1)

A developer working on a laptop runs the stack locally with one command and gets the full set of services (API, web, broker) — the same definition that ships on the appliance — and bare-metal `dotnet run` development continues to work unchanged.

**Why this priority**: The repo currently has two divergent compose files (root without a broker; `docker/` with one). Consolidating to a single authoritative definition is a prerequisite for the appliance and removes a standing footgun. Breaking dev is unacceptable.

**Independent Test**: `docker compose config` parses the single root file; the api service resolves the broker by service name; `dotnet run` of the API still reads `localhost` from `appsettings.json` (defaults unchanged).

**Acceptance Scenarios**:

1. **Given** the consolidated root compose file, **When** `docker compose config` is run, **Then** it validates with exactly one MQTT broker service and no duplicate/second stack.
2. **Given** the containerised API, **When** the stack starts, **Then** the API connects to the broker by its in-network service name, while the committed `appsettings.json` default remains `localhost` so bare-metal dev is unaffected.
3. **Given** existing documentation that referenced the deleted `docker/docker-compose.yml`, **When** a reader follows it, **Then** the references point to the consolidated file and the commands work.

---

### User Story 3 - Controller owns the MQTT broker (ADR-001 item 3) (Priority: P2)

The controller hosts its own Mosquitto broker so the messaging fabric (LED dimmers, future Victron bridge, the .NET app) survives the .NET app restarting. The broker persists retained messages and is last-will-friendly. A documented, off-by-default stanza is provided to later bridge the Victron Cerbo's broker into the local one. Auth is open on the trusted van LAN by default, with a single, documented flag to switch to password + ACL using the existing example files.

**Why this priority**: Directly realises ADR-001's broker-ownership decision and de-risks the Victron path, but the appliance can boot and serve the UI before the bridge/auth refinements, so it is P2 not P1.

**Independent Test**: `mosquitto -c <config>` passes a config-file test; the bridge stanza is present but commented; flipping the documented auth flag references the committed `passwd.example`/`acl.conf.example` (no real secrets).

**Acceptance Scenarios**:

1. **Given** the broker config, **When** the broker starts, **Then** persistence is enabled (retained messages and sessions survive restart) and the config passes a config-file validation.
2. **Given** the .NET app is restarted, **When** other MQTT participants are connected, **Then** they remain connected to the broker (the fabric outlives the app).
3. **Given** the operator wants authentication, **When** they follow the single documented flag/step, **Then** anonymous access is disabled and password/ACL auth is enabled using the provided example files — with no real credentials committed to the repo.

---

### User Story 4 - SD-installer fallback for the no-bench-machine case (Priority: P3)

An installer without a USB-NVMe adapter or bench machine writes a small installer image to a microSD, inserts it into the Pi, and powers on. The installer images the prebuilt root filesystem onto the NVMe, then reboots to NVMe; the SD is removed afterward. This path is explicitly secondary to the bench-flash path.

**Why this priority**: A convenience fallback for a real but less common situation. The primary path (US1) fully delivers the feature without it.

**Independent Test**: The installer scripts pass shellcheck and the procedure is documented end-to-end; on hardware (Class C) the SD boots, copies to NVMe, and the Pi subsequently boots from NVMe.

**Acceptance Scenarios**:

1. **Given** the installer SD image, **When** the Pi boots from it, **Then** it writes the appliance rootfs to the NVMe unattended and signals completion.
2. **Given** the copy completed, **When** the SD is removed and the Pi rebooted, **Then** the Pi boots from NVMe into the same running appliance as the primary path.

---

### Edge Cases

- **microSD as boot medium**: The image and docs MUST steer to NVMe/eMMC. Booting the *primary* appliance from a bare microSD is the documented anti-pattern (SD-rot, ADR-001) — the SD path (US4) uses SD only as a transient installer, not the running medium.
- **NVMe boot not enabled in firmware**: Pi 5 EEPROM `BOOT_ORDER` is separate firmware that cannot live in the rootfs image. If NVMe boot is not enabled, the Pi will not boot from NVMe — this MUST be a clearly documented one-time human step in the verification checklist.
- **Missing or malformed boot-partition config file**: First boot MUST fall back to safe, documented defaults (and MUST NOT expose unconfigured secrets) rather than hang waiting for interactive input.
- **No network / wrong WiFi credentials**: The appliance MUST still boot and run locally; the operator MUST be able to recover (e.g., re-edit the boot-partition config) without a monitor.
- **GHCR image pull fails at build time** (rate limit / auth / offline builder): the build MUST fail loudly and reproducibly, not silently produce an image without the app images baked in.
- **First boot before images are pulled**: if images were not pre-pulled, services MUST retry (restart policy) rather than enter a permanently failed state once connectivity is available.
- **Windows build host**: pi-gen does not run natively on Windows; the build MUST be documented to run under WSL2/Linux/Docker.
- **Secret leakage**: generated first-boot secrets MUST NOT be written to world-readable locations or committed; only `.example` files exist in the repo.

## Requirements *(mandatory)*

### Functional Requirements

**Consolidation (US2)**
- **FR-001**: The repository MUST have exactly one authoritative Compose stack defining the API, web, and MQTT broker services; the duplicate `docker/docker-compose.yml` MUST be removed.
- **FR-002**: All three services MUST declare an automatic restart policy; the API and broker MUST declare healthchecks; the web service MUST depend on the API being healthy.
- **FR-003**: The containerised API MUST reach the broker by its in-network service name via environment configuration, WITHOUT changing the committed application default (which remains `localhost` for bare-metal development).
- **FR-004**: Documentation that referenced the removed compose file MUST be updated to reference the consolidated file; the local development workflow MUST remain functional and be described.

**Broker ownership (US3, ADR-001 item 3)**
- **FR-005**: The MQTT broker configuration MUST enable persistence so retained messages and client sessions survive a broker restart.
- **FR-006**: The broker configuration MUST include a documented, disabled-by-default stanza for bridging an upstream (Victron Cerbo) broker into the local broker.
- **FR-007**: The broker MUST default to anonymous access suitable for a trusted private LAN, and MUST provide a single documented switch to enable password + ACL authentication using the existing committed example files.
- **FR-008**: No real broker credentials, keys, or generated secrets may be committed; only `.example` templates may exist in the repository.

**Image build (US1, ADR-001 items 1–2)**
- **FR-009**: There MUST be a documented, re-runnable build that produces a flashable arm64 Raspberry Pi OS Lite image containing a container runtime and the consolidated stack.
- **FR-010**: The build MUST obtain the API and web application images as prebuilt multi-arch (`linux/arm64`) images from a container registry, and MUST make them available to the appliance at first boot without requiring an internet round-trip for core start-up (pre-pulled at build time, with a documented first-boot-pull fallback).
- **FR-011**: A continuous-integration workflow MUST build and publish the API and web images as multi-arch images (including `linux/arm64`) to the registry.
- **FR-012**: The image MUST start the full stack automatically on boot via a managed system service, with the services configured to restart unless stopped.
- **FR-013**: The image MUST encode the expectation of NVMe boot (firmware/boot configuration for the NVMe HAT) and MUST document the one-time firmware boot-order step that cannot be baked into the root filesystem.
- **FR-014**: First-boot configuration (hostname, network, SSH public key, broker credentials) MUST be supplied via a single plain-text config file on the boot partition, consumed at build time and/or by a one-shot first-boot routine, with NO interactive setup required.
- **FR-015**: The build MUST be reproducible and MUST NOT perform any irreversible action on the build host (it runs in a sandboxed/containerised builder).

**SD-installer fallback (US4)**
- **FR-016**: A secondary, clearly-labelled installer path MUST exist that boots from microSD, images the prebuilt root filesystem onto the NVMe, and reboots to NVMe, after which the SD is removed. It MUST be documented as the fallback, not the primary path.

**Documentation (all stories)**
- **FR-017**: A deployment guide (`docs/deployment/pi-appliance-setup.md`) MUST cover: flashing steps, the config-file fields, first-boot expectations, verification (health endpoint, container status, broker reachable on 1883), the update procedure (re-flash vs pull-and-restart), and a backup note for the API-data and MQTT-data volumes.
- **FR-018**: The guide MUST be cross-linked from `PROJECT_PLAN.md` and `ADR-001`, and ADR-001 action items 1–3 MUST be annotated to reflect this work.
- **FR-019**: All deliverables MUST target arm64; any unavoidable x86-only assumption MUST be explicitly flagged.
- **FR-020**: The feature MUST ship an on-hardware verification checklist (Class C) covering boot-from-NVMe, services-up, broker-reachable, and ACC power-cycle survival, explicitly NOT claimed as passing until a human verifies on the device.

### Key Entities *(include if feature involves data)*

- **Appliance image**: the flashable arm64 `.img` artifact; contains OS, runtime, stack definition, pre-pulled app images, and first-boot tooling.
- **Boot-partition config file**: the single operator-editable input (hostname, network, SSH public key, broker credentials); lives on the FAT boot partition so it is editable from the bench after flashing.
- **Consolidated Compose stack**: the single authoritative service definition (API, web, broker) used for both appliance and local dev.
- **Broker configuration**: persistence settings, last-will/retained behaviour, the off-by-default Cerbo bridge stanza, and the auth toggle referencing example credential files.
- **Persistent volumes**: API-data and MQTT-data, whose survival across reboots and whose backup procedure are in scope.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: From a flashed NVMe and an edited config file, the appliance boots to a fully running stack (UI reachable, health endpoint healthy, broker reachable on 1883) with zero interactive steps and no attached keyboard/monitor. *(Class C — human-verified)*
- **SC-002**: The repository contains exactly one Compose stack with a broker service; `docker compose config` validates it, and bare-metal `dotnet run` development still works with unchanged committed defaults. *(Class B — statically verifiable)*
- **SC-003**: The broker configuration passes a config-file validation, has persistence enabled, contains the disabled-by-default Cerbo bridge stanza, and exposes a single documented auth switch — with no real secrets in the repository. *(Class B)*
- **SC-004**: The image build is re-runnable and produces a flashable arm64 image; the CI workflow publishes `linux/arm64` API and web images to the registry. *(Class B — build succeeds in a Linux/Docker builder)*
- **SC-005**: After a power cycle, all services restart automatically and persisted config + retained MQTT messages survive. *(Class C — human-verified)*
- **SC-006**: A reader can follow `pi-appliance-setup.md` end-to-end (flash → verify → update → back up) and ADR-001 items 1–3 are reflected as addressed. *(Class B — reviewable)*
- **SC-007**: The on-hardware verification checklist exists and is delivered; no item in it is marked passed without a recorded human observation on the device. *(governance gate)*

## Assumptions

- The registry is GHCR under the repo owner's namespace; images are public, or the appliance has documented pull access. If GHCR access requires auth at pull time, that is a documented config field, not a hardcoded secret.
- The Pi 5 has a supported NVMe HAT; the automotive 12 V front-end (ADR-001 item 2) is a separate `hw/` hardware task, referenced not built here.
- The van LAN is a trusted private network for the default (anonymous broker) posture; TLS/auth hardening is available via the documented switch and is out of scope to enable by default.
- The build host is Linux or runs the build under WSL2/Docker (pi-gen is not native to Windows).
- "Pre-pulled at build time" is achievable for the chosen runtime; if images cannot be baked into the rootfs cleanly, a first-boot pull with retry is the documented fallback (still no interactive step).

## Out of Scope / Needs-Hardware

- The automotive 12 V→5 V DC-DC front-end, load-dump protection, and clean-shutdown/hold-up circuitry (ADR-001 item 2 — separate hardware task).
- RS485/CAN HAT-vs-USB selection for the Modbus relay (ADR-001 item 4).
- Proving boot-from-NVMe, services-up, broker-on-LAN, and ACC-cycle survival on the physical Pi (Class C — delivered as a checklist for human verification, never auto-claimed).
- TLS for MQTT and multi-user ACL hardening beyond the single documented on/off switch.
- The CM5-on-carrier permanent-install path (ADR-001 item 6 — future).
