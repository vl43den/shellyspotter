# Diagrams — ShellySpotter

UML class diagrams and the entity-relationship diagram for the ShellySpotter domain.
All diagrams use Mermaid and render directly on GitHub.

---

## 1. Domain Model (Class Diagram)

The core persistent entities and their relationships. `Room` is the aggregate root: every
sensor reading, alert, ping target and maintenance window belongs to exactly one room
(composition — children do not exist without their room). `User` lives in the separate
Token-MS (Redis) store and is linked only logically: a room's `OwnerId` matches a customer's
`Username`.

```mermaid
classDiagram
    class Room {
        +int Id
        +string Name
        +string Description
        +string OwnerId
        +double HighTemperatureThreshold
    }
    class SensorReading {
        +int Id
        +int RoomId
        +DateTime Timestamp
        +double? Temperature
        +bool DoorOpen
        +double? Brightness
        +int? BatteryPercent
    }
    class Alert {
        +int Id
        +int RoomId
        +string Type
        +string Message
        +DateTime CreatedAt
        +DateTime? ResolvedAt
        +string? TicketUrl
    }
    class PingTarget {
        +int Id
        +int RoomId
        +string Name
        +string IpAddress
        +bool IsEnabled
    }
    class PingResult {
        +int Id
        +int PingTargetId
        +DateTime Timestamp
        +bool IsReachable
        +long? RoundTripMs
    }
    class MaintenanceWindow {
        +int Id
        +int RoomId
        +DayOfWeek DayOfWeek
        +TimeSpan StartTime
        +TimeSpan EndTime
        +string? Label
    }
    class User {
        +string Id
        +string Username
        +string Email
        +string PasswordHash
        +string Role
    }

    Room "1" *-- "0..*" SensorReading : records
    Room "1" *-- "0..*" Alert : raises
    Room "1" *-- "0..*" PingTarget : monitors
    Room "1" *-- "0..*" MaintenanceWindow : schedules
    PingTarget "1" *-- "0..*" PingResult : produces
    Room ..> User : OwnerId ↔ Username
```

**Notes**
- `?` marks a nullable field (e.g. `Temperature` is null when a reading only reports door state).
- `Alert.Type` is a discriminator string: `"DoorOpenedOutsideMaintenance"` or `"TemperatureHigh"`.
- `User.Role` ∈ { `Customer`, `Employee`, `Admin`, `Agent` }.

---

## 2. Entity-Relationship Diagram (Database Schema)

The relational schema persisted in MSSQL (via EF Core). These are the six tables created by
the `InitialCreate` + `AddHighTemperatureThreshold` migrations. `User` is **not** shown here:
it lives in Redis (Token-MS), a NoSQL store, and is linked only logically via `Room.OwnerId`.

```mermaid
erDiagram
    ROOM ||--o{ SENSOR_READING : records
    ROOM ||--o{ ALERT : raises
    ROOM ||--o{ PING_TARGET : monitors
    ROOM ||--o{ MAINTENANCE_WINDOW : schedules
    PING_TARGET ||--o{ PING_RESULT : produces

    ROOM {
        int Id PK
        string Name
        string Description
        string OwnerId
        double HighTemperatureThreshold
    }
    SENSOR_READING {
        int Id PK
        int RoomId FK
        datetime Timestamp
        double Temperature "nullable"
        bool DoorOpen
        double Brightness "nullable"
        int BatteryPercent "nullable"
    }
    ALERT {
        int Id PK
        int RoomId FK
        string Type
        string Message
        datetime CreatedAt
        datetime ResolvedAt "nullable"
        string TicketUrl "nullable"
    }
    PING_TARGET {
        int Id PK
        int RoomId FK
        string Name
        string IpAddress
        bool IsEnabled
    }
    PING_RESULT {
        int Id PK
        int PingTargetId FK
        datetime Timestamp
        bool IsReachable
        long RoundTripMs "nullable"
    }
    MAINTENANCE_WINDOW {
        int Id PK
        int RoomId FK
        int DayOfWeek
        time StartTime
        time EndTime
        string Label "nullable"
    }
```

---

## 3. Application Architecture — Core-MS (Class Diagram)

How the Core-MS layers collaborate. Controllers depend on services and the `AppDbContext`; all
wiring is constructor-injected (ASP.NET Core DI). `RoomAccessService` centralises the per-tenant
authorization check used by every room-scoped controller (see threat model F1).

```mermaid
classDiagram
    class RoomsController {
        <<Controller>>
    }
    class SensorReadingsController {
        <<Controller>>
    }
    class AlertsController {
        <<Controller>>
    }
    class PingTargetsController {
        <<Controller>>
    }
    class MaintenanceWindowsController {
        <<Controller>>
    }
    class AgentController {
        <<Controller>>
    }
    class AlertService {
        <<Service>>
        +HandleSensorReadingAsync(reading)
        +ResolveDoorAlertsAsync(roomId)
        +ResolveTemperatureAlertsAsync(roomId)
    }
    class MaintenanceWindowService {
        <<Service>>
        +IsWithinMaintenanceWindowAsync(roomId, utcNow)
    }
    class TicketService {
        <<Service>>
        +CreateTicketAsync(subject, description)
    }
    class RoomAccessService {
        <<Service>>
        +CanAccessRoomAsync(user, roomId)
    }
    class AppDbContext {
        <<EF Core>>
        +DbSet~Room~ Rooms
        +DbSet~SensorReading~ SensorReadings
        +DbSet~Alert~ Alerts
        +DbSet~PingTarget~ PingTargets
        +DbSet~PingResult~ PingResults
        +DbSet~MaintenanceWindow~ MaintenanceWindows
    }
    class Redmine {
        <<External>>
    }

    RoomsController ..> AppDbContext
    SensorReadingsController ..> AlertService
    SensorReadingsController ..> RoomAccessService
    SensorReadingsController ..> AppDbContext
    AlertsController ..> RoomAccessService
    AlertsController ..> AppDbContext
    PingTargetsController ..> RoomAccessService
    PingTargetsController ..> AppDbContext
    MaintenanceWindowsController ..> RoomAccessService
    MaintenanceWindowsController ..> AppDbContext
    AgentController ..> AlertService
    AgentController ..> AppDbContext
    AlertService ..> MaintenanceWindowService
    AlertService ..> TicketService
    AlertService ..> AppDbContext
    MaintenanceWindowService ..> AppDbContext
    RoomAccessService ..> AppDbContext
    TicketService ..> Redmine
```

---

## 4. Authentication — Token-MS (Class Diagram)

Token-MS is a small, separate service backed by Redis: it issues and revokes JWTs and stores users.

```mermaid
classDiagram
    class AuthController {
        <<Controller>>
        +Login(request)
        +Logout()
        +Register(request)
        +Me()
    }
    class UserService {
        <<Service>>
        +ValidateCredentialsAsync(username, password)
        +RegisterAsync(request)
        +SeedDefaultUsersAsync()
    }
    class JwtService {
        <<Service>>
        +GenerateToken(user)
        +BlacklistTokenAsync(token)
        +IsTokenBlacklistedAsync(jti)
    }
    class Redis {
        <<NoSQL store>>
    }

    AuthController ..> UserService
    AuthController ..> JwtService
    UserService ..> Redis : users
    JwtService ..> Redis : blacklist
```
