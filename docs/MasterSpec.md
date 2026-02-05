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

### ? Iteration 5 - Room Cleanup/TTL
- [x] `RoomCleanupHostedService` background service
- [x] `RoomCleanupOptions` configuration class
- [x] `HostDisconnectedAtUtc` tracking on Room model
- [x] Remove rooms without host after TTL (default 10 min)
- [x] Remove disconnected players after grace period (default 120s)
- [x] Configuration in `appsettings.json`
- [x] Cleanup broadcasts `LobbyUpdated` when players removed
- [x] Unit tests for cleanup logic (14 tests)
- [x] 84 total tests passing

### ? Iteration 6 - Game Session Skeleton + Start Game
- [x] `GameType` enum (Quiz)
- [x] `GameSession` model (GameType, Phase, StartedUtc, State)
- [x] `CurrentGame` property on Room model
- [x] `GameSessionDto` for client communication
- [x] Extended `RoomStateDto` with `currentGame`
- [x] `StartGameAsync` in `ILobbyService` and `LobbyService`
- [x] `StartGame` SignalR hub method (host-only)
- [x] Error codes: `INVALID_STATE`, `NOT_ENOUGH_PLAYERS`
- [x] Validation: room exists, caller is host, status=Lobby, ?2 players
- [x] Side effects: status?InGame, isLocked?true, CurrentGame created
- [x] Broadcasts both `LobbyUpdated` and `GameStarted` events
- [x] TV UI: Working Start Game button with overlay
- [x] Phone UI: Shows "Game Started!" when status=InGame
- [x] `game-client.js`: `startGame(gameType)` method, `onGameStarted` callback
- [x] Unit tests for StartGameAsync (6 tests)
- [x] Integration tests for StartGame (4 tests)
- [x] 94 total tests passing

### ? Iteration 7 - Quiz Questions (Planned)
- [ ] Question model and storage
- [ ] Question phases (Show, Answer, Results)
- [ ] Player answer submission
- [ ] Phase transitions

---

## Quiz Question Bank

### JSON Schema

Question packs are stored in `PartyGame.Server/Content/questions.{locale}.json`.

```json
{
  "schemaVersion": 1,
  "packId": "general-knowledge-nl-be",
  "title": "Algemene Kennis",
  "locale": "nl-BE",
  "tags": ["general", "trivia"],
  "questions": [
    {
      "id": "geo-001",
      "category": "Geografie",
      "difficulty": 1,
      "question": "Wat is de hoofdstad van België?",
      "options": [
        { "key": "A", "text": "Antwerpen" },
        { "key": "B", "text": "Brussel" },
        { "key": "C", "text": "Gent" },
        { "key": "D", "text": "Luik" }
      ],
      "correctOptionKey": "B",
      "explanation": "Brussel is de hoofdstad van België.",
      "timeLimitSeconds": 15,
      "shuffleOptions": true,
      "tags": ["belgium", "capitals"],
      "source": { "type": "original", "ref": null }
    }
  ]
}
```

### Validation Rules

| Rule | Error |
|------|-------|
| `packId` required | "PackId is required" |
| `locale` required | "Pack Locale is required" |
| `questions` not empty | "Pack must contain at least one question" |
| `id` unique per pack | "Duplicate Id '{id}'" |
| `question` text required | "Question text is required" |
| `difficulty` 1-5 | "Difficulty must be between 1 and 5" |
| Exactly 4 options | "Must have exactly 4 options" |
| Option keys unique | "Duplicate option key '{key}'" |
| `correctOptionKey` valid | "CorrectOptionKey does not match any option" |

### Content Location

```
PartyGame.Server/
??? Content/
?   ??? questions.nl-BE.json    # Dutch (Belgium) questions
?   ??? questions.en-US.json    # (future) English questions
```

### Adding New Question Packs

1. Create a new JSON file: `Content/questions.{locale}.json`
2. Follow the schema above with unique `packId`
3. Each question needs:
   - Unique `id` within the pack
   - Exactly 4 options with keys A, B, C, D
   - Valid `correctOptionKey` matching one option
   - `difficulty` between 1 and 5
4. Restart the server to load new content

### IQuizQuestionBank Interface

