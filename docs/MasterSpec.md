# PartyGame - Master Specification

## ?? Document Status
**Version:** 0.5.0  
**Last Updated:** 2024-02-04  
**Iteration:** 4 - Room Locking & Host-only Actions (Complete)

---

## 1. Architecture Overview

### 1.1 Solution Structure

```
PartyGame/
??? PartyGame.sln
??? docs/
?   ??? MasterSpec.md          # This file
??? PartyGame.Core/            # Domain models, interfaces, pure business logic
?   ??? Models/                # Entities: Room, Player
?   ??? Enums/                 # RoomStatus, ClientRole
?   ??? Interfaces/            # IRoomStore, IConnectionIndex, IClock, IRoomCodeGenerator
?   ??? Services/              # RoomCodeGenerator
??? PartyGame.Server/          # ASP.NET Core Web API + SignalR
?   ??? Controllers/           # REST API endpoints (RoomsController)
?   ??? Hubs/                  # SignalR hubs (GameHub)
?   ??? Services/              # InMemoryRoomStore, ConnectionIndex, LobbyService
?   ??? DTOs/                  # CreateRoomResponseDto, RoomStateDto, PlayerDto, ErrorDto, ErrorCodes
?   ??? Program.cs             # Application entry point
??? PartyGame.Web/             # Static file hosting for TV/Phone UI
?   ??? wwwroot/
?       ??? css/styles.css     # Shared styles
?       ??? js/config.js       # Server URL configuration
?       ??? js/game-client.js  # SignalR client wrapper
?       ??? index.html         # Landing page
?       ??? tv.html            # TV/Host view
?       ??? join.html          # Player join view
??? PartyGame.Tests/           # xUnit tests
    ??? Unit/                  # Unit tests (LobbyServiceTests, etc.)
    ??? Integration/           # Integration tests (GameHubTests, etc.)
```

### 1.2 Design Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| **API Style** | Minimal APIs + Controllers | Minimal APIs for simple endpoints (/health), Controllers for complex REST (/api/rooms) |
| **Realtime** | SignalR | Built-in .NET support, WebSocket abstraction, automatic fallback |
| **Storage (MVP)** | InMemory (ConcurrentDictionary) | Fast iteration, easy to test, will be abstracted via interface for Redis later |
| **Test Framework** | xUnit + FluentAssertions + NSubstitute | Industry standard, readable assertions, proper mocking |
| **Frontend** | Static files (vanilla JS first) | Simple start, can evolve to React/Blazor |
| **Reconnect Strategy** | playerId in localStorage | Survives page refresh, simple to implement |
| **ILobbyService Location** | Server project | Contains SignalR-specific logic and DTO references |
| **Host-only Actions** | Via SignalR connection ID check | Room.HostConnectionId tracks which connection is the host |

### 1.3 Project Dependencies

```
PartyGame.Core          <- No ASP.NET dependencies (pure domain)
PartyGame.Server        <- PartyGame.Core
PartyGame.Web           <- Standalone (static files)
PartyGame.Tests         <- PartyGame.Core, PartyGame.Server, SignalR.Client, NSubstitute
```

---

## 2. Conventions

### 2.1 Naming Conventions

| Element | Convention | Example |
|---------|------------|---------|
| Interfaces | Prefix with `I` | `IRoomStore`, `ILobbyService` |
| DTOs | Suffix with `Dto` | `RoomStateDto`, `PlayerDto` |
| Error codes | SCREAMING_SNAKE_CASE | `ROOM_NOT_FOUND`, `ROOM_LOCKED` |
| Room codes | 4-char uppercase alphanumeric | `A1B2`, `K9F2` |
| Player IDs | GUID | `Guid.NewGuid()` |

### 2.2 Folder Structure Rules

1. **Core project**: No ASP.NET dependencies allowed
2. **Business logic**: Lives in Services, NOT in Controllers/Hubs
3. **DTOs**: Defined in Server project (they are API contracts)
4. **Entities**: Defined in Core project
5. **Error codes**: Centralized in `ErrorCodes` static class

### 2.3 Error Handling

