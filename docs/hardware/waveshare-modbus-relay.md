# Waveshare Modbus PoE ETH Relay Setup Guide

This guide explains how to configure and use the **Waveshare 8-Channel Modbus PoE ETH Relay** with VanDaemon.

## Hardware Overview

**Product:** Waveshare Modbus PoE ETH Relay
**Documentation:** https://www.waveshare.com/modbus-poe-eth-relay.htm

### Specifications

- **Channels:** 8 relay outputs
- **Contact Rating:** 10A @ 250VAC or 30VDC
- **Contact Form:** 1NO/1NC (normally open/normally closed)
- **Protocol:** Modbus TCP (also supports RTU over serial)
- **Power:** PoE (IEEE 802.3af), DC jack (5.5×2.1mm), or screw terminal (7-36V)
- **Network:** Ethernet with PoE support
- **Default Port:** 502 (Modbus TCP)
- **Enclosure:** DIN rail mountable ABS housing (145×90×40mm)

### Features

- Flash-on/flash-off with automatic shutdown timers
- Optocoupler isolation (protects against high-voltage interference)
- Reverse-polarity protection
- Web-based configuration interface
- DHCP and DNS support

## Initial Setup

### 1. Physical Installation

1. **Power the device:**
   - Connect PoE-enabled Ethernet cable (recommended), OR
   - Connect 12V DC power supply to barrel jack, OR
   - Connect 7-36V to screw terminals

2. **Connect to network:**
   - Connect Ethernet cable to your local network
   - Note: PoE cable provides both power and data

3. **Wire relay outputs:**
   - Each relay has COM, NO, NC terminals
   - Connect your loads (lights, pumps, etc.) to appropriate terminals
   - **IMPORTANT:** Relays switch AC mains voltage - ensure proper electrical safety!

### 2. Network Configuration

The device ships with DHCP enabled. To find its IP address:

**Option 1: Check your router's DHCP leases**
- Log into your router and look for "Waveshare" or "Modbus Relay"
- Note the assigned IP address

**Option 2: Use the manufacturer's discovery tool**
- Download from Waveshare's website
- Run the tool to scan your network

**Option 3: Set static IP via web interface**
1. Connect to device's temporary IP (check documentation)
2. Open web browser: `http://<device-ip>`
3. Navigate to Network Settings
4. Set static IP address (recommended for production)
   - Example: `192.168.1.100`
   - Netmask: `255.255.255.0`
   - Gateway: `192.168.1.1`
5. Save and reboot device

### 3. Test Modbus Communication

Use a Modbus test tool (like "Modbus Poll" or "QModMaster") to verify:

**Connection Settings:**
- Protocol: Modbus TCP
- IP Address: `<your-device-ip>`
- Port: `502`
- Unit ID: `0` (default)

**Test Read:**
- Function Code: 01 (Read Coils)
- Starting Address: 0
- Quantity: 8

**Test Write:**
- Function Code: 05 (Write Single Coil)
- Address: 0 (Relay 1)
- Value: ON (0xFF00) or OFF (0x0000)

You should hear the relay click when toggled.

## VanDaemon Integration

### Adding Relay Controls

1. **Open VanDaemon Web UI**
   - Navigate to Controls page
   - Click "Add Control" button

2. **Basic Configuration**
   - **Name:** e.g., "Main Lights"
   - **Type:** Toggle (for on/off control)
   - **Icon:** Choose appropriate icon (e.g., lightbulb)

3. **Provider Configuration**
   - **Control Provider:** Select "Modbus Device"
   - **Device Type:** Select "Waveshare 8-Channel PoE Relay"
   - **Modbus Address:** `192.168.1.100:502` (your device IP)
   - **Relay Channel:** Select "Relay 1 (Register 0)" through "Relay 8 (Register 7)"

4. **Save**
   - Click "Save" to create the control
   - Test by toggling the control on/off

### Dashboard Integration

After creating controls, you can:

1. Add them to the Dashboard page
2. Position them over your van diagram
3. Control relays from the main dashboard view
4. View real-time status

### Modbus Register Mapping

| Relay Channel | Register Address | Function Code | Description |
|--------------|------------------|---------------|-------------|
| Relay 1      | 0                | FC05         | Coil 0      |
| Relay 2      | 1                | FC05         | Coil 1      |
| Relay 3      | 2                | FC05         | Coil 2      |
| Relay 4      | 3                | FC05         | Coil 3      |
| Relay 5      | 4                | FC05         | Coil 4      |
| Relay 6      | 5                | FC05         | Coil 5      |
| Relay 7      | 6                | FC05         | Coil 6      |
| Relay 8      | 7                | FC05         | Coil 7      |

