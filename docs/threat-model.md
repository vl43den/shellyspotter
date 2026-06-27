# Threat Model: ShellySpotter

**In scope:** the four .NET services (Agent, Core, Token-Service, WebApp), their
data stores (SQL Server, Redis), the Redmine ticket system, and the network and
CI security around them.

**Out of scope (this iteration):** physical security of the Shelly device and the
Raspberry Pi, and the customer's local network. These sit at the customer site on
a physically controlled, trusted LAN.

---

## How the system works

The Agent always starts the connection itself. It asks Core for its config and
sends its reports. Core can never call back into the customer's network, so the
customer's firewall needs no open incoming port.

```mermaid
flowchart TB
    subgraph CUST["Customer site — trusted LAN"]
        SHELLY["Shelly D/W2<br/>HTTP, LAN-only"]
        AGENT["Agent / Raspberry Pi"]
        NET["Network equipment<br/>(ping targets)"]
    end

    USER["Browser — untrusted<br/>Customer / Employee / Admin"]

    subgraph EDGE["Cloud edge — public ingress"]
        CADDY["Caddy reverse proxy (TLS)"]
    end

    subgraph FRONT["frontend-net"]
        WEB["WebApp (Blazor)"]
        CORE["Core API"]
        TOK["Token-Service"]
    end

    subgraph BACKNET["backend-net — no internet"]
        DB[("SQL Server")]
        REDIS[("Redis")]
    end

    subgraph TICKET["ticket-net"]
        REDM["Redmine"]
        RDB[("Redmine DB")]
    end

    AGENT -.->|ICMP ping| NET
    SHELLY -->|"webhook (HTTP, LAN)"| AGENT
    AGENT -->|"HTTPS + JWT"| CADDY
    USER -->|HTTPS| CADDY
    CADDY --> WEB
    CADDY --> CORE
    CADDY --> TOK
    CADDY -->|"tickets.*"| REDM
    WEB --> CORE
    WEB --> TOK
    CORE --> DB
    CORE --> REDIS
    CORE --> REDM
    TOK --> REDIS
    REDM --> RDB
```

## What is worth protecting

We also note which property matters most: **C**onfidentiality, **I**ntegrity,
**A**vailability.

- **Reliable monitoring and alerting (A + I).** The Agent → Core → ticket chain
  must actually raise an alert when a door opens or the temperature is too high.
  If it is silenced or down, a real event is missed. This is the core of the product.
- **Room data** — sensor and ping results **(C + I).** Only the owner should see
  it, and wrong data means wrong or missing alerts.
- **Customer configuration** — maintenance windows, temperature thresholds, ping
  targets **(I).** If tampered with, monitoring breaks silently.
- **User passwords (C)** — bcrypt hashes in Redis.
- **JWT signing secret (C)** — anyone who has it can forge any login or role.
- **The tokens themselves (C)** — a stolen token lets someone act as that user.
- **Database and Redis passwords (C).**
- **Redmine API key (C)** — grants access to the ticket system.
- **Alerts and tickets (I)** — our record of what happened.
- **Source code and build pipeline (I)** — supply-chain integrity.

### Trust boundaries (where data moves into something we trust more)

1. Internet → cloud edge (browser or Agent → Caddy): TLS and a JWT.
2. Cloud edge → frontend-net: services are only reachable through Caddy.
3. frontend-net → backend-net: only Core/Token reach SQL Server and Redis; these
   are not on the internet.
4. Shelly → Agent: plain HTTP, but only on the customer's own trusted LAN.
5. Caddy → Redmine (`tickets.*`): the ticket system must be internet-reachable,
   so it is exposed on purpose, behind TLS.

---

## The threats and what we do

We went through the diagram with **STRIDE** and asked, for each part, "what could
go wrong, and *how*?". Each threat gets a decision, while accepting a small risk is fine
when fixing it is not worth it yet. The "what we do" column is meant to be
actionable: an open item should tell a developer what to build and how to check it.

Not every measure is coded yet which is fine per the course. The Status column
says honestly what is built (done), still planned, or consciously accepted.