- All SignalR errors sent via `Error(ErrorDto)` event
- REST errors return appropriate HTTP status codes with `ErrorDto` body
- Never expose internal exceptions to clients
- Use `ErrorCodes` constants for consistency

---

## 3. API Routes

### 3.1 REST Endpoints (PartyGame.Server)

| Method | Route | Description | Response |
|--------|-------|-------------|----------|
| GET | `/health` | Health check | `{ status: "healthy", timestamp: "..." }` |
| POST | `/api/rooms` | Create a new room | `CreateRoomResponseDto` |
| GET | `/api/rooms/{code}` | Get room state | `RoomStateDto` |
| GET | `/swagger` | Swagger UI (dev only) | HTML |

### 3.2 Static Routes (PartyGame.Web)

| Route | Description |
|-------|-------------|
| `/` | Landing page |
| `/tv` | Host/TV view (creates room, shows QR) |
| `/join/{code}` | Player join page |
| `/controller` | Player controller view (post-join) |

---

## 4. SignalR Hub

### 4.1 Hub Endpoint

```
/hub/game
```

### 4.2 Client ? Server Methods

| Method | Parameters | Description | Host-only |
|--------|------------|-------------|-----------|
| `RegisterHost` | `roomCode: string` | Host registers for a room after creating via REST | - |
| `JoinRoom` | `roomCode: string, playerId: Guid, displayName: string` | Player joins room | No |
| `LeaveRoom` | `roomCode: string, playerId: Guid` | Player voluntarily leaves | No |
| `SetRoomLocked` | `roomCode: string, isLocked: bool` | Lock or unlock the room | **Yes** |
| `StartGame` | `roomCode: string` | Host starts the game (future) | **Yes** |
| `SubmitAnswer` | `roomCode: string, playerId: Guid, answer: object` | Player submits answer (future) | No |

### 4.3 Server ? Client Events

| Event | Payload | Description |
|-------|---------|-------------|
| `LobbyUpdated` | `RoomStateDto` | Sent when lobby state changes (player join/leave/status/lock) |
| `Error` | `ErrorDto` | Sent when an operation fails |
| `Kicked` | `{ reason: string }` | Sent when player is removed (future) |
| `GameStarted` | `GameStateDto` | Sent when game begins (future) |
| `RoundUpdate` | `RoundStateDto` | Sent during game rounds (future) |

---

## 5. Data Models

### 5.1 Enums

```csharp
public enum RoomStatus { Lobby, InGame, Finished }
public enum ClientRole { Host, Player }
```

### 5.2 Entities (Core)

```csharp
public class Room
{
    public string Code { get; set; }
    public DateTime CreatedUtc { get; set; }
    public RoomStatus Status { get; set; }
    public bool IsLocked { get; set; }
    public string? HostConnectionId { get; set; }
    public Dictionary<Guid, Player> Players { get; set; }
    public int MaxPlayers { get; set; } = 8;
}

public class Player
{
    public Guid PlayerId { get; set; }
    public string DisplayName { get; set; }
    public string? ConnectionId { get; set; }
    public DateTime LastSeenUtc { get; set; }
    public bool IsConnected { get; set; }
    public int Score { get; set; }
}
```

### 5.3 DTOs (Server)

```csharp
public record CreateRoomResponseDto(string RoomCode, string JoinUrl);

public record RoomStateDto(
    string RoomCode,
    RoomStatus Status,
    bool IsLocked,
    IReadOnlyList<PlayerDto> Players
);

public record PlayerDto(
    Guid PlayerId,
    string DisplayName,
    bool IsConnected,
    int Score
);

public record ErrorDto(string Code, string Message);

public static class ErrorCodes
{
    public const string RoomNotFound = "ROOM_NOT_FOUND";
    public const string RoomLocked = "ROOM_LOCKED";
    public const string RoomFull = "ROOM_FULL";
    public const string NameInvalid = "NAME_INVALID";
    public const string NameTaken = "NAME_TAKEN";
    public const string AlreadyHost = "ALREADY_HOST";
    public const string NotHost = "NOT_HOST";
    public const string ConnectionFailed = "CONNECTION_FAILED";
}
```