**Function Codes:**
- **FC01:** Read Coils (read relay state)
- **FC05:** Write Single Coil (control relay)

## Example Use Cases

### 1. Interior Lighting
- Connect LED strip lights to Relay 1 (NO terminal)
- Create "Interior Lights" control in VanDaemon
- Toggle from dashboard or schedule automatically

### 2. Water Pump
- Connect 12V water pump to Relay 2
- Add flow switch for safety (optional)
- Create momentary control for manual operation

### 3. Exterior Lights
- Connect awning lights to Relay 3
- Add to dashboard for easy access
- Set up automation rules (future feature)

### 4. Heating System
- Connect diesel heater relay to Relay 4
- Monitor temperature with separate sensor
- Create automated climate control

## Troubleshooting

### Cannot Connect to Device

**Check network connectivity:**
```bash
ping 192.168.1.100
```

**Test Modbus connection:**
- Use Modbus testing tool to verify device responds
- Check firewall rules (port 502 must be open)
- Verify IP address is correct

### Relays Not Switching

**Verify Modbus communication:**
- Check VanDaemon logs for error messages
- Test with Modbus tool first to isolate issue
- Ensure register addresses are correct (0-7)

**Check wiring:**
- Verify relay coil gets power (listen for click)
- Check load wiring (COM, NO, NC connections)
- Test with multimeter

### Intermittent Connection Issues

**Network stability:**
- Check Ethernet cable quality
- Verify switch/router performance
- Consider static IP instead of DHCP

**Power supply:**
- Ensure adequate PoE budget on switch
- Check DC power supply voltage (if using)
- Verify power LED indicators

## Advanced Configuration

### Web Interface Features

Access the device web interface at `http://<device-ip>`:

- **Network Settings:** Configure IP, netmask, gateway
- **Modbus Settings:** Change unit ID, port
- **Relay Timers:** Configure flash intervals
- **MQTT:** Enable cloud integration (if needed)

### Security Considerations

1. **Network Isolation:**
   - Place Modbus devices on isolated VLAN if possible
   - Restrict access to management port (80/HTTP)

2. **No Authentication:**
   - Modbus TCP has no built-in authentication
   - Control access via network security
   - Do not expose to internet directly

3. **Electrical Safety:**
   - Relays switch mains voltage - hire electrician if unsure
   - Use appropriate circuit protection (fuses/breakers)
   - Follow local electrical codes

## Support and Resources

**Waveshare Documentation:**
- Product page: https://www.waveshare.com/modbus-poe-eth-relay.htm
- Wiki: https://www.waveshare.com/wiki/Modbus_POE_ETH_Relay

**VanDaemon Resources:**
- Plugin documentation: `/docs/deployment/plugin-development.md`
- GitHub issues: https://github.com/yourusername/vandaemon/issues

**Modbus Protocol:**
- Modbus specification: https://modbus.org/docs/Modbus_Application_Protocol_V1_1b3.pdf
- FluentModbus library: https://github.com/Apollo3zehn/FluentModbus

## Wiring Diagram

```
┌─────────────────────────────────────┐
│  Waveshare 8-Channel Relay Module   │
├─────────────────────────────────────┤
│                                     │
│  PoE IN ───── [RJ45] ──────────────┼── To PoE Switch
│                                     │
│  ┌───────┐  ┌───────┐  ┌───────┐  │
│  │Relay 1│  │Relay 2│  │  ...  │  │
│  └───┬───┘  └───┬───┘  └───┬───┘  │
│      │          │          │      │
│   COM NO NC   COM NO NC   COM     │
│    │  │  │    │  │  │    │        │
└────┼──┼──┼────┼──┼──┼────┼────────┘
     │  │  │    │  │  │    │
     └──┼──┘    │  │  └────┼─── To Load
        │       │  └───────┼─── (e.g., lights)
        │       └──────────┼─── To Load
        └──────────────────┘─── (e.g., pump)

COM = Common terminal
NO  = Normally Open (energized when relay on)
NC  = Normally Closed (energized when relay off)
```

## Conclusion

The Waveshare Modbus PoE ETH Relay provides a robust, industrial-grade solution for controlling high-power loads in your camper van. With VanDaemon's Modbus plugin, you can easily integrate it into your smart van system.

For additional help, consult the Waveshare documentation or open an issue on the VanDaemon GitHub repository.
