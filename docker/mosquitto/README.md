# VanDaemon MQTT Broker (Mosquitto)

Eclipse Mosquitto MQTT broker for VanDaemon LED dimmer communication and IoT device integration.

## Quick Start

### Start MQTT Broker

```bash
cd docker
docker compose up -d mqtt
```

The broker will be available at:
- **MQTT:** `mqtt://localhost:1883`
- **WebSocket:** `ws://localhost:9001`

### View Logs

```bash
docker logs vandaemon-mqtt -f
```

### Stop Broker

```bash
docker compose stop mqtt
```

## Configuration

### Default Settings

- **Allow Anonymous:** Yes (development mode)
- **Persistence:** Enabled
- **Max Connections:** Unlimited
- **Keepalive:** 60 seconds

### Production Security

For production deployments, enable authentication:

**1. Create Password File:**

```bash
# Create password for vandaemon user
docker exec -it vandaemon-mqtt mosquitto_passwd -c /mosquitto/config/passwd vandaemon

# Add more users (without -c flag)
docker exec -it vandaemon-mqtt mosquitto_passwd /mosquitto/config/passwd leddimmer
```

**2. Edit Configuration:**

Edit `docker/mosquitto/config/mosquitto.conf`:

```conf
# Disable anonymous access
allow_anonymous false

# Enable password authentication
password_file /mosquitto/config/passwd

# Optional: Enable ACLs for topic-level security
acl_file /mosquitto/config/acl.conf
```

**3. Create ACL (Optional):**

Copy `acl.conf.example` to `acl.conf` and customize topic permissions.

**4. Restart Broker:**

```bash
docker restart vandaemon-mqtt
```

## Testing MQTT

### Subscribe to All Topics

```bash
# Using mosquitto_sub (install mosquitto-clients)
mosquitto_sub -h localhost -t 'vandaemon/#' -v

# Or using Docker
docker run --rm -it --network docker_vandaemon eclipse-mosquitto:2.0 \
  mosquitto_sub -h mqtt -t 'vandaemon/#' -v
```

### Publish Test Message

```bash
# Set LED channel 0 to 50% brightness
mosquitto_pub -h localhost -t 'vandaemon/leddimmer/test-device/channel/0/set' -m '128'

# Or using Docker
docker run --rm -it --network docker_vandaemon eclipse-mosquitto:2.0 \
  mosquitto_pub -h mqtt -t 'vandaemon/leddimmer/test-device/channel/0/set' -m '128'
```

## Topic Structure

VanDaemon uses the following MQTT topic hierarchy:

```
vandaemon/
├── leddimmer/
│   └── {deviceId}/
│       ├── status                    → "online"/"offline" (LWT)
│       ├── config                    → Device capabilities (JSON)
│       ├── heartbeat                 → Periodic health status (JSON)
│       ├── all/
│       │   └── set                   ← Set all channels (0-255)
│       └── channel/
│           ├── 0/
│           │   ├── set               ← Command channel 0 (0-255)
│           │   └── state             → Channel 0 brightness
│           ├── 1/
│           │   ├── set
│           │   └── state
│           └── ... (up to channel 7)
```

### Topic Examples

**Device Status:**
```
vandaemon/leddimmer/cabin-lights/status → "online"
```

**Device Configuration:**
```
vandaemon/leddimmer/cabin-lights/config → {
  "deviceId": "cabin-lights",
  "deviceName": "Cabin Lights",
  "channels": 8,
  "version": "1.0.0",
  "variant": "8CH"
}
```

**Set Channel Command:**
```
vandaemon/leddimmer/cabin-lights/channel/0/set ← "128"
vandaemon/leddimmer/cabin-lights/channel/0/set ← {"brightness": 128}
```

**Channel State:**
```
vandaemon/leddimmer/cabin-lights/channel/0/state → "128"
```

**Heartbeat:**
```
vandaemon/leddimmer/cabin-lights/heartbeat → {
  "uptime": 3600,
  "freeHeap": 123456,
  "rssi": -45
}
```

## Monitoring

### View Active Clients

```bash
docker exec vandaemon-mqtt mosquitto_sub -t '$SYS/broker/clients/active' -C 1
```

### View Total Messages

```bash
docker exec vandaemon-mqtt mosquitto_sub -t '$SYS/broker/messages/received' -C 1
```

### View Subscriptions

```bash
docker exec vandaemon-mqtt mosquitto_sub -t '$SYS/broker/subscriptions/count' -C 1
```

## Persistence

MQTT messages and client sessions are persisted in Docker volumes:

- **Data:** `mqtt-data` volume → `/mosquitto/data`
- **Logs:** `mqtt-logs` volume → `/mosquitto/log`

### Backup Data

```bash
docker run --rm -v docker_mqtt-data:/data -v $(pwd):/backup alpine \
  tar czf /backup/mqtt-data-backup.tar.gz -C /data .
```

### Restore Data

```bash
docker run --rm -v docker_mqtt-data:/data -v $(pwd):/backup alpine \
  tar xzf /backup/mqtt-data-backup.tar.gz -C /data
```

## Performance Tuning

For high-traffic deployments, adjust `mosquitto.conf`:

```conf
# Increase message queue
max_queued_messages 10000
max_queued_bytes 10485760

# Increase inflight messages
max_inflight_messages 100

# Adjust keepalive
max_keepalive 300
```

## TLS/SSL Encryption (Optional)

For encrypted MQTT connections:

**1. Generate Certificates:**

```bash
# Create CA and server certificates
# (Use Let's Encrypt, self-signed, or corporate CA)
```

**2. Add to Configuration:**

```conf
listener 8883
protocol mqtt
cafile /mosquitto/config/ca.crt
certfile /mosquitto/config/server.crt
keyfile /mosquitto/config/server.key
require_certificate false
```

**3. Update docker-compose.yml:**

```yaml
ports:
  - "8883:8883"  # Secure MQTT
```

## Troubleshooting

### Broker won't start

Check logs:
```bash
docker logs vandaemon-mqtt
```

Common issues:
- Configuration syntax error
- Port 1883 already in use
- Permission issues with volumes

### Clients can't connect

1. **Check broker is running:**
   ```bash
   docker ps | grep mqtt
   ```

2. **Test connectivity:**
   ```bash
   telnet localhost 1883
   ```

3. **Check firewall:**
   ```bash
   # Ensure port 1883 is open
   sudo ufw status
   ```

4. **Verify configuration:**
   ```bash
   docker exec vandaemon-mqtt cat /mosquitto/config/mosquitto.conf
   ```

### Message not received

1. **Check topic spelling** (case-sensitive!)
2. **Verify QoS settings**
3. **Check ACL permissions** (if enabled)
4. **Monitor broker logs** for errors

## Resources

- [Mosquitto Documentation](https://mosquitto.org/documentation/)
- [MQTT Protocol Specification](https://mqtt.org/)
- [MQTT.fx](https://mqttfx.jensd.de/) - GUI client for testing
- [MQTT Explorer](http://mqtt-explorer.com/) - Visual MQTT client

## Integration with VanDaemon

The MQTT broker is automatically used by:
- **ESP32 LED Dimmers** - Publish/subscribe to control topics
- **VanDaemon Backend** - MqttLedDimmer plugin
- **Other IoT Devices** - Victron Cerbo GX, sensors, etc.

See main VanDaemon documentation for plugin configuration.