| # | What could go wrong, and how | STRIDE | Decision | What we do about it | Status |
|---|------------------------------|--------|----------|---------------------|--------|
| 1 | An attacker without a valid Agent token calls Core's report endpoints directly (or grabs a token with a guessed/leaked Agent password) and pushes false sensor data, causing wrong or missing alerts | Spoofing | Reduce | Core requires a valid JWT; the report endpoints are `[Authorize(Roles="Agent,Employee,Admin")]`, so an anonymous caller is rejected. **To do:** keep the Agent password a strong env secret and rotate it. **Verify:** no token → 401, Customer token → 403 | done |
| 2 | An attacker sends many login attempts (guessed or leaked username/password pairs) to `POST /api/auth/login`, because there is no rate limit, and takes over an account | Spoofing / DoS | Reduce | Rate limiting per-IP **and** per-account on `POST /api/auth/login` (app-level via AspNetCoreRateLimit, and/or edge via Caddy `rate_limit` / fail2ban), ~5/min then 429 + short lockout. **Verify:** 6th attempt in a minute → 429 | planned |
| 3 | An attacker **steals** a valid JWT — via XSS in the WebApp (the token sits in `ProtectedSessionStorage`), sniffing an unencrypted link, or a token leaked in logs/URL | Spoofing / Info disclosure | Reduce | Tokens only over HTTPS, never in URLs or logs; Blazor auto-encodes output, add a CSP header in Caddy to limit XSS. **Verify:** no token appears in server logs or URLs; CSP header is present | planned |
| 4 | An attacker **reuses** a stolen or copied token to call the API as that user before it expires | Spoofing | Reduce | 8 h expiry + logout puts the token's `jti` on a Redis blocklist; both services check it in `OnTokenValidated`. **Verify:** after logout the same token → 401 | done |
| 5 | An attacker puts SQL fragments into an API field (e.g. a filter parameter) to change the query and read or modify other tenants' data | Tampering / Info disclosure | Reduce | EF Core uses parameterized queries; no string-built SQL; input bound via validated DTOs. **Verify:** code-review for `FromSqlRaw`/interpolation; input `' OR 1=1 --` changes nothing | done |
| 6 | If the SQL Server were reachable from the internet, an attacker could connect directly with default/stolen credentials and read or change all data | Info disclosure / Tampering | Reduce | SQL Server is only on the internal `backend-net`, no host port published; only Core reaches it. **To do:** give the app its own least-privilege DB user instead of `sa`. **Verify:** `docker compose config` shows no published DB port; an external connection fails | planned |
| 7 | A device on the customer LAN (or, with a firewall misconfig, from outside) calls the Agent's unprotected `GET /hook/door` and sends fake door/temperature events, causing false alarms or hiding a real intrusion | Spoofing / Tampering | Accept → Reduce | The webhook is only reachable on the customer's trusted LAN (network isolation). **To do (Reduce):** require a shared-secret token in the webhook URL that only Shelly and the Agent know; the Agent drops requests without it. **Verify:** call without the token → 401 | accepted |
| 8 | An attacker between Agent and Core secretly reads or changes the traffic (man-in-the-middle), if the HTTPS certificate check is misconfigured | Tampering / Info disclosure | Reduce | HTTPS everywhere; in production the certificate must be valid, self-signed is allowed only in Development. **Verify:** a production Agent against an invalid certificate → connection fails | done |
| 9 | Someone disputes who created or resolved an alert, and it can't be proven because the alert records no acting user | Repudiation | Reduce | Every alert records `CreatedAt`/`ResolvedAt`, and resolving is Employee/Admin-only. **To do:** also record which staff user resolved an alert. **Verify:** resolve with a Customer token → 403 | planned |
| 10 | A customer opens another customer's room by changing the `roomId` in the URL (the endpoint only checked that you are logged in, not that you own the room) | Info disclosure | Reduce | `RoomAccessService` checks ownership on every room-scoped endpoint. **Verify:** own room → 200, foreign room → 403 | done |
| 11 | The JWT signing secret or a DB/Redis password leaks — e.g. accidentally committed to git or printed in logs — letting an attacker forge tokens or read the DB | Info disclosure | Reduce | Secrets live only in env vars / `.env` (not committed), never in code; our secret scanner (TruffleHog) runs on every build and fails on a real secret. **Verify:** CI fails on a planted live secret | done |
| 12 | An employee uses their legitimate global role to look at customer data they do not need for the task | Info disclosure | Accept | Role checks limit access and actions are logged; revisit (need-to-know, per-record access logs) if the team grows | accepted |
| 13 | An insider (Employee) creates a very wide maintenance window so a real after-hours door opening produces no alert or ticket, and the break-in goes unnoticed | Tampering / Repudiation | Reduce | Creating/deleting a window is Employee/Admin-only. Every door event is logged even when the alert is suppressed (`AlertService`); temperature alerts are deliberately never suppressed. **To do:** cap the maximum window length and log who changes a window. **Verify:** a door event inside a window creates a log entry but no ticket | planned |
| 14 | A door event opens a new ticket every few seconds (ticket spam) | Denial of service | Reduce | No new alert or ticket while one is already open for that room. **Verify:** repeated open events create one ticket, not many | done |
| 15 | One of our NuGet packages has a known security hole, and an attacker uses it | Tampering | Reduce | CI already lists all our packages. **To do:** also scan that list for known holes (`dotnet list package --vulnerable`, or Dependabot) and fail the build on a serious one. **Verify:** CI fails on an old, vulnerable package | planned |
| 16 | Redmine must be internet-reachable (spec); with default logins or unpatched, an attacker reads tickets or abuses the API key | Info disclosure / EoP | Reduce | In production only reachable through Caddy/TLS (dev publishes it on localhost); change the default admin immediately; API key kept as a secret; keep Redmine patched. **Verify:** in prod no Redmine port is published except via Caddy | planned |
| 17 | A customer edits the `role` in their own token to become Admin | Elevation of priv. | Reduce | The role is a signed claim, so changing it breaks the signature. **Verify:** a token with a tampered role → 401 | done |
| 18 | The Agent or Shelly stops reporting (power loss, dead battery, lost network) and a real door or temperature event is missed because no data ever arrives | Denial of service | Reduce | The battery level is already sent with each reading. **To do:** Core tracks each Agent's last-seen time and raises an alert if it goes quiet or the battery is low. **Verify:** stop the Agent → a "no data" alert appears after the timeout | planned |
| 19 | The Redis store is dumped (a backup leak or a compromised host) and the user password hashes inside it are exposed; an attacker then cracks them offline | Info disclosure | Reduce | bcrypt (cost factor 11) makes offline cracking expensive; Redis sits only on `backend-net` and is password-protected, so it is not directly reachable. **Verify:** stored hashes start with `$2` (bcrypt), not plaintext | done |

---

Last reviewed: 2026-06-27.
