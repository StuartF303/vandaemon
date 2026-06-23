# CI/CD Reference

## Contents
- GitHub Actions Workflows
- Build Pipeline
- Deployment Triggers
- Common Anti-Patterns
- Workflow Checklist

## GitHub Actions Workflows

### Build and Test Workflow

```yaml
# .github/workflows/build.yml
name: Build and Test

on:
  push:
    branches: [main, develop]
  pull_request:
    branches: [main]

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      
      - name: Restore dependencies
        run: dotnet restore VanDaemon.sln
      
      - name: Build
        run: dotnet build VanDaemon.sln --no-restore --configuration Release
      
      - name: Test
        run: dotnet test VanDaemon.sln --no-build --configuration Release --verbosity normal
```

### Fly.io Deployment Workflow

```yaml
# .github/workflows/fly-deploy.yml
name: Deploy to Fly.io

on:
  push:
    branches: [main]

jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      
      - uses: superfly/flyctl-actions/setup-flyctl@master
      
      - name: Deploy
        run: flyctl deploy --remote-only
        env:
          FLY_API_TOKEN: ${{ secrets.FLY_API_TOKEN }}
```

## Build Pipeline

### Multi-Stage Docker Build in CI

```yaml
- name: Build Docker image
  run: |
    docker build -f docker/Dockerfile.combined \
      --tag vandaemon:${{ github.sha }} \
      --tag vandaemon:latest \
      .

- name: Test container health
  run: |
    docker run -d --name test-container -p 8080:8080 vandaemon:latest
    sleep 10
    curl -f http://localhost:8080/health || exit 1
    docker stop test-container
```

## Deployment Triggers

### Branch-Based Deployment

| Branch | Action | Target |
|--------|--------|--------|
| `main` | Auto-deploy | Fly.io production |
| `develop` | Build + test only | None |
| PR to `main` | Build + test | None |

### Manual Deployment

```yaml
on:
  workflow_dispatch:
    inputs:
      environment:
        description: 'Deployment target'
        required: true
        default: 'staging'
        type: choice
        options:
          - staging
          - production
```

## Common Anti-Patterns

### WARNING: No Build Cache

**The Problem:**

```yaml
# BAD - rebuilds everything every time
- name: Build
  run: dotnet build
```

**Why This Breaks:**
1. CI builds take 5-10 minutes instead of 1-2 minutes
2. Wastes GitHub Actions minutes
3. Slow feedback loop for developers

**The Fix:**

```yaml
# GOOD - cache NuGet packages
- name: Cache NuGet packages
  uses: actions/cache@v4
  with:
    path: ~/.nuget/packages
    key: ${{ runner.os }}-nuget-${{ hashFiles('**/*.csproj') }}
    restore-keys: |
      ${{ runner.os }}-nuget-

- name: Build
  run: dotnet build --no-restore
```

### WARNING: Secrets in Logs

**The Problem:**

```yaml
# BAD - token visible in logs
- run: echo "Deploying with token ${{ secrets.FLY_API_TOKEN }}"
```

**Why This Breaks:**
1. Secrets appear in build logs
2. Anyone with repo access can see them
3. Security incident waiting to happen

**The Fix:**

```yaml
# GOOD - pass as environment variable
- name: Deploy
  run: flyctl deploy --remote-only
  env:
    FLY_API_TOKEN: ${{ secrets.FLY_API_TOKEN }}
```

### WARNING: Missing Test Gate

**The Problem:**

```yaml
# BAD - deploy without running tests
jobs:
  deploy:
    steps:
      - uses: actions/checkout@v4
      - run: flyctl deploy
```

**Why This Breaks:**
1. Broken code reaches production
2. No safety net for regressions
3. Manual rollbacks required

**The Fix:**

```yaml
# GOOD - tests must pass before deploy
jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - run: dotnet test

  deploy:
    needs: test  # Depends on test job passing
    runs-on: ubuntu-latest
    steps:
      - run: flyctl deploy
```

## Workflow Checklist

Copy this checklist when setting up CI/CD:

- [ ] Create `.github/workflows/build.yml` for PR checks
- [ ] Add NuGet package caching
- [ ] Configure `dotnet test` with coverage
- [ ] Create `.github/workflows/fly-deploy.yml` for production
- [ ] Add `FLY_API_TOKEN` to GitHub Secrets
- [ ] Verify `needs: test` dependency in deploy job
- [ ] Test workflow on a feature branch first

---

## Multi-Arch Image Publishing & arm64 Appliance Build (feature 006 lessons)

The Pi-5 appliance runs `linux/arm64`. The api/web images are built **multi-arch** and published to
**GHCR**, then baked into the pi-gen image. These are the hard-won lessons from wiring that up.

### Publishing multi-arch images to GHCR

`.github/workflows/publish-images.yml` (matrix over api/web):