```csharp
IEnumerable<QuizQuestion> GetAll(string locale);
QuizQuestion? GetRandom(string locale, string? category, int? difficulty, 
                        IEnumerable<string>? tags, IEnumerable<string>? excludeIds);
bool TryGetById(string questionId, out QuizQuestion? question);
IReadOnlySet<string> GetAvailableLocales();
IReadOnlySet<string> GetAvailableCategories(string locale);
int GetCount(string locale);
```

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

### v0.6.0 (Iteration 5)
- Room cleanup background service
- `RoomCleanupHostedService` with configurable intervals
- `RoomCleanupOptions` for TTL and grace period settings
- Host disconnect timestamp tracking
- Automatic removal of hostless rooms and disconnected players
- 84 total tests passing (14 new for cleanup)

### v0.7.0 (Iteration 6)
- Game session skeleton implementation
- `GameType` enum and `GameSession` model
- `StartGame` SignalR hub method (host-only)
- New error codes: `INVALID_STATE`, `NOT_ENOUGH_PLAYERS`
- `GameStarted` event broadcast
- TV UI: Working Start Game with overlay
- Phone UI: Game active state display
- `game-client.js`: `startGame(gameType)` method, `onGameStarted` callback
- Unit tests for StartGameAsync (6 tests)
- Integration tests for StartGame (4 tests)
- 94 total tests passing

### v0.8.0 (Quiz Question Bank)
- Added `QuizQuestion`, `QuizOption`, `QuestionPack`, `SourceInfo` models
- Added `IQuizQuestionBank` interface
- Added `JsonQuizQuestionBank` implementation with JSON file loading
- Added `QuestionPackValidationException` for validation errors
- Added 15 sample questions in `questions.nl-BE.json`
- Added 24 unit tests for question bank functionality
- 118 total tests passing

### v0.9.0 (Iteration 7 - Quiz Gameplay)
- **Domain Models**:
  - Added `QuizPhase` enum: Question, Answering, Reveal, Scoreboard, Finished
  - Added `QuizGameState` model with full game state tracking
  - Added `QuizOptionState`, `PlayerScoreState` supporting models

- **Engine & Orchestrator**:
  - Added `IQuizGameEngine` interface for pure game logic functions
  - Added `QuizGameEngine` implementation with:
    - `InitializeGame`, `StartNewQuestion`, `StartAnsweringPhase`
    - `SubmitAnswer` (idempotent - first answer wins)
    - `RevealAnswer` (calculates scores), `ShowScoreboard`, `FinishGame`
    - `AllPlayersAnswered`, `HasMoreQuestions`, `IsValidOptionKey` helpers
  - Added `IQuizGameOrchestrator` interface for timer management
  - Added `QuizGameOrchestrator` with:
    - Auto-advancing phase timers (Question: 3s, Answering: 15s, Reveal: 5s, Scoreboard: 5s)
    - Early advance when all players answered
    - `QuizStateUpdated` event broadcasting
    - Concurrent room timer management

- **DTOs**:
  - Added `QuizGameStateDto` (safe for clients - hides correct answer during Answering)
  - Added `QuizOptionDto`, `PlayerAnswerStatusDto`, `PlayerScoreDto`

- **SignalR Hub Methods**:
  - Added `SubmitAnswer(roomCode, playerId, optionKey)` - validates phase and option
  - Added `NextQuestion(roomCode)` - host-triggered advance (optional, auto-advances)
  - Updated `StartGame` to initialize quiz orchestrator

- **Web UI**:
  - Added `quiz-tv.html` - TV view for quiz gameplay:
    - Question display with options grid
    - Timer with warning animation
    - Answer status badges
    - Correct answer highlighting in Reveal phase
    - Scoreboard with positions and deltas
    - Winner celebration on finish
  - Added `quiz-phone.html` - Phone controller for quiz:
    - Large A/B/C/D answer buttons
    - Visual feedback on selection
    - Waiting/Submitted states
    - Result display (correct/incorrect)
    - Personal scoreboard view
  - Updated `game-client.js` with `submitAnswer()`, `nextQuestion()`, `onQuizStateUpdated` callback
  - Updated `tv.html` to navigate to `quiz-tv.html` on game start
  - Updated `join.html` to navigate to `quiz-phone.html` on game start

- **Tests**:
  - Added 19 unit tests for `QuizGameEngine`
  - Added 8 integration tests for quiz gameplay
  - 145 total tests passing

