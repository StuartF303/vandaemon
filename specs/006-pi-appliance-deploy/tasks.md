# Tasks: Headless Pi-5 Appliance Deployment

**Feature**: `006-pi-appliance-deploy` | **Spec**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)

**Risk class**: mixed **B/C**, gated at strictest = **C**. Every task is tagged `[Class B]` (statically
verifiable — the done-condition is a static check) or `[Class C]` (human-on-hardware — authored as a
checklist item, **never auto-run, never auto-claimed**). There is **no `dotnet test`** for this feature.

**Class-B static gate** (from `research.md` → "Class-B static verification gate" and each contract's
"Static acceptance"). Run on a Linux/Docker host (WSL2 on the Windows dev box):

```
docker compose config -q
docker run --rm -v "$PWD/docker/mosquitto/config:/c" eclipse-mosquitto:2.0 mosquitto -c /c/mosquitto.conf
shellcheck deploy/pi/**/*.sh
actionlint .github/workflows/publish-images.yml
yamllint docker-compose.yml .github/workflows/publish-images.yml
docker buildx build --platform linux/arm64 -f docker/Dockerfile.api .
docker buildx build --platform linux/arm64 -f docker/Dockerfile.web .
```

**Hard rule**: NO task flashes hardware, edits the Pi EEPROM, or claims on-device success. Those are
documented for a human in the Class-C checklist (T030).

---

## Phase 1: Setup

- [x] T001 [Class B] Create the appliance build tree skeleton with placeholder dirs and a top-level note, matching plan.md "Source Code" layout: `deploy/pi/`, `deploy/pi/config/`, `deploy/pi/stage-vandaemon/{00-install-docker,01-vandaemon-stack/files,02-firstboot/files}/`, `deploy/pi/boot/`, `deploy/pi/sd-installer/`. Done: directories exist and are tracked (`.gitkeep` where empty).
- [x] T002 [Class B] Author `deploy/pi/README.md` stating the build runs on Linux/WSL2/Docker only (pi-gen NOT native to Windows), the arm64 target, and that it produces a flashable `.img` with no secrets baked in. Done: file present, `shellcheck`-irrelevant, links to `docs/deployment/pi-appliance-setup.md`.

---

## Phase 2: Foundational (blocks US1 image pre-load)

- [x] T003 [Class B] Add `.github/workflows/publish-images.yml`: a `docker/build-push-action` + `docker/setup-buildx-action` workflow that builds `docker/Dockerfile.api` and `Dockerfile.web` for `linux/amd64,linux/arm64` and pushes to `ghcr.io/${{ github.repository_owner }}/vandaemon-{api,web}` with tags `latest`, `${{ github.sha }}`, and semver-on-tag; uses `GITHUB_TOKEN` with `packages: write`. Done: `actionlint .github/workflows/publish-images.yml` passes; `yamllint` clean.
- [~] T004 [Class B] Confirm the existing Dockerfiles build for arm64 unchanged. IN PROGRESS: arm64 cross-build running under QEMU (binfmt registered via `tonistiigi/binfmt` after an initial `exec format error`). Reaches the .NET build steps; awaiting completion. If a native dep breaks, STOP and report (do not patch silently).

---

## Phase 3: User Story 2 — One authoritative stack, dev unbroken (P1)

**Goal**: single root `docker-compose.yml` (api+web+mqtt); duplicate deleted; api→broker via env; dev path intact.
**Independent test**: `docker compose config -q` validates one stack with one broker; `appsettings.json` still `localhost`.

