# ShellySpotter

Server room monitoring system using Shelly Door/Window 2, a Raspberry Pi 5 appliance, and a cloud-hosted microservice stack.

## Architecture

```
[Shelly Door/Window 2]  ──(local LAN)──  [Agent / Raspberry Pi 5]
                                               │
                                               │ HTTPS REST/JSON
                                               ▼
                         [Token-MS] ←──── [Core-MS] ────► [Redmine]
                             │                │
                           [Redis]          [MSSQL]
                                               │
                                          [Web-App / Blazor]
```

| Service | Technology | Purpose |
|---------|-----------|---------|
| **Agent** | .NET 10 Worker Service | Polls Shelly, pings IPs, reports to Core-MS |
| **Core-MS** | ASP.NET Core Web API + MSSQL | Business logic, REST API, alerts, tickets |
| **Token-MS** | ASP.NET Core Web API + Redis | JWT issuance, user management |
| **WebApp** | Blazor Server | Dashboard, configuration UI |
| **Redmine** | Docker (on-premise) | Ticket system |

## Quick Start

### Prerequisites
- Docker Desktop
- A `.env` file (copy `.env.example` and fill in values)

```bash
cp .env.example .env
# Edit .env with your secrets
docker compose up -d
```

The web interface is available at **http://localhost**

Redmine runs at **http://localhost:3000** (setup wizard on first run — create a project named `shellyspotter`)

### Demo accounts (development only — change before any real deployment)

Token-MS seeds these accounts on first run. Their passwords are **not** in source —
they come from your `.env` (`ADMIN_PASSWORD`, `EMPLOYEE_PASSWORD`, `CUSTOMER_PASSWORD`,
`AGENT_PASSWORD`). The development defaults shipped in `.env.example` are:

| Username | `.env` variable | Default | Role |
|----------|-----------------|---------|------|
| admin | `ADMIN_PASSWORD` | `Admin1234!` | Admin |
| employee1 | `EMPLOYEE_PASSWORD` | `Employee1234!` | Employee |
| customer1 | `CUSTOMER_PASSWORD` | `Customer1234!` | Customer |
| agent | `AGENT_PASSWORD` | `Agent1234!Secret` | Agent |

> Change all of these in `.env` before any non-local deployment.

## Agent Setup (Raspberry Pi 5)

1. Install Docker on the Pi:
   ```bash
   curl -fsSL https://get.docker.com | sh
   ```

2. Copy `src/ShellySpotter.Agent/` to the Pi and create a local
   `appsettings.Production.json` (gitignored — keep the password out of source):
   ```json
   {
     "Agent": {
       "CoreBaseUrl": "https://<your-core-domain>",
       "TokenServiceUrl": "https://<your-token-domain>",
       "AgentUsername": "agent",
       "AgentPassword": "<must match AGENT_PASSWORD from .env>",
       "RoomId": 1,
       "ShellyBaseUrl": "http://<shelly-ip>",
       "PollIntervalSeconds": 30
     }
   }
   ```
   (Or pass it as an environment variable `Agent__AgentPassword` instead.)

3. Build & run:
   ```bash
   docker build -t shelly-agent .
   docker run -d --restart=unless-stopped --network=host \
     -e DOTNET_ENVIRONMENT=Production \
     -v $(pwd)/appsettings.Production.json:/app/appsettings.Production.json:ro \
     shelly-agent
   ```

## Shelly Door/Window 2 — Webhook Setup

In the Shelly web interface (`http://<shelly-ip>`):
- Go to **Settings → Actions**
- Add an action for **Door opened** and **Door closed**
- URL: `http://<agent-ip>:5000/api/hook` (optional — the agent also polls every 30s)

## API Overview (Core-MS)

All endpoints require `Authorization: Bearer <token>`.