### v0.10.0 (Scoreboard Animations)
- **Animation Module**:
  - Added `scoreboard-animations.js` - centralized animation module
  - `ScoreboardAnimations` class with:
    - FLIP reorder animation when ranks change
    - Score tick-up counter animation (0 ? 100)
    - Delta badge pop/fade animation (+100)
    - Winner highlight pulse glow

- **Dependencies**:
  - Added GSAP 3.12.5 (CDN) for animations
  - Added GSAP Flip plugin for smooth reorder transitions

- **Styling Updates**:
  - Glassy scoreboard rows with backdrop-filter blur
  - Tabular nums for stable score widths
  - Gradient delta badges (green/red)
  - Medal gradients for top 3 positions
  - Winner glow pulse animation

- **Graceful Degradation**:
  - Scoreboard works without animations if GSAP fails to load
  - Console warnings for missing libraries

- **Documentation**:
  - Added Section 13: Scoreboard Animations in MasterSpec.md
  - Animation API documentation
  - Test checklist for animations

### v0.11.0  Iteration 9: Rounds & Category Selection
- STRUCTURE
  - Game now structured as 3 rounds × 3 questions = 9 total
  - Added `RoundType` enum with `CategoryQuiz` type
  - Added `GameRound` model with round lifecycle
  - Added `SelectRoundLeader` method to pick RoundLeader (lowest score rule)
  - Added `GetRandomCategories` to fetch random categories for the round
  - Updated `QuizGameState` and `QuizGameStateDto` with round/category info
  - Updated SignalR hub methods for category selection

- UI
  - TV view shows category selection screen with 3 options
  - Phone view (RoundLeader) shows 3 tappable category buttons
  - Waiting screen for other players during category selection
  - Shows "Watching [LeaderName]" when not leader

- PHASES
  - New `CategorySelection` phase before questions
  - Automatic progression through phases: Question ? Answering ? Reveal ? Scoreboard
  - Loops through 3 questions per round, then progresses rounds or ends game

- TESTS
  - Manual test checklist for round and category selection flow
  - 150 total tests passing

---

## 12. Quiz Game Flow

### 12.1 Phase Sequence

```
StartGame -> Question (3s) -> Answering (15s) -> Reveal (5s) -> Scoreboard (5s) -> [repeat or Finished]
```

### 12.2 QuizPhase Enum

| Phase | Value | Description |
|-------|-------|-------------|
| CategorySelection | 0 | Waiting for round leader to select a category |
| Question | 1 | Display question, players watch TV |
| Answering | 2 | Players can submit answers |
| Reveal | 3 | Show correct answer, calculate scores |
| Scoreboard | 4 | Display current standings |
| Finished | 5 | Game complete, show final results |

### 12.3 QuizGameStateDto

```typescript
interface QuizGameStateDto {
    phase: QuizPhase;
    questionNumber: number;
    totalQuestions: number;
    roundNumber: number;                        // NEW
    questionsInRound: number;                   // NEW (always 3)
    currentQuestionInRound: number;             // NEW (1-3)
    currentCategory?: string;                  // NEW
    roundLeaderPlayerId?: string;              // NEW
    availableCategories?: string[];            // NEW (only in CategorySelection)
    // ...existing properties...
}
```

### 12.4 SignalR Events

| Event | Direction | Payload | When |
|-------|-----------|---------|------|
| `QuizStateUpdated` | Server ? Client | `QuizGameStateDto` | Every phase change, answer submission |
| `SubmitAnswer` | Client ? Server | `roomCode, playerId, optionKey` | Player taps answer button |
| `NextQuestion` | Client ? Server | `roomCode` | Host manually advances (optional) |
| `SelectCategory` | Client ? Server | `roomCode, playerId, category` | Round leader selects a category |

### 12.5 Scoring Rules

| Rule | Points |
|------|--------|
| Correct answer | +100 |
| Wrong answer | +0 |
| No answer | +0 |

### 12.6 Manual Test Checklist

