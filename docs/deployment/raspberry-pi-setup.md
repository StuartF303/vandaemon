# Raspberry Pi Setup Guide

This guide will walk you through setting up VanDaemon on a Raspberry Pi for use in your camper van.

## Hardware Requirements

### Minimum Requirements
- Raspberry Pi 4 Model B (2GB RAM)
- 16GB microSD card (Class 10 or better)
- 5V 3A USB-C power supply
- Network connectivity (WiFi or Ethernet)

### Recommended Requirements
- Raspberry Pi 4 Model B (4GB or 8GB RAM)
- 32GB+ microSD card (Class 10 or better, A2 rated preferred)
- Official Raspberry Pi Power Supply
- Ethernet connection (more stable than WiFi)

### Optional Hardware
- Raspberry Pi case with cooling (fan or heatsinks)
- UPS/Battery backup for power stability
- USB to RS485/RS232 adapter (for Modbus RTU)
- I2C sensors for direct tank level monitoring

## Initial Setup

### 1. Install Raspberry Pi OS

1. Download [Raspberry Pi Imager](https://www.raspberrypi.com/software/)
2. Install Raspberry Pi OS (64-bit recommended)
3. Configure WiFi and SSH during imaging (recommended)
4. Insert the SD card and boot the Pi

### 2. First Boot Configuration

Connect via SSH or directly with keyboard/monitor:

```bash
# Update the system
sudo apt update
sudo apt upgrade -y

# Set hostname (optional but recommended)
sudo raspi-config
# Select: System Options -> Hostname -> vandaemon

# Enable I2C if you plan to use I2C sensors
sudo raspi-config
# Select: Interface Options -> I2C -> Enable

# Set timezone
sudo raspi-config
# Select: Localisation Options -> Timezone
```

### 3. Install Docker

```bash
# Install Docker
curl -fsSL https://get.docker.com -o get-docker.sh
sudo sh get-docker.sh

# Add your user to the docker group
sudo usermod -aG docker $USER

# Log out and log back in for group changes to take effect
# Or run: newgrp docker

# Verify Docker installation
docker --version
docker run hello-world
```

### 4. Install Docker Compose

```bash
# Install Docker Compose
sudo apt install docker-compose -y

# Verify installation
docker-compose --version
```

## VanDaemon Installation

### 1. Clone the Repository

```bash
# Navigate to your home directory
cd ~

# Clone the repository
git clone https://github.com/yourusername/vandaemon.git
cd vandaemon
```

### 2. Configure the Application

```bash
# Copy environment template (if provided)
cp .env.example .env

# Edit configuration
nano .env
```

Configuration options:
- `ASPNETCORE_ENVIRONMENT` - Set to `Production`
- `PLUGIN_TYPE` - Choose your plugin type (Simulated, Modbus, I2C, Victron)
- Plugin-specific settings as needed

### 3. Start the Application

```bash
cd docker
docker-compose up -d
```

### 4. Verify Installation

Check that containers are running:
```bash
docker-compose ps
```

Check logs:
```bash
docker-compose logs -f
```

Access the web interface:
- Open a browser and navigate to `http://raspberrypi.local:8080`
- Or use the Pi's IP address: `http://192.168.1.xxx:8080`

## Auto-Start Configuration

To ensure VanDaemon starts automatically on boot:

### Method 1: Systemd Service (Recommended)

```bash
# Create the service file
sudo nano /etc/systemd/system/vandaemon.service
```

Add the following content (adjust paths if needed):
```ini
[Unit]
Description=VanDaemon Control System
Requires=docker.service
After=docker.service network-online.target
Wants=network-online.target

[Service]
Type=oneshot
RemainAfterExit=yes
User=pi
WorkingDirectory=/home/pi/vandaemon/docker
ExecStart=/usr/bin/docker-compose up -d
ExecStop=/usr/bin/docker-compose down
TimeoutStartSec=300

[Install]
WantedBy=multi-user.target
```

Enable and start the service:
```bash
# Reload systemd
sudo systemctl daemon-reload

# Enable the service
sudo systemctl enable vandaemon

# Start the service
sudo systemctl start vandaemon

# Check status
sudo systemctl status vandaemon
```

### Method 2: Crontab

```bash
# Edit crontab
crontab -e

# Add this line:
@reboot sleep 30 && cd /home/pi/vandaemon/docker && /usr/bin/docker-compose up -d
```

## Hardware Configuration

### Enabling I2C

If you're using I2C sensors:

```bash
# Enable I2C
sudo raspi-config
# Interface Options -> I2C -> Enable

# Install I2C tools
sudo apt install i2c-tools -y

# Add user to i2c group
sudo usermod -aG i2c $USER

# Test I2C (after connecting devices)
i2cdetect -y 1
```

### Configuring Modbus

For Modbus devices:

```bash
# Install Modbus utilities (for testing)
sudo apt install python3-pip -y
pip3 install pymodbus

# For USB to RS485 adapter
# Usually appears as /dev/ttyUSB0 or /dev/ttyAMA0
ls -l /dev/tty*

# Add user to dialout group for serial access
sudo usermod -aG dialout $USER
```

### Victron Cerbo Setup

For Victron Cerbo GX integration:

1. Enable MQTT on Cerbo GX:
   - Go to Settings -> Services -> MQTT
   - Enable "MQTT on LAN"
   - Note the IP address

2. Configure VanDaemon to connect to Cerbo:
   - Update plugin configuration with Cerbo IP
   - Set MQTT broker address

## Network Configuration

### Setting Static IP

For a more reliable setup, configure a static IP:

```bash
# Edit dhcpcd configuration
sudo nano /etc/dhcpcd.conf

# Add these lines (adjust for your network):
interface eth0
static ip_address=192.168.1.100/24
static routers=192.168.1.1
static domain_name_servers=192.168.1.1 8.8.8.8
```

### Setting Up WiFi Access Point

To access VanDaemon without existing WiFi:

```bash
# Install required packages
sudo apt install hostapd dnsmasq -y

# Configure hostapd
sudo nano /etc/hostapd/hostapd.conf
```

Add:
```
interface=wlan0
driver=nl80211
ssid=VanDaemon
hw_mode=g
channel=7
wmm_enabled=0
macaddr_acl=0
auth_algs=1
ignore_broadcast_ssid=0
wpa=2
wpa_passphrase=YourPasswordHere
wpa_key_mgmt=WPA-PSK
wpa_pairwise=TKIP
rsn_pairwise=CCMP
```

## Performance Optimization

### Reduce Memory Usage

```bash
# Disable unnecessary services
sudo systemctl disable bluetooth
sudo systemctl disable cups

# Configure swap
sudo nano /etc/dphys-swapfile
# Set: CONF_SWAPSIZE=1024
sudo dphys-swapfile setup
sudo dphys-swapfile swapon
```

### Improve Docker Performance

```bash
# Clean up unused Docker resources
docker system prune -a

# Monitor Docker stats
docker stats
```

## Monitoring and Maintenance

### Checking System Health

```bash
# Check CPU temperature
vcgencmd measure_temp

# Check system resources
htop

# Check Docker logs
cd ~/vandaemon/docker
docker-compose logs --tail=100 -f
```

### Regular Maintenance

Weekly tasks:
```bash
# Update system
sudo apt update && sudo apt upgrade -y

# Restart VanDaemon (if needed)
cd ~/vandaemon/docker
docker-compose restart
```

Monthly tasks:
```bash
# Clean up Docker
docker system prune -a

# Backup configuration
cp -r ~/vandaemon/docker /backup/location/
```

## Troubleshooting

### Container Won't Start

```bash
# Check logs
docker-compose logs

# Check disk space
df -h

# Check Docker status
systemctl status docker
```

### Can't Access Web Interface

```bash
# Check if containers are running
docker-compose ps

# Check firewall (if enabled)
sudo ufw status

# Check ports
sudo netstat -tulpn | grep -E ':(5000|8080)'
```

### I2C Devices Not Detected

```bash
# Check if I2C is enabled
lsmod | grep i2c

# Test I2C bus
i2cdetect -y 1

# Check permissions
ls -l /dev/i2c-*
groups $USER  # Should include 'i2c'
```

### High CPU Usage

```bash
# Check what's using CPU
top
docker stats

# Reduce polling frequency in configuration
# Edit appsettings.json to increase refresh intervals
```

## Security Considerations

1. **Change Default Passwords**: Change the default Pi password
```bash
passwd
```

2. **Enable Firewall** (optional):
```bash
sudo apt install ufw -y
sudo ufw allow 22    # SSH
sudo ufw allow 8080  # Web UI
sudo ufw enable
```

3. **Keep System Updated**:
```bash
# Set up automatic security updates
sudo apt install unattended-upgrades -y
sudo dpkg-reconfigure --priority=low unattended-upgrades
```

4. **Use HTTPS**: For production use, consider setting up HTTPS with Let's Encrypt

## Additional Resources

- [Raspberry Pi Documentation](https://www.raspberrypi.com/documentation/)
- [Docker Documentation](https://docs.docker.com/)
- [VanDaemon GitHub Repository](https://github.com/yourusername/vandaemon)

## Support

If you encounter issues:
1. Check the troubleshooting section above
2. Review application logs
3. Open an issue on GitHub
4. Ask in the community discussions
