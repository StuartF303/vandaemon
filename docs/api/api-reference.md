# API Reference

VanDaemon exposes a RESTful API for monitoring and controlling your camper van systems.

## Base URL

- Development: `http://localhost:5000`
- Production: `http://your-raspberry-pi-ip:5000`

## Authentication

Currently, the API does not require authentication. In production environments, consider implementing authentication using JWT tokens or API keys.

## Common Response Codes

| Code | Description |
|------|-------------|
| 200 | Success |
| 201 | Created |
| 204 | No Content (successful delete) |
| 400 | Bad Request |
| 404 | Not Found |
| 500 | Internal Server Error |

## Tanks API

### Get All Tanks

```http
GET /api/tanks
```

Returns a list of all configured tanks.

**Response:**
```json
[
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "name": "Fresh Water",
    "type": "FreshWater",
    "currentLevel": 75.5,
    "capacity": 100,
    "lowLevelThreshold": 10,
    "highLevelThreshold": 90,
    "sensorPlugin": "Simulated Sensor Plugin",
    "sensorConfiguration": {
      "sensorId": "fresh_water"
    },
    "lastUpdated": "2024-01-15T10:30:00Z",
    "isActive": true
  }
]
```

### Get Tank by ID

```http
GET /api/tanks/{id}
```

Returns details for a specific tank.

**Parameters:**
- `id` (path): Tank ID (GUID)

**Response:**
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "name": "Fresh Water",
  "type": "FreshWater",
  "currentLevel": 75.5,
  "capacity": 100,
  "lowLevelThreshold": 10,
  "highLevelThreshold": 90,
  "sensorPlugin": "Simulated Sensor Plugin",
  "sensorConfiguration": {
    "sensorId": "fresh_water"
  },
  "lastUpdated": "2024-01-15T10:30:00Z",
  "isActive": true
}
```

### Get Current Tank Level

```http
GET /api/tanks/{id}/level
```

Reads the current level from the sensor and returns the value.

**Parameters:**
- `id` (path): Tank ID (GUID)

**Response:**
```json
{
  "tankId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "level": 75.5
}
```

### Refresh All Tank Levels

```http
POST /api/tanks/refresh
```

Triggers a refresh of all tank levels from their respective sensors.

**Response:**
```json
{
  "message": "All tank levels refreshed"
}
```

### Update Tank Configuration

```http
PUT /api/tanks/{id}
```

Updates the configuration for a tank (does not change the current level).

**Parameters:**
- `id` (path): Tank ID (GUID)

**Request Body:**
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "name": "Fresh Water Tank",
  "type": "FreshWater",
  "currentLevel": 75.5,
  "capacity": 120,
  "lowLevelThreshold": 15,
  "highLevelThreshold": 85,
  "sensorPlugin": "Modbus Sensor Plugin",
  "sensorConfiguration": {
    "ipAddress": "192.168.1.100",
    "port": 502,
    "registerId": "1000"
  },
  "isActive": true
}
```