- [ ] Create room on TV, 2+ players join
- [ ] Start game, TV shows category selection
- [ ] Round leader selects a category
- [ ] TV shows first question
- [ ] Phone shows "Watch the screen" during Question phase
- [ ] Phone shows answer buttons during Answering phase
- [ ] Timer counts down on both TV and phone
- [ ] Submitting answer shows "Answer Received" on phone
- [ ] All players answering advances early to Reveal
- [ ] Reveal phase highlights correct answer in green
- [ ] Phone shows ? or ? based on answer
- [ ] Scoreboard shows positions with +100 or +0 deltas
- [ ] After 10 questions, game shows "Finished" with winner
- [ ] Phone shows final position and "Play Again" button
- [ ] TV "Back to Lobby" returns to tv.html

---

## 13. Scoreboard Animations (TV View)

### 13.1 Overview

The TV scoreboard features professional, TV-worthy animations using GSAP + Flip plugin:

- **FLIP Reorder**: Smooth position changes when ranks shift
- **Score Tick-up**: Counter animation from old to new score
- **Delta Badge**: Pop/fade badge showing points gained (+100)
- **Winner Highlight**: Golden pulse glow on round winner

### 13.2 Architecture

```
PartyGame.Web/wwwroot/
??? js/
?   ??? scoreboard-animations.js   # Animation module (ScoreboardAnimations class)
??? quiz-tv.html                   # TV view with GSAP integration
```

### 13.3 Dependencies (CDN)

```html
<!-- GSAP Core + Flip Plugin -->
<script src="https://cdnjs.cloudflare.com/ajax/libs/gsap/3.12.5/gsap.min.js"></script>
<script src="https://cdnjs.cloudflare.com/ajax/libs/gsap/3.12.5/Flip.min.js"></script>
```

### 13.4 ScoreboardAnimations API

```javascript
const animator = new ScoreboardAnimations('#scoreboardList');

// Apply scoreboard update with animations
animator.applyUpdate(scoreboard, {
    animate: true,                    // Enable animations
    highlightWinnerId: 'player-guid'  // Optional: highlight specific player
});

// Reset stored scores (call when starting new game)
animator.reset();
```

### 13.5 DOM Structure (Required)

Each scoreboard row must have:
```html
<li class="scoreboard-item" data-player-id="GUID">
    <div class="scoreboard-position">??</div>
    <div class="scoreboard-player">
        <div class="player-avatar">JD</div>
        <div class="scoreboard-name">John Doe</div>
    </div>
    <div class="score-value" data-score="500">500</div>
</li>
```

### 13.6 Styling Features

- **Glassy rows**: `backdrop-filter: blur()` with subtle borders
- **Tabular nums**: `font-variant-numeric: tabular-nums` for stable score widths
- **Delta badge pill**: Absolute positioned, gradient background
- **Winner glow**: Box-shadow pulse animation
- **Medal gradients**: Gold/Silver/Bronze for top 3

### 13.7 Graceful Degradation

If GSAP/Flip fails to load:
- Scoreboard renders normally without animations
- Console warnings indicate missing libraries
- No JavaScript errors

### 13.8 Performance Notes

- DOM nodes reused via `data-player-id` matching (no thrashing)
- FLIP uses transforms (GPU-accelerated)
- Staggered animations prevent simultaneous repaints
- Tested with 10-12 players smoothly

### 13.9 Animation Test Checklist

- [ ] Start quiz game with 3+ players on TV
- [ ] After first question, scoreboard phase shows
- [ ] **Tick-up**: Scores animate from 0 to new value
- [ ] **Delta badge**: "+100" appears and fades out
- [ ] **Winner glow**: Correct answerer has golden pulse
- [ ] Answer second question, different player correct
- [ ] **FLIP reorder**: Rows smoothly swap positions
- [ ] Verify no console errors
- [ ] Verify smooth 60fps on TV display

---

## 14. Iteration 9  Rounds & Category Selection

### 14.1 Overview

The game is now structured into **rounds**. Each round consists of:
- A **RoundLeader** who selects a category
- Exactly **3 questions** from that category
- Automatic progression to the next round (or game end)

This architecture enables future round types (e.g., Woordenboekspel, Ranking the Stars) without major refactoring.

### 14.2 Domain Models

#### RoundType Enum
```csharp
public enum RoundType
{
    CategoryQuiz  // Current implementation
    // Future: Woordenboekspel, RankingTheStars, etc.
}
```