- [x] T005 [Class B] [US2] Rewrite root `docker-compose.yml` to the consolidated stack per `contracts/stack-contract.md`: add the `mqtt` (`eclipse-mosquitto:2.0`) service (ports 1883/9001, `mqtt-data`/`mqtt-logs` volumes, config bind-mount `./docker/mosquitto/config`, `restart: unless-stopped`, healthcheck); keep `api`/`web`; add api env `MqttLedDimmer__MqttBroker=mqtt` + `__MqttPort=1883`; add api `image:`+`build:` and web `image:`+`build:` referencing `ghcr.io/<owner>/vandaemon-{api,web}:${VANDAEMON_TAG:-latest}`; drop obsolete `version:`; web `depends_on api: service_healthy`. Done: `docker compose config -q` passes; exactly one mosquitto service.
- [x] T006 [Class B] [US2] Delete `docker/docker-compose.yml`. Done: file absent (`test ! -f docker/docker-compose.yml`); no second stack remains.
- [x] T007 [Class B] [US2] Verify the app default is untouched: `appsettings.json` keeps `"MqttBroker": "localhost"`. Done: `grep '"MqttBroker": "localhost"' src/Backend/VanDaemon.Api/appsettings.json` matches (no edit to that file).
- [x] T008 [P] [Class B] [US2] Repoint compose references in docs to the root file (remove `docker/docker-compose.yml` usages): `DEPLOYMENT.md`, `DOCKER.md`, `hw/LEDDimmer/README.md`, `docker/mosquitto/README.md`. Done: `grep -rn "docker/docker-compose.yml" DEPLOYMENT.md DOCKER.md hw/LEDDimmer/README.md docker/mosquitto/README.md` returns nothing; commands shown use `docker compose ...` from repo root.
- [x] T009 [P] [Class B] [US2] Update `PROJECT_PLAN.md` project-structure tree to show the single root compose (drop the `docker/docker-compose.yml` entry). Done: tree reflects one authoritative compose.

**Checkpoint US2**: `docker compose config -q` green; dev `docker compose up` and bare-metal `dotnet run` both documented working.

---

## Phase 4: User Story 1 — Flashable image boots a working controller (P1) — MVP

**Goal**: pi-gen stage + `build.sh` produce a flashable arm64 `.img` that, on boot, runs the stack with pre-loaded images and applies first-boot config — no interactive setup.
**Independent test (Class B)**: scripts pass `shellcheck`/`bash -n`; `build.sh` stages compose+config+image tarballs and exercises `docker load`. **Boot/serve is Class C (T030).**

- [x] T010 [Class B] [US1] `deploy/pi/config/pi-gen-config.example`: pi-gen top-level config (arm64 Lite base, `IMG_NAME`, locale, stages skipped past stage2, `ENABLE_SSH` handled via firstboot). No secrets. Done: documented keys present; file is `.example` only.
- [x] T011 [Class B] [US1] `deploy/pi/stage-vandaemon/prerun.sh` + stage skeleton per pi-gen conventions (EXPORT/`SKIP` files as needed). Done: `shellcheck`/`bash -n` clean; layout matches pi-gen stage rules.
- [x] T012 [Class B] [US1] `deploy/pi/stage-vandaemon/00-install-docker/{00-packages,00-run-chroot.sh}`: install Docker Engine + compose plugin and enable docker on boot inside the chroot. Done: `shellcheck` clean; package list valid for Bookworm arm64.
- [x] T013 [Class B] [US1] `deploy/pi/stage-vandaemon/01-vandaemon-stack/`: stage `docker-compose.yml` + `docker/mosquitto/config/*` into `files/`, and `00-run.sh` copies them to `/opt/vandaemon` and `docker load`s image tarballs from `/opt/vandaemon/images/`. Done: `shellcheck` clean; `00-run.sh` references the staged compose + a `docker load` loop.
- [x] T014 [Class B] [US1] `deploy/pi/stage-vandaemon/02-firstboot/files/vandaemon.service` (oneshot, `RemainAfterExit=yes`, `After=docker.service`, `ExecStart=docker compose -f /opt/vandaemon/docker-compose.yml up -d`) and its `00-run.sh` install/enable. Done: `systemd-analyze verify` (where available) / structural review; `shellcheck` clean.
- [x] T015 [Class B] [US1] `deploy/pi/stage-vandaemon/02-firstboot/files/firstboot.sh` + `vandaemon-firstboot.service` (oneshot, self-disabling): parse `/boot/firmware/vandaemon-config.txt` per `contracts/boot-config.md` (hostname, WiFi, SSH pubkey, MQTT auth), write secrets `0600`, then start the stack; safe defaults when file/fields absent; never block on input. Done: `shellcheck` clean; covers all boot-config.md fields + the missing-file fallback.
- [x] T016 [Class B] [US1] `deploy/pi/boot/vandaemon-config.txt.example` (the single FAT-partition config file) with every documented key and NO real secret values; staged onto the boot partition by the stage. Done: matches `contracts/boot-config.md`; `.example` only.
- [x] T017 [Class B] [US1] In the stage, write NVMe `config.txt` params (`dtparam=pciex1`/`dtparam=nvme` per the HAT) and add a comment block referencing the one-time EEPROM `BOOT_ORDER` human step (NOT applied here). Done: `config.txt` snippet present; no EEPROM mutation in any script.
- [x] T018 [Class B] [US1] `deploy/pi/build.sh`: wrap pi-gen's Docker build path; pull `ghcr.io/<owner>/vandaemon-{api,web}` + `eclipse-mosquitto:2.0` for `linux/arm64` and `docker save` them into the stage `images/` slot; invoke the build; emit the `.img` path. Idempotent/re-runnable; nothing irreversible to the host. Done: `shellcheck` clean; `bash -n` ok; dry-run logic review confirms tarball staging + pi-gen invocation.