**Response:**
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "name": "Fresh Water Tank",
  "type": "FreshWater",
  "currentLevel": 75.5,
  "capacity": 120,
  "lowLevelThreshold": 15,
  "highLevelThreshold": 85,
  "sensorPlugin": "Modbus Sensor Plugin",
  "sensorConfiguration": {
    "ipAddress": "192.168.1.100",
    "port": 502,
    "registerId": "1000"
  },
  "lastUpdated": "2024-01-15T10:35:00Z",
  "isActive": true
}
```

### Create Tank

```http
POST /api/tanks
```

Creates a new tank configuration.

**Request Body:**
```json
{
  "name": "Grey Water",
  "type": "WasteWater",
  "capacity": 80,
  "lowLevelThreshold": 10,
  "highLevelThreshold": 90,
  "sensorPlugin": "I2C Sensor Plugin",
  "sensorConfiguration": {
    "i2cAddress": "0x48",
    "channel": 0
  }
}
```

**Response:**
```json
{
  "id": "7ea85f64-9827-4562-b3fc-2c963f66afa7",
  "name": "Grey Water",
  "type": "WasteWater",
  "currentLevel": 0,
  "capacity": 80,
  "lowLevelThreshold": 10,
  "highLevelThreshold": 90,
  "sensorPlugin": "I2C Sensor Plugin",
  "sensorConfiguration": {
    "i2cAddress": "0x48",
    "channel": 0
  },
  "lastUpdated": "2024-01-15T10:40:00Z",
  "isActive": true
}
```

### Delete Tank

```http
DELETE /api/tanks/{id}
```

Marks a tank as inactive (soft delete).

**Parameters:**
- `id` (path): Tank ID (GUID)

**Response:** 204 No Content

## Tank Types

| Type | Description |
|------|-------------|
| `FreshWater` | Drinking/usable water |
| `WasteWater` | Grey or black water |
| `LPG` | Liquefied Petroleum Gas |
| `Fuel` | Diesel or gasoline |
| `Battery` | Battery level (as percentage) |

## Controls API (Coming Soon)

### Get All Controls

```http
GET /api/controls
```

### Get Control by ID

```http
GET /api/controls/{id}
```

### Get Control State

```http
GET /api/controls/{id}/state
```

### Set Control State

```http
POST /api/controls/{id}/state
```

**Request Body:**
```json
{
  "state": true
}
```

For dimmer controls:
```json
{
  "state": 75
}
```

## Settings API (Coming Soon)

### Get System Configuration

```http
GET /api/settings
```

### Update System Configuration

```http
PUT /api/settings
```

**Request Body:**
```json
{
  "vanModel": "Mercedes Sprinter LWB",
  "vanDiagramPath": "/diagrams/sprinter-lwb.svg",
  "alertSettings": {
    "tankLowLevelThreshold": 10,
    "tankHighLevelThreshold": 90,
    "enableAudioAlerts": true,
    "enablePushNotifications": false
  },
  "pluginConfigurations": {}
}
```

### Get Available Van Diagrams

```http
GET /api/settings/van-diagrams
```

## Alerts API (Coming Soon)

### Get All Alerts

```http
GET /api/alerts?includeAcknowledged=false
```

**Query Parameters:**
- `includeAcknowledged` (optional): Include acknowledged alerts (default: false)

### Acknowledge Alert

```http
POST /api/alerts/{id}/acknowledge
```

### Delete Alert

```http
DELETE /api/alerts/{id}
```

## WebSocket / SignalR (Coming Soon)

VanDaemon will support real-time updates via SignalR for live monitoring.

### Telemetry Hub

Connect to `/hubs/telemetry` for real-time tank level updates.

**Events:**
- `TankLevelUpdated`: Fired when a tank level changes
- `ControlStateChanged`: Fired when a control state changes
- `AlertCreated`: Fired when a new alert is created

**Example (JavaScript):**
```javascript
const connection = new signalR.HubConnectionBuilder()
    .withUrl("http://localhost:5000/hubs/telemetry")
    .build();

connection.on("TankLevelUpdated", (tankId, level) => {
    console.log(`Tank ${tankId} level: ${level}%`);
});

await connection.start();
```

## Error Responses

All error responses follow this format:

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "Bad Request",
  "status": 400,
  "detail": "The request body is invalid",
  "errors": {
    "name": ["The name field is required"]
  }
}
```

## Rate Limiting

Currently, there is no rate limiting. Consider implementing rate limiting for production use.

## Examples

### cURL Examples

**Get all tanks:**
```bash
curl http://localhost:5000/api/tanks
```

**Get tank level:**
```bash
curl http://localhost:5000/api/tanks/3fa85f64-5717-4562-b3fc-2c963f66afa6/level
```

**Refresh all tanks:**
```bash
curl -X POST http://localhost:5000/api/tanks/refresh
```

**Update tank:**
```bash
curl -X PUT http://localhost:5000/api/tanks/3fa85f64-5717-4562-b3fc-2c963f66afa6 \
  -H "Content-Type: application/json" \
  -d '{
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "name": "Fresh Water Tank",
    "type": "FreshWater",
    "capacity": 120,
    "lowLevelThreshold": 15,
    "highLevelThreshold": 85,
    "sensorPlugin": "Simulated Sensor Plugin",
    "sensorConfiguration": {"sensorId": "fresh_water"},
    "isActive": true
  }'
```

### JavaScript/TypeScript Examples

**Fetch all tanks:**
```javascript
const response = await fetch('http://localhost:5000/api/tanks');
const tanks = await response.json();
console.log(tanks);
```

**Update tank level:**
```javascript
const tankId = '3fa85f64-5717-4562-b3fc-2c963f66afa6';
const response = await fetch(`http://localhost:5000/api/tanks/${tankId}/level`);
const data = await response.json();
console.log(`Tank level: ${data.level}%`);
```

### Python Examples

```python
import requests

# Get all tanks
response = requests.get('http://localhost:5000/api/tanks')
tanks = response.json()

# Get specific tank level
tank_id = '3fa85f64-5717-4562-b3fc-2c963f66afa6'
response = requests.get(f'http://localhost:5000/api/tanks/{tank_id}/level')
level_data = response.json()
print(f"Tank level: {level_data['level']}%")

# Refresh all tanks
response = requests.post('http://localhost:5000/api/tanks/refresh')
print(response.json())
```

## OpenAPI / Swagger Documentation

Interactive API documentation is available at:
- `http://localhost:5000/swagger` (when running in development mode)

The Swagger UI provides:
- Complete API schema
- Interactive testing interface
- Request/response examples
- Model definitions

## Versioning

The current API version is v1. Future versions will be accessible via `/api/v2/`, `/api/v3/`, etc.

## Support

For API issues or questions:
- Check the [GitHub Issues](https://github.com/yourusername/vandaemon/issues)
- Review the [source code](https://github.com/yourusername/vandaemon)
- Join the community discussions
