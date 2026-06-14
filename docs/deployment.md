# Cloud Deployment + Raspberry Pi Appliance

This is the production topology from the project diagram: the Agent runs on-site
on the Raspberry Pi (behind the customer firewall), the rest runs in the cloud,
and the Pi reaches the cloud outbound over HTTPS.

```
[Shelly] --LAN/HTTP--> [Raspberry Pi: Agent] --HTTPS--> [VPS]
                                                          │
                                            Caddy (TLS, :443)
                                            ├── webapp   (dashboard)
                                            ├── core     (REST API)
                                            ├── token-ms (JWT)
                                            └── redmine  (tickets)
                                            + MSSQL + Redis (internal only)
```

> MSSQL has no ARM64 image, so the **full stack cannot run on the Pi**. The Pi runs
> the **Agent only**; Core/DB live in the cloud (or any x86 host).

---

## Part A — Cloud (any x86 VPS: Hetzner, DigitalOcean, Azure VM…)

### 1. DNS
Point these A records at the VPS public IP (edit names in `Caddyfile`):
```
shellyspotter.example.com        -> dashboard
api.shellyspotter.example.com    -> Core
auth.shellyspotter.example.com   -> Token-MS
tickets.shellyspotter.example.com-> Redmine
```

### 2. Prepare the host
```bash
git clone <your-repo> && cd shellyspotter
cp .env.example .env          # fill in STRONG, unique secrets
# edit Caddyfile -> replace example.com with your real domains
```

### 3. Bring it up (base + prod overlay)
```bash
docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d
```
> Requires Docker Compose **v2.24+** (the overlay uses `!reset` to drop the dev
> host port mappings). Check with `docker compose version`.
Caddy obtains Let's Encrypt certificates automatically. Only ports **80/443** are
exposed; Core/Token/Redmine/DB/Redis are reachable only on internal networks.

### 4. Redmine first-time setup
Same as [demo-setup.md](demo-setup.md) §4, but browse to
`https://tickets.shellyspotter.example.com`.

### Production differences (handled by `docker-compose.prod.yml`)
- Caddy is the only internet-facing service (TLS terminator)
- All other host port mappings removed
- `AllowedCorsOrigin` set → Core/Token restrict CORS to the dashboard origin
- Use real secrets in `.env` (never commit it)

---

## Part B — Raspberry Pi (Agent)

### 1. Install Docker on the Pi
```bash
curl -fsSL https://get.docker.com | sh
```

### 2. Configure the Agent
Create `appsettings.Production.json` next to the Agent (do NOT commit it):
```json
{
  "Agent": {
    "CoreBaseUrl": "https://api.shellyspotter.example.com",
    "TokenServiceUrl": "https://auth.shellyspotter.example.com",
    "AgentUsername": "agent",
    "AgentPassword": "<strong-agent-password>",
    "RoomId": 1,
    "ShellyBaseUrl": "http://<shelly-lan-ip>",
    "PollIntervalSeconds": 30
  }
}
```

### 3. Build (natively on the Pi — it's ARM64) and run
```bash
cd src/ShellySpotter.Agent
docker build -t shelly-agent .
docker run -d --restart=unless-stopped \
  --network host \
  -e DOTNET_ENVIRONMENT=Production \
  -v $(pwd)/appsettings.Production.json:/app/appsettings.Production.json:ro \
  shelly-agent
```
`--network host` lets the Agent both reach the Shelly on the LAN and receive the
Shelly's webhook on port 5000.

### 4. Point the Shelly at the Pi
In the Shelly app → Actions, set the Open/Close URLs to the **Pi's LAN IP**:
```
http://<pi-lan-ip>:5000/hook/door?state=open&temp={tC}&lux={lux}&bat={bat}
http://<pi-lan-ip>:5000/hook/door?state=close&temp={tC}&lux={lux}&bat={bat}
```

### Why this matches the assignment
The Agent is never reachable from Core (it sits behind the customer firewall); it
only makes outbound HTTPS calls to the cloud. The Shelly talks to the Agent purely
on the local network. Exactly the diagram.