#### GameRound Model
```csharp
public class GameRound
{
    public Guid RoundId { get; set; }
    public RoundType Type { get; init; } = RoundType.CategoryQuiz;
    public string Category { get; set; } = string.Empty;
    public Guid RoundLeaderPlayerId { get; init; }
    public int CurrentQuestionIndex { get; set; } = 0;  // 0..2
    public bool IsCompleted { get; set; } = false;
    public const int QuestionsPerRound = 3;
}
```

#### QuizGameState Extensions
```csharp
public class QuizGameState
{
    // ...existing properties...
    
    public int RoundNumber { get; set; } = 0;
    public GameRound? CurrentRound { get; set; }
    public List<GameRound> CompletedRounds { get; set; } = new();
    public HashSet<string> UsedCategories { get; set; } = new();
    public List<string> AvailableCategories { get; set; } = new();
    public List<Guid> PreviousRoundLeaders { get; set; } = new();
    public string Locale { get; set; } = "nl-BE";
}
```

### 14.3 Round Lifecycle

```
???????????????????????????????????????????????????????????????
?                    ROUND LIFECYCLE                          ?
???????????????????????????????????????????????????????????????
?                                                             ?
?  StartNewRound()                                            ?
?       ?                                                     ?
?       ?                                                     ?
?  ???????????????????                                        ?
?  ? CategorySelection? ??? RoundLeader selects 1 of 3       ?
?  ?   (30 seconds)   ?     categories on their phone        ?
?  ???????????????????                                        ?
?           ?                                                 ?
?           ?                                                 ?
?  ???????????????????                                        ?
?  ?    Question     ? ??? Loop 3 times (questions 1-3)      ?
?  ?   (3 seconds)   ?                                        ?
?  ???????????????????                                        ?
?           ?                                                 ?
?           ?                                                 ?
?  ???????????????????                                        ?
?  ?    Answering    ?                                        ?
?  ?   (15 seconds)  ?                                        ?
?  ???????????????????                                        ?
?           ?                                                 ?
?           ?                                                 ?
?  ???????????????????                                        ?
?  ?     Reveal      ?                                        ?
?  ?   (5 seconds)   ?                                        ?
?  ???????????????????                                        ?
?           ?                                                 ?
?           ?                                                 ?
?  ???????????????????                                        ?
?  ?   Scoreboard    ?                                        ?
?  ?   (5 seconds)   ?                                        ?
?  ???????????????????                                        ?
?           ?                                                 ?
?           ?                                                 ?
?  ???????????????????????????????????????????????????????????  ?
?  ?  More questions in round?                              ?  ?
?       ?           ?                                         ?
?      Yes          No                                        ?
?       ?           ?                                         ?
?       ?           ?                                         ?
?       ?       MarkRoundComplete()                           ?
?       ?           ?                                         ?
?       ?   ???????????????????????????????????????????????   ?
?       ?   ?  More rounds?                               ?   ?
?       ?       ?       ?                                     ?
?       ?      Yes      No                                    ?
?       ?       ?       ?                                     ?
?       ?       ?       ?                                     ?
?       ?       ?   FinishGame()                           


## 13. Dev Autoplay (Server-side bots)

### 13.1 Overview
- Development-only feature that lets the host spawn server-side bots.
- Bots participate in category selection, quiz answers, dictionary answers, and ranking votes.
- Designed for local testing without real phones or SignalR clients.

### 13.2 Configuration
Autoplay is controlled via `Autoplay` options in `PartyGame.Server` appsettings:

```json
{
  "Autoplay": {
    "Enabled": true,
    "MinBots": 4,
    "MaxBots": 12,
    "PollIntervalMs": 250,
    "MinActionDelayMs": 250,
    "MaxActionDelayMs": 1200,
    "DefaultTimeLimitSeconds": 15
  }
}
```

- `appsettings.Development.json`: `Enabled = true`
- `appsettings.json` (Production default): `Enabled = false`

### 13.3 Host Controls (TV)
- **Add Bots (random)**: Adds a random number of bots to the room.
- **Start Autoplay**: Adds bots if needed and starts the autoplay loop.
- **Stop Autoplay**: Stops the autoplay loop.
- UI is only visible in Development environment.

### 13.4 Manual Test Checklist
1. Start the server in Development.
2. Open `/tv` and create a room.
3. Click **Start Autoplay** in the DEV panel.
4. Start the game and confirm bots answer and phases advance.
5. Click **Stop Autoplay** and verify bots stop acting.
