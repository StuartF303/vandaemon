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