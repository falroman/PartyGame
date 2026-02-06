# PartyGame Boosters System

## Overview

Each player receives **one random booster** at the start of the game. Boosters are single-use power-ups that can affect the game in various ways.

## How to Test Boosters

1. **Start a Quiz Game**
   - Create a room on `/tv.html`
   - Have 2+ players join via `/join.html`
   - Start the game

2. **View Your Booster**
   - On the phone UI (`quiz-phone.html`), you'll see a colorful booster card below the timer
   - The card shows: icon, name, description, and activation status

3. **Activate Your Booster**
   - The "Use" button is enabled only during valid phases (shown in status)
   - Click "Use" to activate
   - If the booster requires a target, a modal will appear to select another player

4. **Watch the TV**
   - When a booster is activated, a toast notification appears on both phone and TV
   - The notification shows who activated what booster (and on whom)

## Available Boosters

| Booster | Emoji | Phase | Target | Effect |
|---------|-------|-------|--------|--------|
| **Double Points** | ? | CategorySelection | No | Doubles your points for the first question |
| **50/50** | ?? | Answering | No | Removes 2 wrong options from your screen |
| **Back to Zero** | ?? | Reveal/Scoreboard | Yes | Resets target's round points to 0 |
| **Nope** | ?? | Question | Yes | Blocks target from answering next question |
| **Position Switch** | ?? | Reveal | Yes | Steal points if you were wrong & target was right |
| **Late Lock** | ? | Answering | No | Gives you +5 extra seconds to answer |
| **Mirror** | ?? | Answering | Yes | Copies target's answer when they submit |
| **Jury Duty** | ?? | Reveal | Yes | Gives +20 bonus to a correct answerer of your choice |
| **Chaos Mode** | ?? | Question | No | Shuffles answer order on everyone else's phones |
| **Shield** | ??? | Passive | No | Automatically blocks one negative booster targeting you |
| **Wildcard** | ?? | Answering | No | Allows changing your answer once after submitting |
| **Spotlight** | ?? | Reveal | No | Your answer is revealed first with dramatic effect |

## Shield Behavior

The **Shield** is a passive booster - it activates automatically when someone targets you with:
- Back to Zero
- Nope
- Position Switch

When blocked, both booster users see a notification, and the attacker's booster is consumed (but has no effect).

## Iteration 14 – Boosters UI + Private Effects

### Public vs Private State Updates

Boosters like 50/50, Nope, ChaosMode, LateLock, Wildcard, and Mirror have **private effects** that should only be visible to the affected player. This prevents cheating where players could inspect network traffic to see which options were removed for another player.

**Public data** (sent to everyone):
- Phase, question, options, scores, round info
- Active effects summary (e.g., "Player X used 50/50")
- All player booster states (type, isUsed)

**Private data** (sent only to the specific player):
- `MyAnsweringEffects`:
  - `RemovedOptions` - Options hidden by 50/50 (only for activator)
  - `IsNoped` - Whether this player is blocked from answering
  - `ShuffledOptionOrder` - Shuffled order from ChaosMode (for non-activators)
  - `ExtendedDeadline` - Personal deadline from LateLock
  - `MirrorTargetId` - Who is being mirrored
  - `CanChangeAnswer` - Whether Wildcard is active

### Per-Connection Sending

The `QuizGameOrchestrator.BroadcastStateAsync()` method sends:
1. **To the host (TV)**: Public state without any private player data
2. **To each player**: Public state + their personal `MyAnsweringEffects`

This is implemented by iterating through `room.Players.Values` and sending a customized DTO to each player's `ConnectionId`.

### Booster Effect Implementation

| Booster | Effect Field | Server Validation |
|---------|--------------|-------------------|
| 50/50 | `RemovedOptions` | Server rejects answers for removed options |
| Nope | `IsNoped` | Server rejects all answers from noped player |
| ChaosMode | `ShuffledOptionOrder` | Options reordered on client, keys unchanged |
| LateLock | `ExtendedDeadline` | Server accepts late answers until extended deadline |
| Wildcard | `CanChangeAnswer` | Server allows one answer overwrite |
| Mirror | `MirrorTargetId` | Server copies target's answer to activator |

### Testing Boosters

**Unit tests** (in `BoosterServiceTests.cs`):
- Booster assignment: each player gets exactly 1 booster
- Per-player DTO mapping: player A sees 50/50 disabled options, player B doesn't
- Nope: target gets `IsNoped=true`, server rejects their answers
- LateLock: activator can submit after normal deadline
- Wildcard: activator can submit twice, others can't
- ChaosMode: mapping differs per player, submissions remain correct

**Manual testing**:
1. Start game with 2+ players
2. Force-assign FiftyFifty to player 1 (via dev tools or test setup)
3. Activate during Answering phase
4. Verify: player 1 sees 2 options removed, player 2 sees all 4

## Technical Details

### Server-side
- Boosters are assigned in `BoosterService.AssignBoostersAtGameStart()`
- Activation is handled via `GameHub.ActivateBooster()`
- State is tracked in `QuizGameState.PlayerBoosters` and `QuizGameState.ActiveEffects`
- Per-player effects computed by `BoosterService.GetAnsweringEffects()`
- Per-player broadcasting in `QuizGameOrchestrator.BroadcastStateAsync()`

### Client-side
- Phone UI: `quiz-phone.html` - Shows booster card, handles activation, respects `MyAnsweringEffects`
- TV UI: `quiz-tv.html` - Shows toast notifications via `BoosterActivated` event
- GameClient: `game-client.js` - `activateBooster(type, targetId)` method

### DTOs
- `PlayerBoosterStateDto` - Booster info for each player (public)
- `ActiveBoosterEffectDto` - Active effects for current question (public)
- `PlayerAnsweringEffectsDto` - Private effects for one player (per-player)
- `BoosterActivatedEventDto` - Event broadcast when booster is used

### Timer Handling
Players with LateLock active receive a personalized `PhaseEndsUtc` in their DTO, allowing their phone timer to show the extended time.

---

*Last Updated: Iteration 14 - Boosters fully playable with private effects*
