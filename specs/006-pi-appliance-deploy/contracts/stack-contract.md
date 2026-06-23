# Contract: Consolidated Compose Stack

The single authoritative `docker-compose.yml` at repo root. Same file for dev and appliance.

## Services

| Service | Image (appliance) | Image (dev) | Ports (host:container) | Restart | Healthcheck |
|---------|-------------------|-------------|------------------------|---------|-------------|
| `api`  | `ghcr.io/<owner>/vandaemon-api:${VANDAEMON_TAG:-latest}` | `build:` from `docker/Dockerfile.api` | `5000:80` | `unless-stopped` | `curl -f http://localhost:80/health` |
| `web`  | `ghcr.io/<owner>/vandaemon-web:${VANDAEMON_TAG:-latest}` | `build:` from `docker/Dockerfile.web` | `8080:80` | `unless-stopped` | (via `depends_on api: service_healthy`) |
| `mqtt` | `eclipse-mosquitto:2.0` | same | `1883:1883`, `9001:9001` | `unless-stopped` | `mosquitto_sub -p 1883 -t '$$SYS/#' -C 1 -W 3` |

> Dev vs appliance image selection: the committed file uses `image:` with a `build:` fallback so
> `docker compose build` works for dev while `docker compose pull` works on the appliance. (Compose
> allows both keys; `build` is used when present and `--build`/local, `image` is the pull/run name.)
> Exact mechanism finalised in implementation; **contract = same service names, ports, env, volumes.**

## API → broker wiring (the load-bearing env contract)

```yaml
api:
  environment:
    - ASPNETCORE_ENVIRONMENT=Production
    - ASPNETCORE_URLS=http://+:80
    - MqttLedDimmer__MqttBroker=mqtt     # service name on the compose network
    - MqttLedDimmer__MqttPort=1883
```

- `MqttLedDimmer__MqttBroker`/`__MqttPort` override `appsettings.json` via .NET's `__` config binding.
- **`appsettings.json` MUST remain `"MqttBroker": "localhost"`** so bare-metal `dotnet run` is unchanged.

## Volumes & network

- Named volumes: `api-data`, `api-logs`, `mqtt-data`, `mqtt-logs`.
- Mosquitto config bind-mounts `./docker/mosquitto/config` (root build context).
- Single bridge network `vandaemon`; services resolve each other by name.
- No `version:` key (obsolete under Compose v2).

## Static acceptance (Class B)

- `docker compose config -q` exits 0.
- Exactly one service uses an `eclipse-mosquitto` image.
- `web.depends_on.api.condition == service_healthy`.
- `grep '"MqttBroker": "localhost"' src/Backend/VanDaemon.Api/appsettings.json` still matches.
- No `docker/docker-compose.yml` present.
