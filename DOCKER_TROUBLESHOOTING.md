# Docker Desktop Troubleshooting for Windows

## Common Error: Docker Daemon Not Running

**Error Message:**
```
error during connect: Get "http://%2F%2F.%2Fpipe%2FdockerDesktopLinuxEngine/v1.51/...":
open //./pipe/dockerDesktopLinuxEngine: The system cannot find the file specified.
```

This means Docker Desktop is not running on your Windows machine.

## Solution Steps

### Step 1: Check if Docker Desktop is Installed

Press Windows key + R, type `appwiz.cpl` and press Enter. Look for "Docker Desktop" in the list.

**If Not Installed:**
1. Download from: https://www.docker.com/products/docker-desktop/
2. Run the installer
3. Restart your computer after installation
4. Continue to Step 2

### Step 2: Start Docker Desktop

**Method 1 - Via Start Menu:**
1. Press Windows key
2. Type "Docker Desktop"
3. Click on "Docker Desktop" application
4. Wait for Docker to start (30-60 seconds)
5. Look for the Docker whale icon in system tray (bottom-right)
6. Icon should be steady (not animated) when ready

**Method 2 - Via System Tray:**
1. Look for the Docker whale icon in the system tray
2. If present but crossed out, click it
3. Select "Start Docker Desktop"

**Method 3 - Via File Explorer:**
1. Navigate to: `C:\Program Files\Docker\Docker`
2. Double-click `Docker Desktop.exe`
3. Wait for startup to complete

### Step 3: Verify Docker is Running

Open a new terminal and run:
```bash
docker version
```

You should see output for both Client and Server. If you only see Client, Docker daemon is not running yet.

Wait a minute and try again:
```bash
docker ps
```

If this returns a list (even if empty), Docker is working!

### Step 4: Test with VanDaemon

From the VanDaemon solution root:
```bash
cd C:\projects\vandaemon
docker compose up -d
```

## Common Issues & Solutions

### Issue: Docker Desktop Won't Start

**Symptoms:**
- Docker Desktop opens but stays on "Starting..." forever
- Docker Desktop crashes immediately
- Error: "WSL 2 installation is incomplete"

**Solutions:**

1. **Enable WSL 2 (Required for Docker Desktop on Windows)**
   ```powershell
   # Open PowerShell as Administrator
   wsl --install

   # Or update WSL
   wsl --update

   # Set WSL 2 as default
   wsl --set-default-version 2
   ```

   Restart your computer after this.

2. **Enable Virtualization in BIOS**
   - Restart computer
   - Enter BIOS (usually F2, F10, DEL, or ESC during boot)
   - Find "Virtualization" or "Intel VT-x" or "AMD-V"
   - Enable it
   - Save and exit BIOS

3. **Enable Windows Features**
   - Open PowerShell as Administrator
   ```powershell
   Enable-WindowsOptionalFeature -Online -FeatureName Microsoft-Hyper-V -All
   Enable-WindowsOptionalFeature -Online -FeatureName VirtualMachinePlatform
   Enable-WindowsOptionalFeature -Online -FeatureName Microsoft-Windows-Subsystem-Linux
   ```
   - Restart your computer

4. **Check Windows Version**
   - Docker Desktop requires Windows 10 (64-bit): Pro, Enterprise, or Education (Build 19044 or higher)
   - Or Windows 11 (64-bit)
   - Check: Press Windows key + R, type `winver`, press Enter

5. **Reset Docker Desktop**
   - Right-click Docker whale icon in system tray
   - Click "Troubleshoot"
   - Click "Reset to factory defaults"
   - Restart Docker Desktop

### Issue: "Access Denied" or Permission Errors

**Solution:**
1. Run Docker Desktop as Administrator (right-click → Run as administrator)
2. Add your user to the "docker-users" group:
   - Open "Computer Management" (Win + X → Computer Management)
   - Navigate to: Local Users and Groups → Groups → docker-users
   - Double-click "docker-users"
   - Click "Add" → enter your username → OK
   - Log out and log back in

### Issue: Port Already in Use (5000 or 8080)

**Check what's using the port:**
```bash
# PowerShell
netstat -ano | findstr :5000
netstat -ano | findstr :8080
```

**Solution 1 - Kill the process:**
```bash
# Find the PID from netstat output, then:
taskkill /PID <PID_NUMBER> /F
```

**Solution 2 - Change ports in docker-compose.yml:**
```yaml
services:
  api:
    ports:
      - "5001:80"  # Changed from 5000
  web:
    ports:
      - "8081:80"  # Changed from 8080
```

### Issue: Slow Performance

**Solutions:**
1. Allocate more resources to Docker:
   - Open Docker Desktop
   - Settings → Resources
   - Increase CPU and Memory
   - Apply & Restart

2. Move Docker data to faster drive:
   - Settings → Resources → Advanced
   - Change "Disk image location"

3. Exclude Docker directories from antivirus:
   - Add these to your antivirus exclusions:
     - `C:\ProgramData\Docker`
     - `C:\Program Files\Docker`
     - `%LOCALAPPDATA%\Docker`

### Issue: WSL 2 Distro Not Found

**Solution:**
```bash
# List distros
wsl --list --verbose

# Install Ubuntu if needed
wsl --install -d Ubuntu

# Set default distro
wsl --set-default Ubuntu
```

## Verification Checklist

Before running VanDaemon, verify:

- [ ] Docker Desktop is running (whale icon in system tray is steady)
- [ ] `docker version` shows both Client and Server info
- [ ] `docker ps` returns a list (even if empty)
- [ ] WSL 2 is installed and running (`wsl --status`)
- [ ] Ports 5000 and 8080 are available

## Alternative: Use Docker without Docker Desktop

If Docker Desktop continues to have issues, you can use Docker in WSL 2 directly:

1. Install Docker in WSL 2:
   ```bash
   # In WSL 2 terminal
   curl -fsSL https://get.docker.com -o get-docker.sh
   sudo sh get-docker.sh
   sudo usermod -aG docker $USER
   ```

2. Start Docker service:
   ```bash
   sudo service docker start
   ```

3. Navigate to VanDaemon (Windows paths are accessible at /mnt/c/):
   ```bash
   cd /mnt/c/projects/vandaemon
   docker compose up -d
   ```

## Getting Help

If issues persist:

1. Check Docker Desktop logs:
   - Docker Desktop → Troubleshoot → View Logs

2. Check WSL logs:
   ```powershell
   wsl --debug-shell
   ```

3. Docker Desktop official troubleshooting:
   https://docs.docker.com/desktop/troubleshoot/overview/

4. VanDaemon Issues:
   https://github.com/StuartF303/vandaemon/issues

## Quick Reference

**Start Docker Desktop:**
```bash
start "" "C:\Program Files\Docker\Docker\Docker Desktop.exe"
```

**Check Docker Status:**
```bash
docker version
docker ps
docker compose version
```

**VanDaemon Commands:**
```bash
# Start
cd C:\projects\vandaemon
docker compose up -d

# View logs
docker compose logs -f

# Stop
docker compose down

# Check status
docker compose ps
```
