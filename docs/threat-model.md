# Threat Model — ShellySpotter

## System Overview

ShellySpotter monitors server rooms using a Shelly Door/Window 2 sensor connected to an on-premise
Raspberry Pi (Agent). Data flows from the Agent to the cloud-hosted Core-MS via HTTPS, then to
a Blazor web frontend. A separate Token-MS issues JWT tokens. A Redmine instance manages tickets.

## Architecture Trust Zones

```
[Internet]
    |
    | HTTPS
    |
[Agent — Raspberry Pi]  ←  [Shelly D/W2 — local LAN]
    |
    | HTTPS REST/JSON
    |
[Core-MS] ─── [MSSQL]
    |
[Token-MS] ─── [Redis]
    |
[WebApp — Blazor]
    |
[Redmine ticket system]
```

---

## STRIDE Analysis

### 1. Spoofing

| Asset | Threat | Mitigation |
|-------|--------|-----------|
| Agent identity | Attacker impersonates Agent to push fake sensor data | Agent authenticates with JWT (role=Agent) obtained via Token-MS credentials |
| User login | Brute-force login to Token-MS | Rate limiting (TODO: add middleware); bcrypt password hashing with work factor 10 |
| JWT tokens | Token theft → unauthorized API access | Tokens expire after 8 h; logout blacklists token JTI in Redis; HTTPS only in production |

### 2. Tampering

| Asset | Threat | Mitigation |
|-------|--------|-----------|
| Sensor readings | MITM between Agent and Core-MS | All API calls use HTTPS; TLS certificate validation enforced |
| Shelly ↔ Agent link | Attacker on local LAN intercepts Shelly HTTP | LAN is physically controlled; Shelly webhook can be restricted to Agent IP only |
| Database records | SQL injection via API inputs | EF Core uses parameterized queries; no raw SQL |

### 3. Repudiation

| Asset | Threat | Mitigation |
|-------|--------|-----------|
| Alert creation | Who created or resolved an alert? | Alerts include CreatedAt / ResolvedAt timestamps; authenticated user claims are logged |
| Ticket creation | Disputed ticket | Redmine records creator; Core-MS logs ticket URL |

### 4. Information Disclosure

| Asset | Threat | Mitigation |
|-------|--------|-----------|
| JWT secret | Secret leaked → all tokens forgeable | Stored in environment variable / Docker secret; never in source code |
| DB credentials | Connection string leaked | Passed via environment variables; appsettings.json contains only placeholders |
| Sensor data | Customer A sees Customer B's room data | Role-based API filtering: Customer role is scoped to OwnerId in every query |
| Redis contents | User password hashes exposed | Bcrypt hashes stored; Redis requires authentication password |

### 5. Denial of Service

| Asset | Threat | Mitigation |
|-------|--------|-----------|
| Core-MS API | Flood of sensor data from rogue agent | JWT authentication required; future: rate limiting per token |
| Ticket system | Spam ticket creation on every door event | Duplicate-detection: no new alert created if an open alert already exists for that room |
| MSSQL | Unbounded data growth | Readings and ping results can be pruned; limit parameter on GET endpoints |

### 6. Elevation of Privilege

| Asset | Threat | Mitigation |
|-------|--------|-----------|
| Customer→Employee | Customer manipulates role in JWT | Role embedded in signed JWT; any tampering invalidates signature |
| Employee→Admin | Employee calls Admin-only endpoints | Role-based authorization attributes on all controllers (`[Authorize(Roles = "Admin")]`) |
| Agent→Admin | Agent token used for management actions | Agent role is separate; management endpoints require Employee or Admin |

---

## Key Security Controls Summary

1. **Authentication**: JWT Bearer tokens issued by Token-MS; shared HMAC-SHA256 secret.
2. **Authorization**: Role-based (`Customer`, `Employee`, `Admin`, `Agent`) on every endpoint.
3. **Password storage**: bcrypt with work factor 10 (BCrypt.Net-Next).
4. **Token invalidation**: Redis-backed blacklist with TTL matching token expiry.
5. **Transport security**: HTTPS enforced in production; Docker networks isolate backend services.
6. **Secrets management**: All secrets via environment variables / `.env` file; `.env` in `.gitignore`.
7. **Input validation**: EF Core parameterized queries; DTO-level binding prevents mass assignment.
8. **Data isolation**: Customer role limited to their own rooms at the query level.
9. **Docker network segmentation**: `frontend-net`, `backend-net`, `ticket-net` limit blast radius.

---

## Residual Risks & Recommendations

| Risk | Recommendation |
|------|---------------|
| No HTTPS on Shelly → Agent link | Use a reverse proxy or VPN on the LAN |
| No rate limiting on login endpoint | Add `AspNetCoreRateLimit` middleware |
| Agent password in appsettings.json | Mount as Docker secret or environment variable |
| Redmine exposed on port 3000 | Put behind reverse proxy with TLS |
| MSSQL SA account used | Create a least-privilege DB user for production |