---

## 6. Interfaces (Core/Server)

```csharp
public interface IRoomStore
{
    Room CreateRoom();
    bool TryGetRoom(string code, out Room? room);
    void Update(Room room);
    void Remove(string code);
    IEnumerable<Room> GetAll();
}

public interface IConnectionIndex
{
    void BindHost(string connectionId, string roomCode);
    void BindPlayer(string connectionId, string roomCode, Guid playerId);
    bool TryGet(string connectionId, out ConnectionBinding? binding);
    void Unbind(string connectionId);
}

public interface ILobbyService
{
    Task<(bool Success, ErrorDto? Error)> RegisterHostAsync(string roomCode, string connectionId);
    Task<(bool Success, ErrorDto? Error)> JoinRoomAsync(string roomCode, Guid playerId, string displayName, string connectionId);
    Task LeaveRoomAsync(string roomCode, Guid playerId);
    Task HandleDisconnectAsync(string connectionId);
    Task<(bool Success, ErrorDto? Error)> SetRoomLockedAsync(string roomCode, string connectionId, bool isLocked);
    bool IsHostOfRoom(string roomCode, string connectionId);
    RoomStateDto? GetRoomState(string roomCode);
}

public interface IClock
{
    DateTime UtcNow { get; }
}
```

---

## 7. Error Codes

| Code | HTTP Status | Description |
|------|-------------|-------------|
| `ROOM_NOT_FOUND` | 404 | Room with given code does not exist |
| `ROOM_LOCKED` | 403 | Room is locked, no new players can join |
| `ROOM_FULL` | 403 | Room has reached max players |
| `NAME_INVALID` | 400 | Display name is empty or too long |
| `NAME_TAKEN` | 409 | Another player in room has same name |
| `ALREADY_HOST` | 409 | Connection is already host of another room |
| `NOT_HOST` | 403 | Action requires host privileges |
| `CONNECTION_FAILED` | - | Failed to connect to server (client-side) |

---

## 8. Iteration Plan

### ? Iteration 0 - Foundation (Complete)
- [x] Solution structure with 4 projects
- [x] Server: Swagger, SignalR hub mapped, health endpoint
- [x] Web: Static file serving with fallback
- [x] Tests: Project setup with sanity tests
- [x] Docs: MasterSpec.md created

### ? Iteration 1 - Room Creation (Complete)
- [x] `IRoomStore` + `InMemoryRoomStore`
- [x] `RoomCodeGenerator` service
- [x] `POST /api/rooms` endpoint
- [x] `GET /api/rooms/{code}` endpoint
- [x] Unit tests for code uniqueness and room creation
- [x] Integration tests for room API

### ? Iteration 2 - Host Registration & Player Join (Complete)
- [x] `IConnectionIndex` + `ConnectionIndex`
- [x] `ILobbyService` + `LobbyService`
- [x] `GameHub.RegisterHost` implementation
- [x] `GameHub.JoinRoom` implementation
- [x] `GameHub.LeaveRoom` implementation
- [x] `OnDisconnectedAsync` handling with `HandleDisconnectAsync`
- [x] `LobbyUpdated` broadcast to room groups
- [x] Reconnect logic (same playerId = update connection)
- [x] Name validation (empty, max length, duplicates)
- [x] Unit tests for ConnectionIndex

### ? Iteration 3 - Web UI (Complete)
- [x] Landing page (`/`) with navigation to TV and Join
- [x] TV page (`/tv`): Create room, show QR code, display live player list
- [x] Join page (`/join/{code}`): Enter name, join room, waiting state
- [x] SignalR client wrapper (`game-client.js`)
- [x] Shared CSS styles (`styles.css`)
- [x] Responsive design for TV and phone
- [x] playerId persistence in localStorage
- [x] Auto-reconnect on page refresh
- [x] CORS configuration for development
- [x] LAN mode support with auto IP detection