```
GET    /api/rooms
POST   /api/rooms                          [Employee/Admin]
PUT    /api/rooms/{id}                      [Employee/Admin]
GET    /api/rooms/{id}/readings
GET    /api/rooms/{id}/readings/latest
POST   /api/rooms/{id}/readings            [Agent/Employee/Admin]
GET    /api/rooms/{id}/alerts
POST   /api/rooms/{id}/alerts/{id}/resolve [Employee/Admin]
GET    /api/rooms/{id}/ping-targets
POST   /api/rooms/{id}/ping-targets        [Employee/Admin]
POST   /api/rooms/{id}/ping-targets/ping-results [Agent]
GET    /api/rooms/{id}/maintenance-windows
POST   /api/rooms/{id}/maintenance-windows [Employee/Admin]
POST   /api/agent/report                   [Agent]
```

## Alerting

Core-MS raises alerts automatically when an agent report or sensor reading arrives:

- **Door opened outside a maintenance window** → urgent alert + Redmine ticket.
  Suppressed during a configured maintenance window; auto-resolved when the door closes.
- **Temperature above the room's limit** → urgent alert + Redmine ticket.
  The limit is per-room (`HighTemperatureThreshold`, default 28 °C, editable in the
  room configuration). Auto-resolved once the temperature drops back below the limit
  (with a 1 °C hysteresis margin to avoid flapping). Not suppressed by maintenance
  windows, since overheating is an environmental hazard regardless of access.

## Setup & Demo

See **[docs/demo-setup.md](docs/demo-setup.md)** for full setup, Redmine first-time
configuration, and the live Shelly demo over a phone hotspot.

For cloud deployment (VPS + Caddy/TLS) and running the Agent on a Raspberry Pi,
see **[docs/deployment.md](docs/deployment.md)**.

## Security

See [docs/threat-model.md](docs/threat-model.md) for the full STRIDE threat model
(trust boundaries, risk ratings, findings & remediations).

Key controls:
- JWT Bearer tokens (HMAC-SHA256, 8h expiry, Redis blacklist enforced on logout)
- bcrypt password hashing (BCrypt.Net-Next default cost factor)
- Role-based authorization: Customer / Employee / Admin / Agent, plus per-tenant
  object-level checks (`RoomAccessService`)
- Docker network segmentation: `frontend-net`, `backend-net`, `ticket-net`
- Secrets via environment variables — never committed to git
- CI: build gate, TruffleHog secret scan, CycloneDX SBOM

## Diagrams

See [docs/diagrams.md](docs/diagrams.md) for the domain model class diagram, the
Core-MS / Token-MS architecture, and the entity-relationship diagram.

## CI / CD

GitHub Actions, defined in [.github/workflows](.github/workflows):

- **CI** (`ci.yml`) — on every PR and push to `main`: build **and unit-test** the
  solution (xUnit, see `tests/`), run a TruffleHog secret scan, and generate a
  CycloneDX SBOM.
- **CD** (`cd.yml`) — on every push to `main`: build all four service images and
  publish them to the GitHub Container Registry as
  `ghcr.io/vl43den/shellyspotter-<service>:latest` (and a commit-SHA tag). A host
  deploys by pulling those images and running them with `docker-compose.prod.yml`
  (Caddy/TLS reverse proxy).

## Project Structure

```
shellyspotter/
├── src/
│   ├── ShellySpotter.Agent/        # Raspberry Pi worker
│   ├── ShellySpotter.Core/         # Core REST API + MSSQL
│   ├── ShellySpotter.TokenService/ # JWT auth + Redis
│   └── ShellySpotter.WebApp/       # Blazor Server frontend
├── tests/
│   └── ShellySpotter.Core.Tests/   # xUnit tests (alerting, access control, windows)
├── docs/
│   ├── threat-model.md
│   ├── diagrams.md             # class + ER diagrams
│   ├── deployment.md
│   └── demo-setup.md
├── docker-compose.yml
├── .env.example
└── ShellySpotter.sln
```
