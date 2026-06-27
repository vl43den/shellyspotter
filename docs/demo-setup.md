# Demo Setup & Resume Guide

How to bring ShellySpotter back up from a fresh clone — including the live Shelly
demo over a phone hotspot.

## 1. One-time prerequisites
- Docker Desktop
- .NET 10 SDK
- `dotnet-ef` tool: `dotnet tool install --global dotnet-ef --version 10.0.0`

## 2. Secrets
```bash
cp .env.example .env
```
Fill in `.env` (any values; `JWT_SECRET` must be 32+ chars):
```
JWT_SECRET=shellyspotter-super-secret-key-2024-xyz
DB_PASSWORD=YourStrong!Passw0rd
REDIS_PASSWORD=redispass
REDMINE_DB_ROOT_PASSWORD=rootpass
REDMINE_DB_PASSWORD=redminepass
REDMINE_SECRET_KEY=anyrandomstring
REDMINE_API_KEY=        # filled in after step 4
```

## 3. Start the backend + website
```bash
docker compose up -d
```
Wait ~60 s for MSSQL to initialise. Open **http://localhost** and log in:

| User | Password | Role |
|------|----------|------|
| admin | Admin1234! | Admin |
| employee1 | Employee1234! | Employee |
| customer1 | Customer1234! | Customer |

## 4. Redmine first-time setup (only on a brand-new volume)
The fresh Redmine DB needs its default data and a project. Once:
1. http://localhost:3000 → log in `admin` / `admin` → set a new password
2. **Administration → Settings → API** → enable **REST web service** → Save
3. Create project: **Administration → Projects → New** — identifier **must** be `shellyspotter`
4. If trackers are missing (issue creation returns 422), load defaults + enable trackers:
   ```bash
   docker compose exec redmine bundle exec rake redmine:load_default_data REDMINE_LANG=en
   ```
   Then **Project → Settings → Information** → tick the trackers (Bug/Feature/Support) → Save
5. **My account → API access key → Show** → copy into `.env` as `REDMINE_API_KEY`
6. `docker compose up -d core` to reload the key

## 5. Live Shelly demo over a phone hotspot
The campus wired LAN and WiFi are isolated, and the Shelly is WiFi-only. The
reliable fix is to put the PC and the Shelly on **one phone hotspot** (the Shelly
was already configured for an iPhone hotspot, `172.20.10.x`).

1. Turn on **Personal Hotspot** on the phone. The phone becomes the gateway
   (`172.20.10.1`) and gets internet via cellular.
2. Connect the **PC** (WiFi) to the hotspot.
3. In the Shelly app, set the Shelly's WiFi to the **hotspot SSID** so it joins
   the same network. Note its new IP (Shelly app → device info).
4. Find the PC's hotspot IP:
   ```powershell
   Get-NetIPAddress -AddressFamily IPv4 | Where-Object { $_.IPAddress -like "172.20.10.*" }
   ```
5. Update **two** things:
   - `src/ShellySpotter.Agent/appsettings.json` → `Agent:ShellyBaseUrl` = `http://<shelly-ip>`
   - Shelly app → **Actions**, set each URL (replace `<pc-ip>` with the PC's hotspot IP):

     | Action | URL |
     |--------|-----|
     | Open when dark / twilight / daylight | `http://<pc-ip>:5000/hook/door?state=open&temp={tC}&lux={lux}&bat={bat}` |
     | On close | `http://<pc-ip>:5000/hook/door?state=close&temp={tC}&lux={lux}&bat={bat}` |

     `{tC}`, `{lux}`, `{bat}` are Gen1 Shelly placeholders the device fills in with
     real temperature / lux / battery values.
6. Start the Agent:
   ```powershell
   cd src/ShellySpotter.Agent
   dotnet run
   ```
   It listens on `http://0.0.0.0:5000` so the Shelly can reach it. Windows
   blocks inbound 5000 by default. **Only while demoing**, add a rule scoped to
   the Private profile (admin PowerShell) — and remove it afterwards:
   ```powershell
   # before the demo (mark the hotspot connection as Private first)
   New-NetFirewallRule -DisplayName "ShellySpotter Agent 5000" -Direction Inbound -Protocol TCP -LocalPort 5000 -Action Allow -Profile Private

   # after the demo — close it again
   Remove-NetFirewallRule -DisplayName "ShellySpotter Agent 5000"
   ```
   > Never open port 5000 on a Public profile. Backend APIs (Core/Token/Redmine/
   > WebApp) are bound to `127.0.0.1` and are never exposed on the network.

### Test
- Manual (from any device on the hotspot):
  `http://<pc-ip>:5000/hook/door?state=open&temp=22.5&bat=88&lux=130`
- Physical: open the door → Agent logs `Webhook door=OPEN ... -> forwarded`
- Dashboard (http://localhost) shows door **OPEN** + temp + battery, raises an
  alert, and creates a Redmine ticket (when outside a maintenance window).

## Architecture note — why the Shelly is event-driven
The Shelly Door/Window 2 is **battery-powered and sleeps**: it is not pingable
and HTTP polling times out. It only wakes on a door/sensor event, when it calls
the Agent's webhook (and we read live values from the placeholder query params).
Door state therefore comes from the webhook, never from polling. The `PingWorker`
only handles always-on network equipment, which polls fine.

## Daily start (after first-time setup)
```bash
docker compose up -d            # backend + website (stays up)
cd src/ShellySpotter.Agent && dotnet run   # agent for the Shelly
```
Open http://localhost.