**Checkpoint US1 (MVP)**: all scripts pass static checks; `build.sh` logic produces a flashable arm64 `.img` with pre-loaded images. Booting it is deferred to T030 (Class C).

---

## Phase 5: User Story 3 — Controller owns the MQTT broker (P2)

**Goal**: Mosquitto persistence + off-by-default Cerbo bridge + single-flag auth, per `contracts/mqtt-broker-contract.md`.
**Independent test (Class B)**: `mosquitto -c mosquitto.conf` config-parse passes; bridge + auth directives present and commented; no committed secrets.

- [x] T019 [Class B] [US3] Edit `docker/mosquitto/config/mosquitto.conf`: keep `persistence true`; add `autosave_interval` and `persistent_client_expiration`; keep listeners 1883/9001. Done: `mosquitto -c` config-parse passes in an `eclipse-mosquitto:2.0` container.
- [x] T020 [Class B] [US3] Add the **commented** `connection cerbo-bridge` stanza (address/topic/remote creds placeholders) per the contract — inert by default. Done: stanza present and fully commented; config-parse still passes (no live bridge).
- [x] T021 [Class B] [US3] Add the **commented** single-flag auth block (set `allow_anonymous false`, uncomment `password_file`/`acl_file`) with step-by-step instructions referencing `passwd.example`/`acl.conf.example`. Done: directives present+commented; `test ! -f docker/mosquitto/config/passwd && test ! -f docker/mosquitto/config/acl.conf` (only `.example` committed).
- [x] T022 [P] [Class B] [US3] Update `docker/mosquitto/README.md` to describe the owned-broker posture, the bridge stanza, and the one-flag auth path. Done: README matches the conf; no `docker/docker-compose.yml` refs (coordinates with T008).

**Checkpoint US3**: broker config-parse green; fabric persists across app restart (design-verified; live persistence across power-cycle is Class C in T030).

---

## Phase 6: User Story 4 — SD-installer fallback (P3)

**Goal**: secondary, clearly-marked SD path that images the rootfs onto NVMe then reboots to NVMe.
**Independent test (Class B)**: `shellcheck` on the installer; procedure documented end-to-end. **Copy/reboot is Class C.**

