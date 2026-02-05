# .NET Workflows Reference

## Contents
- Development Workflow
- Build and Test Workflow
- Adding New Projects
- NuGet Package Management
- Docker Deployment
- Troubleshooting

## Development Workflow

### Daily Development Cycle

```bash
# Pull latest and build
git pull
dotnet build VanDaemon.sln

# Run API and Web in separate terminals
cd src/Backend/VanDaemon.Api && dotnet run
cd src/Frontend/VanDaemon.Web && dotnet run

# Run tests before committing
dotnet test VanDaemon.sln
```

### Hot Reload Development

```bash
# API with hot reload
cd src/Backend/VanDaemon.Api
dotnet watch run

# Blazor with hot reload
cd src/Frontend/VanDaemon.Web
dotnet watch run
```

## Build and Test Workflow

### Complete Build Pipeline

Copy this checklist and track progress:
- [ ] Clean previous build artifacts
- [ ] Restore NuGet packages
- [ ] Build in Release mode
- [ ] Run all tests
- [ ] Verify Docker build

```bash
# Clean
dotnet clean VanDaemon.sln

# Restore
dotnet restore VanDaemon.sln

# Build Release
dotnet build VanDaemon.sln --configuration Release

# Test
dotnet test VanDaemon.sln --configuration Release

# Docker verification
docker compose build
```

### Test with Coverage

```bash
dotnet test VanDaemon.sln --collect:"XPlat Code Coverage"

# Results in: tests/*/TestResults/*/coverage.cobertura.xml
```

### Iterate Until Tests Pass

1. Run tests: `dotnet test VanDaemon.sln`
2. If tests fail, fix the issues
3. Repeat step 1 until all tests pass
4. Only commit when tests pass

## Adding New Projects

### Add Class Library

```bash
# Create project
dotnet new classlib -n VanDaemon.Plugins.NewPlugin \
  -o src/Backend/VanDaemon.Plugins/NewPlugin

# Add to solution
dotnet sln VanDaemon.sln add \
  src/Backend/VanDaemon.Plugins/NewPlugin/VanDaemon.Plugins.NewPlugin.csproj

# Add reference to Abstractions
dotnet add src/Backend/VanDaemon.Plugins/NewPlugin/VanDaemon.Plugins.NewPlugin.csproj \
  reference src/Backend/VanDaemon.Plugins/Abstractions/VanDaemon.Plugins.Abstractions.csproj

# Add reference from API
dotnet add src/Backend/VanDaemon.Api/VanDaemon.Api.csproj \
  reference src/Backend/VanDaemon.Plugins/NewPlugin/VanDaemon.Plugins.NewPlugin.csproj
```

### Add Test Project

```bash
# Create xUnit test project
dotnet new xunit -n VanDaemon.NewPlugin.Tests \
  -o tests/VanDaemon.NewPlugin.Tests

# Add to solution
dotnet sln VanDaemon.sln add tests/VanDaemon.NewPlugin.Tests/VanDaemon.NewPlugin.Tests.csproj

# Add reference to project under test
dotnet add tests/VanDaemon.NewPlugin.Tests/VanDaemon.NewPlugin.Tests.csproj \
  reference src/Backend/VanDaemon.Plugins/NewPlugin/VanDaemon.Plugins.NewPlugin.csproj

# Add testing packages
dotnet add tests/VanDaemon.NewPlugin.Tests package FluentAssertions
dotnet add tests/VanDaemon.NewPlugin.Tests package Moq
```

See the **xunit** skill for test patterns.

## NuGet Package Management

### Add Package

```bash
# Add to specific project
dotnet add src/Backend/VanDaemon.Api/VanDaemon.Api.csproj package Serilog.AspNetCore

# Add specific version
dotnet add package MQTTnet --version 4.3.3
```

### Update Packages

```bash
# List outdated packages
dotnet list package --outdated

# Update specific package
dotnet add package Serilog.AspNetCore

# Restore after updates
dotnet restore
```

### Remove Package

```bash
dotnet remove src/Backend/VanDaemon.Api/VanDaemon.Api.csproj package OldPackage
```

## Docker Deployment

### Local Docker Compose

```bash
# Build and start
docker compose up -d --build

# View logs
docker compose logs -f

# Stop
docker compose down
```

### Fly.io Deployment

```bash
# Login
flyctl auth login

# Deploy
flyctl deploy

# View logs
flyctl logs --app vandaemon
```

See the **docker** skill for container configuration details.

## Troubleshooting

### Build Failures

```bash
# Clear NuGet cache
dotnet nuget locals all --clear

# Force restore
dotnet restore --force

# Rebuild
dotnet build --no-incremental
```

### Port Conflicts

```bash
# Windows - find process on port
netstat -ano | findstr :5000

# Kill process
taskkill /PID <pid> /F
```

### DLL Version Conflicts

```xml
<!-- Force specific version in csproj -->
<PackageReference Include="Microsoft.Extensions.Logging" Version="10.0.0">
  <PrivateAssets>all</PrivateAssets>
</PackageReference>
```

### WARNING: Stale Build Artifacts

**The Problem:**
After switching branches or updating packages, old DLLs cause runtime errors.

**The Fix:**

```bash
# Nuclear clean
dotnet clean VanDaemon.sln
rm -rf src/**/bin src/**/obj tests/**/bin tests/**/obj
dotnet restore
dotnet build