```yaml
permissions:
  contents: read
  packages: write          # required to push to GHCR with GITHUB_TOKEN
steps:
  - uses: docker/setup-qemu-action@v3       # arm64 emulation on the amd64 runner
  - uses: docker/setup-buildx-action@v3
  - uses: docker/login-action@v3
    with: { registry: ghcr.io, username: ${{ github.actor }}, password: ${{ secrets.GITHUB_TOKEN }} }
  - uses: docker/metadata-action@v5         # tags: latest (default branch), sha, semver
    with: { images: ghcr.io/${{ github.repository_owner }}/vandaemon-<name> }
  - uses: docker/build-push-action@v6
    with:
      platforms: linux/amd64,linux/arm64
      push: true
      cache-from: type=gha,scope=<name>
      cache-to:   type=gha,mode=max,scope=<name>
```

- **`GITHUB_TOKEN` + `packages: write`** is enough — no PAT needed for same-repo GHCR pushes.
- **Scope the GHA cache per image** (`scope=api` / `scope=web`) or they clobber each other.
- The .NET base images `mcr.microsoft.com/dotnet/{sdk,aspnet}:10.0` are multi-arch — **no Dockerfile
  change** is needed to target arm64; buildx selects the arch per `--platform`.

### Local arm64 cross-build (Docker Desktop / WSL2)

**GOTCHA — `exec /bin/sh: exec format error` during `RUN`.** The arm64 base layers pull fine, but any
`RUN` step fails because **QEMU binfmt handlers aren't registered** in the Docker VM. They are **not
persistent — they are lost on every reboot / Docker restart.** Fix before each session:

```bash
docker run --privileged --rm tonistiigi/binfmt --install arm64
# verify: emulators should list qemu-aarch64
```

`docker/setup-qemu-action` does this automatically in CI; locally you must do it yourself.

**Build time:** a full .NET `restore`+`build`+`publish` for arm64 **under QEMU emulation is slow
(~15–20 min per image)** vs ~1–2 min native. Run it in the background; don't assume it hung. It is only
a pre-CI sanity check (the real arm64 build happens natively-ish on CI runners) — you can skip it and
let CI cover it.

### Windows / Git-Bash: docker volume path mangling

**GOTCHA** — MSYS rewrites container-side paths, e.g. `-v "$PWD:/repo"` → working dir becomes
`C:/Program Files/Git/repo` and the run fails. Prefix docker commands with `MSYS_NO_PATHCONV=1`:

```bash
MSYS_NO_PATHCONV=1 docker run --rm -v "$PWD:/repo" --workdir /repo <image> ...
```

### Linting without local installs (containerized)

When shellcheck/yamllint/actionlint aren't on PATH, run them via their official images — useful as the
static gate for infra (shell/compose/workflow) work that has no `dotnet test`:

```bash
docker run --rm -v "$PWD:/mnt" -w /mnt koalaman/shellcheck:stable deploy/pi/**/*.sh
docker run --rm -v "$PWD:/data" -w /data cytopia/yamllint:latest -d relaxed docker-compose.yml
docker run --rm -v "$PWD:/repo" -w /repo rhysd/actionlint:latest .github/workflows/*.yml
```
(`actionlint` also flags **too-old action versions** — bump `@v3` → `@v4` etc.)

### Compose project name → volume/network names

Compose derives the project name from the **directory containing the compose file**. Moving the file
from `docker/` to the repo root renamed everything `docker_*` → `vandaemon_*`. Any docs/scripts doing
`docker run -v docker_mqtt-data:...` or `--network docker_vandaemon` must be updated to `vandaemon_*`.

### Mosquitto config: `max_packet_size` has no zero form

**GOTCHA that crash-loops the broker.** Mosquitto 2.0 deprecated `message_size_limit` in favour of
`max_packet_size`, but the semantics differ: `message_size_limit 0` meant *unlimited*, whereas
**`max_packet_size 0` is invalid** and makes the broker exit non-zero (with `restart: unless-stopped`
it then crash-loops). For "unlimited", **omit the directive entirely**.

### Config-parse is not enough — smoke-test the service

`mosquitto -c conf` only validates *syntax*; it passed even with the bad `max_packet_size 0`. The crash
only surfaced when the container actually **ran**. For broker/stack changes, do a live smoke test:

```bash
docker compose up -d mqtt
# wait for: docker inspect -f '{{.State.Health.Status}}' vandaemon-mqtt  == healthy
docker exec vandaemon-mqtt sh -c "mosquitto_sub -t t -C 1 -W 5 & sleep 1; mosquitto_pub -t t -m ok; wait"
docker compose down -v
```

### Pre-baking images for offline first boot

To make the appliance boot with **no internet**, pull the arm64 images on the builder and
`docker save` them into the image; load them at first boot (`docker load`) — do **not** try to run a
Docker daemon inside the pi-gen chroot. `docker save`/`load` is daemon-agnostic and reproducible.