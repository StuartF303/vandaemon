#!/bin/bash

set -e

echo "==================================="
echo "VanDaemon Build Script"
echo "==================================="

# Colors for output
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

# Function to print colored output
print_status() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Check if .NET SDK is installed
if ! command -v dotnet &> /dev/null; then
    print_error ".NET SDK not found. Please install .NET 8.0 SDK"
    exit 1
fi

print_status "Found .NET SDK version: $(dotnet --version)"

# Clean previous builds
print_status "Cleaning previous builds..."
dotnet clean VanDaemon.sln --configuration Release

# Restore dependencies
print_status "Restoring dependencies..."
dotnet restore VanDaemon.sln

# Build solution
print_status "Building solution..."
dotnet build VanDaemon.sln --configuration Release --no-restore

# Run tests
print_status "Running tests..."
dotnet test VanDaemon.sln --configuration Release --no-build --verbosity normal

# Build Docker images if Docker is available
if command -v docker &> /dev/null; then
    print_status "Building Docker images..."

    print_status "Building API image..."
    docker build -f docker/Dockerfile.api -t vandaemon-api:latest .

    print_status "Building Web image..."
    docker build -f docker/Dockerfile.web -t vandaemon-web:latest .

    print_status "Docker images built successfully!"
else
    print_warning "Docker not found. Skipping Docker image build."
fi

print_status "Build completed successfully!"
echo ""
echo "To run the application:"
echo "  1. Using Docker: cd docker && docker-compose up"
echo "  2. Using dotnet: cd src/Backend/VanDaemon.Api && dotnet run"
echo ""