- [x] T023 [Class B] [US4] `deploy/pi/sd-installer/install-to-nvme.sh`: boot-from-SD one-shot that `dd`/`rpi-clone`s the prebuilt appliance rootfs onto the NVMe and triggers a reboot-to-NVMe; guarded target detection; loud abort if NVMe absent. Done: `shellcheck` clean; `bash -n` ok; no host-side destructive default.
- [x] T024 [Class B] [US4] `deploy/pi/sd-installer/README.md`: clearly marks this the **fallback** (not primary), with the end-to-end procedure and the "remove SD after" step. Done: file present; explicitly labels primary = bench NVMe flash.

---

## Phase 7: Docs, Cross-links & Class-C Checklist

- [x] T025 [Class B] Author `docs/deployment/pi-appliance-setup.md`: flash steps (rpi-imager CLI/Imager), config-file fields (link `contracts/boot-config.md`), first-boot expectations, verification (health endpoint, `docker ps`, broker on 1883), update procedure (re-flash vs `docker compose pull && up -d`), and backup of `api-data`/`mqtt-data` volumes. Done: all FR-017 sections present; commands target arm64/root compose.
- [x] T026 [P] [Class B] Tick/annotate ADR-001 action items 1–3 in `docs/deployment/adr/ADR-001-controller-soc.md` and cross-link `pi-appliance-setup.md`. Done: items 1–3 annotated as addressed-by-006; link present.
- [x] T027 [P] [Class B] Cross-link the new guide and record the Pi 5/NVMe deployment target decision in `PROJECT_PLAN.md` (deployment-targets + Related Documentation). Done: link present; target recorded (ADR-001 item 1).
- [x] T028 [P] [Class B] Update `DEPLOYMENT.md` to add the Pi-appliance path and point Raspberry Pi users at the consolidated root compose + the new guide. Done: section present; no stale `docker/docker-compose.yml` refs.
- [x] T029 [P] [Class B] Note the build-time-only nature of GHCR (runtime stays offline, Constitution III) in `deploy/pi/README.md` and the setup guide. Done: explicit "GHCR = build-time only, appliance runs offline" statement in both.
- [x] T030 [Authored (Class B) ✓ · Execution Class C PENDING] Author `docs/deployment/pi-appliance-verification-checklist.md`: the on-hardware checklist a human runs — EEPROM `BOOT_ORDER` set; boots from NVMe with SD/USB removed; `docker ps` all healthy; `/health` healthy; web UI served; broker reachable on 1883 from another LAN host; retained MQTT + config survive a power cycle; (optional) SD-installer copy + reboot-to-NVMe. Each item has an explicit "verified by ___ on ___" field and starts UNCHECKED. Done: checklist file exists, every item unchecked, no item asserted as passing.

---

## Dependencies & Execution Order

- **Setup (T001–T002)** → first.
- **Foundational (T003–T004)** → before US1 (image pre-load needs published arm64 images / arm64 build proof).
- **US2 (T005–T009)** → P1; the consolidated compose is baked by US1, so US2 before US1 finalisation.
- **US1 (T010–T018)** → P1; **MVP = Setup + Foundational + US2 + US1**.
- **US3 (T019–T022)** → P2; independently testable; ideally lands before the final image build so the enhanced conf is baked, but does not block US1's static gate.
- **US4 (T023–T024)** → P3; independent.
- **Docs (T025–T030)** → last; T030 is the governance deliverable (Class-C checklist).

### Parallel opportunities

- `[P]` doc tasks T008, T009 (different files) can run together after T005–T007.
- `[P]` T026, T027, T028, T029 (different files) can run together after T025.
- US3 (T019–T022) and US4 (T023–T024) are independent of each other and of US1's script work.

## Implementation Strategy

1. **MVP**: Setup → Foundational → US2 → US1, then run the full Class-B static gate. Deliver the
   buildable image + scripts.
2. **Increment**: US3 (broker hardening) → re-bake conf into the image; US4 (SD fallback).
3. **Govern**: Docs + the Class-C verification checklist (T030). STOP — hand the checklist to Stuart.
   **No self-merge; no on-device success claim until the checklist is human-completed.**
