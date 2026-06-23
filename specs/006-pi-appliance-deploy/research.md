# Phase 0 Research: Headless Pi-5 Appliance Deployment

All major forks were pre-decided in the brief (pi-gen, GHCR prebuilt images, root-compose
consolidation) and confirmed with the requester. This file records the supporting technical decisions
and the few facts that must be **verified on hardware** (flagged, Class C).

## R1. .NET 10 base images on arm64

- **Decision**: Reuse the existing `docker/Dockerfile.api` and `Dockerfile.web` unchanged; build them
  for `linux/arm64` (and `linux/amd64` for dev) via `docker buildx` in CI and publish to GHCR.
- **Rationale**: `mcr.microsoft.com/dotnet/sdk:10.0` and `aspnet:10.0` are multi-arch manifests that
  include `linux/arm64`; buildx selects the right arch per `--platform`. No Dockerfile change needed.
- **Verify (Class B)**: `docker buildx build --platform linux/arm64 -f docker/Dockerfile.api .`
  completes on the builder. If a native dependency ever breaks on arm64 (ADR-001 item 5), it surfaces
  here, not on the Pi.
- **Alternatives rejected**: building on the Pi (slow, not reproducible in CI); `Dockerfile.combined`
  (Fly.io single-container model — different deployment shape, keep separate).

## R2. Image delivery to the appliance — pre-load vs first-boot pull

- **Decision**: **Pre-load** the arm64 api/web images into the rootfs as `docker save` tarballs under
  `/opt/vandaemon/images/`, and have `firstboot.sh` run `docker load` then `docker compose up -d`.
  Keep `docker compose pull` as an **online fallback** if a tarball is missing.
- **Rationale**: Running a Docker daemon *inside* the pi-gen chroot to pre-pull into `/var/lib/docker`
  is fragile (nested daemon, storage-driver mismatch). `docker save`/`load` is daemon-agnostic at build
  time, keeps the appliance's first boot **offline-capable** (Constitution III), and is fully
  reproducible. `eclipse-mosquitto:2.0` is also saved as a tarball for the same reason.
- **Verify (Class B)**: `build.sh` produces the tarballs (pulled from GHCR / Docker Hub on the builder)
  and stages them; `docker load < tarball` is exercised in the build.
- **Alternatives rejected**: nested dockerd in chroot (fragile/non-reproducible); pure first-boot pull
  (breaks the "boots straight into running stack with no internet" promise).

## R3. pi-gen integration (custom stage, Docker build path)

- **Decision**: Add a custom `stage-vandaemon` and drive pi-gen via its **Docker build path**
  (`pi-gen/build-docker.sh`) wrapped by `deploy/pi/build.sh`. Base = arm64 Lite (stage0–stage2,
  `IMG_NAME` set, desktop stages skipped). The stage installs Docker, stages the compose+config, loads
  the image tarballs, installs systemd units, and writes NVMe `config.txt` params.
