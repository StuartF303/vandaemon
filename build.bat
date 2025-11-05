@echo off
setlocal enabledelayedexpansion

echo ===================================
echo VanDaemon Build Script
echo ===================================
echo.

REM Check if .NET SDK is installed
where dotnet >nul 2>nul
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] .NET SDK not found. Please install .NET 8.0 SDK
    exit /b 1
)

echo [INFO] Found .NET SDK
dotnet --version
echo.

REM Clean previous builds
echo [INFO] Cleaning previous builds...
dotnet clean VanDaemon.sln --configuration Release

REM Restore dependencies
echo [INFO] Restoring dependencies...
dotnet restore VanDaemon.sln

REM Build solution
echo [INFO] Building solution...
dotnet build VanDaemon.sln --configuration Release --no-restore
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Build failed
    exit /b 1
)

REM Run tests
echo [INFO] Running tests...
dotnet test VanDaemon.sln --configuration Release --no-build --verbosity normal
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Tests failed
    exit /b 1
)

REM Build Docker images if Docker is available
where docker >nul 2>nul
if %ERRORLEVEL% EQU 0 (
    echo [INFO] Building Docker images...

    echo [INFO] Building API image...
    docker build -f docker/Dockerfile.api -t vandaemon-api:latest .

    echo [INFO] Building Web image...
    docker build -f docker/Dockerfile.web -t vandaemon-web:latest .

    echo [INFO] Docker images built successfully!
) else (
    echo [WARN] Docker not found. Skipping Docker image build.
)

echo.
echo [INFO] Build completed successfully!
echo.
echo To run the application:
echo   1. Using Docker: cd docker ^&^& docker-compose up
echo   2. Using dotnet: cd src\Backend\VanDaemon.Api ^&^& dotnet run
echo.

pause
