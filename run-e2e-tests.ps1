#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Runs VanDaemon E2E tests by starting the application and running Playwright tests
.DESCRIPTION
    This script starts the API and Web UI in the background, waits for them to be ready,
    runs the E2E tests, and then cleans up the background processes.
.PARAMETER Headless
    Run tests in headless mode (default: true)
.PARAMETER Browser
    Browser to use for tests (chromium, firefox, webkit). Default: chromium
.PARAMETER SlowMo
    Slow down operations by specified milliseconds for debugging. Default: 0
.EXAMPLE
    .\run-e2e-tests.ps1
.EXAMPLE
    .\run-e2e-tests.ps1 -Headless $false -SlowMo 500
.EXAMPLE
    .\run-e2e-tests.ps1 -Browser firefox
#>

param(
    [bool]$Headless = $true,
    [string]$Browser = "chromium",
    [int]$SlowMo = 0
)

$ErrorActionPreference = "Stop"

# Colors for output
function Write-ColorOutput($ForegroundColor, $Message) {
    $fc = $host.UI.RawUI.ForegroundColor
    $host.UI.RawUI.ForegroundColor = $ForegroundColor
    Write-Output $Message
    $host.UI.RawUI.ForegroundColor = $fc
}

function Write-Info($Message) {
    Write-ColorOutput Green "[INFO] $Message"
}

function Write-Warning($Message) {
    Write-ColorOutput Yellow "[WARN] $Message"
}

function Write-Error($Message) {
    Write-ColorOutput Red "[ERROR] $Message"
}

Write-Info "VanDaemon E2E Test Runner"
Write-Info "=========================="
Write-Output ""

# Check if dotnet is available
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Error ".NET SDK not found. Please install .NET 10.0 SDK"
    exit 1
}

# Build solution first
Write-Info "Building solution..."
dotnet build VanDaemon.sln --configuration Debug
if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed"
    exit 1
}

# Start API in background
Write-Info "Starting VanDaemon API on http://localhost:5000..."
$apiProcess = Start-Process -FilePath "dotnet" `
    -ArgumentList "run", "--project", "src\Backend\VanDaemon.Api\VanDaemon.Api.csproj", "--no-build" `
    -PassThru `
    -NoNewWindow `
    -RedirectStandardOutput "api-output.log" `
    -RedirectStandardError "api-error.log"

# Start Web UI in background
Write-Info "Starting VanDaemon Web UI on http://localhost:5001..."
$webProcess = Start-Process -FilePath "dotnet" `
    -ArgumentList "run", "--project", "src\Frontend\VanDaemon.Web\VanDaemon.Web.csproj", "--no-build" `
    -PassThru `
    -NoNewWindow `
    -RedirectStandardOutput "web-output.log" `
    -RedirectStandardError "web-error.log"

# Function to cleanup processes on exit
function Cleanup {
    Write-Info "Cleaning up processes..."
    if ($apiProcess -and -not $apiProcess.HasExited) {
        Write-Info "Stopping API process..."
        Stop-Process -Id $apiProcess.Id -Force -ErrorAction SilentlyContinue
    }
    if ($webProcess -and -not $webProcess.HasExited) {
        Write-Info "Stopping Web process..."
        Stop-Process -Id $webProcess.Id -Force -ErrorAction SilentlyContinue
    }

    # Also kill any dotnet processes running VanDaemon
    Get-Process -Name dotnet -ErrorAction SilentlyContinue | Where-Object {
        $_.Path -like "*VanDaemon*"
    } | ForEach-Object {
        Write-Info "Stopping orphaned process: $($_.Id)"
        Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
    }
}

# Register cleanup on script exit
Register-EngineEvent -SourceIdentifier PowerShell.Exiting -Action { Cleanup } | Out-Null
trap { Cleanup; break }

# Wait for services to be ready
Write-Info "Waiting for services to start..."
$maxWaitSeconds = 60
$waitedSeconds = 0
$apiReady = $false
$webReady = $false

while ($waitedSeconds -lt $maxWaitSeconds) {
    Start-Sleep -Seconds 2
    $waitedSeconds += 2

    # Check API
    if (-not $apiReady) {
        try {
            $response = Invoke-WebRequest -Uri "http://localhost:5000/health" -TimeoutSec 2 -ErrorAction SilentlyContinue
            if ($response.StatusCode -eq 200) {
                $apiReady = $true
                Write-Info "API is ready!"
            }
        } catch {
            # Still waiting
        }
    }

    # Check Web UI
    if (-not $webReady) {
        try {
            $response = Invoke-WebRequest -Uri "http://localhost:5001" -TimeoutSec 2 -ErrorAction SilentlyContinue
            if ($response.StatusCode -eq 200) {
                $webReady = $true
                Write-Info "Web UI is ready!"
            }
        } catch {
            # Still waiting
        }
    }

    if ($apiReady -and $webReady) {
        break
    }

    Write-Output "  Waiting... ($waitedSeconds/$maxWaitSeconds seconds)"
}

if (-not $apiReady) {
    Write-Error "API failed to start within $maxWaitSeconds seconds"
    Write-Output "API Output:"
    Get-Content "api-output.log" -ErrorAction SilentlyContinue
    Write-Output "API Errors:"
    Get-Content "api-error.log" -ErrorAction SilentlyContinue
    Cleanup
    exit 1
}

if (-not $webReady) {
    Write-Error "Web UI failed to start within $maxWaitSeconds seconds"
    Write-Output "Web Output:"
    Get-Content "web-output.log" -ErrorAction SilentlyContinue
    Write-Output "Web Errors:"
    Get-Content "web-error.log" -ErrorAction SilentlyContinue
    Cleanup
    exit 1
}

Write-Info "Both services are ready!"
Write-Info "Waiting additional 10 seconds for Blazor WASM to initialize..."
Start-Sleep -Seconds 10
Write-Output ""

# Set environment variables for test configuration
$env:PLAYWRIGHT_HEADLESS = if ($Headless) { "true" } else { "false" }
$env:PLAYWRIGHT_BROWSER = $Browser
$env:PLAYWRIGHT_SLOWMO = $SlowMo.ToString()

Write-Info "Running E2E tests..."
Write-Info "  Browser: $Browser"
Write-Info "  Headless: $Headless"
Write-Info "  SlowMo: ${SlowMo}ms"
Write-Output ""

# Run E2E tests
dotnet test tests\VanDaemon.E2E.Tests\VanDaemon.E2E.Tests.csproj --no-build --verbosity normal

$testExitCode = $LASTEXITCODE

# Cleanup
Cleanup

Write-Output ""
if ($testExitCode -eq 0) {
    Write-Info "E2E tests completed successfully!"
} else {
    Write-Error "E2E tests failed with exit code: $testExitCode"
}

# Clean up log files
Remove-Item "api-output.log" -ErrorAction SilentlyContinue
Remove-Item "api-error.log" -ErrorAction SilentlyContinue
Remove-Item "web-output.log" -ErrorAction SilentlyContinue
Remove-Item "web-error.log" -ErrorAction SilentlyContinue

exit $testExitCode