### ? Iteration 4 - Room Locking & Host-only Actions (Complete)
- [x] `ErrorCodes` static class for centralized error codes
- [x] `SetRoomLockedAsync` in LobbyService with host validation
- [x] `IsHostOfRoom` method for host verification
- [x] `SetRoomLocked` SignalR hub method (host-only)
- [x] Join blocked when room is locked (reconnects still allowed)
- [x] `LobbyUpdated` includes `isLocked` state
- [x] TV UI: Lock toggle switch with visual feedback
- [x] Join page: Shows ROOM_LOCKED error when room is locked
- [x] Unit tests for LobbyService lock behavior (18 tests)
- [x] Integration tests for SignalR lock operations (8 tests)
- [x] 70 total tests passing

### ?? Iteration 5 - Room Cleanup
- [ ] `RoomCleanupHostedService`
- [ ] Remove rooms without host after timeout
- [ ] Remove disconnected players after grace period
- [ ] Configuration for timeouts

### ?? Iteration 6 - Game Start & State Machine
- [ ] `StartGame` hub method (host-only)
- [ ] Room status transitions (Lobby ? InGame ? Finished)
- [ ] Game phase management
- [ ] UI updates for game state

---

## 9. Running the Project

### Build
```bash
dotnet build
```

### Run Tests
```bash
dotnet test
```

### Run Server (API + SignalR)
```bash
cd PartyGame.Server
dotnet run
# API: https://localhost:7213
# Swagger: https://localhost:7213/swagger
# SignalR: wss://localhost:7213/hub/game
```

### Run Web (Static files)
```bash
cd PartyGame.Web
dotnet run
# Web: https://localhost:7147
# TV: https://localhost:7147/tv
# Join: https://localhost:7147/join/{code}
```

### Run Both for LAN Testing
Use the `lan` profile for testing with mobile devices:
```bash
# Terminal 1 - Server
cd PartyGame.Server && dotnet run --launch-profile lan
# Listens on http://0.0.0.0:5000

# Terminal 2 - Web
cd PartyGame.Web && dotnet run --launch-profile lan
# Listens on http://0.0.0.0:5002
```

Then open `http://<your-ip>:5002/tv` in a browser and scan the QR code with your phone.

---

## 10. Manual Testing Checklist

### Iteration 4: Room Locking

1. **Start both projects** using `lan` profile
2. **Open TV view** at `http://<your-ip>:5002/tv`
3. **Verify** room shows "Room Open" with ?? icon
4. **Click lock toggle** - should change to "Room Locked" with ?? icon
5. **On phone**, scan QR code or navigate to join URL
6. **Try to join locked room** - should see error "This room is locked and not accepting new players"
7. **On TV**, click lock toggle to unlock
8. **On phone**, join the unlocked room - should succeed
9. **On TV**, lock room again after player joined
10. **On phone**, disconnect (close browser) then reconnect - should succeed (reconnect allowed even when locked)

---

## 11. Changelog

### v0.1.0 (Iteration 0)
- Initial solution structure
- Basic Server with Swagger, SignalR, health endpoint
- Basic Web with static file serving
- Test project with sanity tests
- MasterSpec.md created

### v0.2.0 (Iteration 1)
- Room creation via REST API
- `IRoomStore` + `InMemoryRoomStore`
- `RoomCodeGenerator` service
- Unit and integration tests for room creation

### v0.3.0 (Iteration 2)
- Host registration and player join via SignalR
- `IConnectionIndex` + `ConnectionIndex`
- `ILobbyService` + `LobbyService`
- Disconnect handling and reconnect support
- 52 total tests passing

### v0.4.0 (Iteration 3)
- Full Web UI implementation
- TV view with QR code and live player list
- Phone join view with name input
- SignalR client wrapper for easy frontend integration
- Responsive CSS design
- CORS support for development
- LAN mode with auto IP detection

### v0.5.0 (Iteration 4)
- Room locking feature (host-only)
- `ErrorCodes` static class for centralized error codes
- `SetRoomLocked` SignalR hub method
- `SetRoomLockedAsync` and `IsHostOfRoom` in LobbyService
- Lock toggle UI on TV view
- ROOM_LOCKED error handling on join page
- Reconnects allowed even when room is locked
- NSubstitute added for unit test mocking
- 70 total tests passing (18 new for room locking)