- **Rationale**: The Docker build path makes the build reproducible on any Linux/WSL2/Docker host
  without polluting the host (Constitution: reversible, nothing irreversible to the user's machine).
  A custom stage is the supported extension point (vs hand-rolled image surgery).
- **Flag (constraint)**: pi-gen does **not** run natively on Windows. `deploy/pi/README.md` documents
  WSL2/Linux/Docker. (x86-only assumption check: the *builder* may be x86 — buildx/qemu handles arm64;
  the *artifact* is arm64. No x86 assumption leaks into the appliance.)
- **Verify (Class B)**: `bash -n` + `shellcheck` on all stage scripts; the stage file/skip layout
  matches pi-gen conventions. **Full image build + boot is Class C** (needs a Linux builder + the Pi).
- **Alternatives rejected**: `rpi-image-gen` (newer, less battle-tested for this); CustomPiOS (extra
  framework); manual `chroot` surgery (not reproducible).

## R4. NVMe boot on Pi 5 (firmware vs rootfs)

- **Decision**: Bake the **rootfs-side** NVMe enablement into `config.txt`
  (`dtparam=pciex1` / `dtparam=nvme` as appropriate for the HAT) inside the stage. Document the
  **firmware-side** one-time step separately: update the Pi 5 bootloader EEPROM and set `BOOT_ORDER`
  to try NVMe (e.g. `0xf416`) via `raspi-config` / `rpi-eeprom-config`.
- **Rationale**: The EEPROM is **separate firmware**, not part of the flashed root filesystem, so it
  **cannot** be baked into the `.img`. It is therefore a human step in the Class-C checklist (reversible
  via `rpi-eeprom-config`, low brick risk — not a Class-D action).
- **Verify (Class C)**: Pi boots from NVMe with the SD/USB removed. **Cannot be auto-verified.**
- **Alternatives rejected**: assuming factory boot order (varies by board/EEPROM date — must be
  explicit); shipping on microSD (ADR-001 anti-pattern, SD-rot).

## R5. First-boot configuration (no interactive setup)

- **Decision**: A single plain-text file `vandaemon-config.txt` on the **FAT boot partition**
  (editable from the bench after flashing), consumed by a one-shot `vandaemon-firstboot.service`
  running `firstboot.sh`: set hostname, write network config (NetworkManager/`wpa`), install the SSH
  authorized key, materialise broker creds (or generate + store with `0600`), then `docker compose up`.
  The unit disables itself after success.
- **Rationale**: Boot-partition config is the established headless Pi pattern (rpi-imager writes a
  similar `firstrun.sh`/`custom.toml`); putting it on FAT means no Linux tooling needed to edit on the
  bench. Generated secrets are written `0600` and never committed; the repo ships only
  `vandaemon-config.txt.example`.
- **Edge handling**: missing/malformed file → safe documented defaults (hostname `vandaemon`, DHCP,
  SSH key absent → SSH password login stays disabled; broker stays anonymous-on-trusted-LAN). Never
  hang waiting for input.
- **Verify (Class B)**: `shellcheck firstboot.sh`; unit files parse (`systemd-analyze verify` where
  available). **Applied-on-boot behaviour is Class C.**
- **Alternatives rejected**: cloud-init (heavier, not default on Pi OS Lite); interactive
  `raspi-config` (violates no-touch); baking secrets at build time (violates no-committed-secrets).

## R6. Compose consolidation & api→broker wiring

- **Decision**: Root `docker-compose.yml` becomes the single stack (api+web+mqtt), healthchecks on api
  (`/health`) and mqtt (`mosquitto_sub` self-probe), `restart: unless-stopped` on all three, web
  `depends_on` api healthy. api gets `MqttLedDimmer__MqttBroker=mqtt` + `__MqttPort=1883` via env.
  Delete `docker/docker-compose.yml`; repoint docs. `version:` key dropped (obsolete in Compose v2).
- **Rationale**: One source of truth; `__`-delimited env overrides .NET config without touching
  `appsettings.json` (bare-metal dev keeps `localhost`). Mosquitto config mounts from
  `./docker/mosquitto/config` (root context).
- **Verify (Class B)**: `docker compose config` validates; exactly one mqtt service; `grep` confirms
  `appsettings.json` still says `localhost`.

## R7. Mosquitto as owned broker (ADR-001 item 3)

- **Decision**: Keep `persistence true` (already present); add `autosave_interval` and
  `persistent_client_expiration`; add an **uncommented section header but commented directives** for a
  `connection cerbo-bridge` stanza (address/topic templates) — off by default. Keep `allow_anonymous
  true` with a clearly-commented one-flag switch (set `false` + uncomment `password_file`/`acl_file`)
  referencing the existing `passwd.example`/`acl.conf.example`.
- **Rationale**: Realises broker ownership and the Victron draft-0003 hook without enabling anything
  risky by default on a trusted LAN; auth is one documented edit away.
- **Verify (Class B)**: `mosquitto -c mosquitto.conf` config test passes in a throwaway container; the
  bridge stanza stays inert (commented) so it doesn't try to dial a non-existent Cerbo.

## R8. SD-installer fallback (secondary)

- **Decision**: Document + script an SD path: a small SD boots a one-shot that `dd`/`rpi-clone`s the
  prebuilt appliance rootfs onto the NVMe, then reboots to NVMe; SD removed after. Clearly marked
  fallback in `deploy/pi/sd-installer/README.md`.
- **Rationale**: Covers the no-bench-machine case while keeping the bench-flash path primary.
- **Verify (Class B)**: `shellcheck` on `install-to-nvme.sh`; procedure documented. **Actual copy +
  reboot-to-NVMe is Class C.**

## Class-B static verification gate (the exit condition for buildable artifacts)

| Check | Command (run on a Linux/Docker host) |
|-------|--------------------------------------|
| Compose validity | `docker compose config -q` |
| Single broker / dev default intact | `grep -c 'image:.*mosquitto' docker-compose.yml` == 1; `grep localhost src/Backend/VanDaemon.Api/appsettings.json` |
| Mosquitto config | `docker run --rm -v "$PWD/docker/mosquitto/config:/c" eclipse-mosquitto:2.0 mosquitto -c /c/mosquitto.conf` (config-parse) |
| Shell scripts | `shellcheck deploy/pi/**/*.sh` |
| GitHub Actions | `actionlint .github/workflows/publish-images.yml` |
| YAML lint | `yamllint docker-compose.yml .github/workflows/publish-images.yml` |
| arm64 build | `docker buildx build --platform linux/arm64 -f docker/Dockerfile.api .` (and web) |

> Note: these run on a **Linux/Docker host**, not the Windows dev box. On Windows, run them under WSL2
> or note them as builder-side checks. The build host availability itself is part of the Class-C/setup
> reality, not something the loop fakes.

## Open items deferred to hardware (Class C — checklist, never auto-claimed)

- Pi boots from NVMe with no SD/USB attached (R4).
- All services healthy on the van LAN after first boot (R5/R6).
- Retained MQTT + config survive an ACC-style power cycle (R7).
- SD-installer copies to NVMe and the Pi then boots NVMe (R8).
