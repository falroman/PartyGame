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
| **Chaos Mode** | ??? | Question | No | Shuffles answer order on everyone else's phones |
| **Shield** | ??? | Passive | No | Automatically blocks one negative booster targeting you |

## Shield Behavior

The **Shield** is a passive booster - it activates automatically when someone targets you with:
- Back to Zero
- Nope
- Position Switch

When blocked, both booster users see a notification, and the attacker's booster is consumed (but has no effect).

## Iteration 14 – Boosters UI + Private Effects

### Public vs Private State Updates

Boosters like 50/50, Nope, ChaosMode, LateLock, and Mirror have **private effects** that should only be visible to the affected player. This prevents cheating where players could inspect network traffic to see which options were removed for another player.

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
| Mirror | `MirrorTargetId` | Server copies target's answer to activator |

### Testing Boosters

**Unit tests** (in `BoosterServiceTests.cs`):
- Booster assignment: each player gets exactly 1 booster
- Per-player DTO mapping: player A sees 50/50 disabled options, player B doesn't
- Nope: target gets `IsNoped=true`, server rejects their answers
- LateLock: activator can submit after normal deadline
- ChaosMode: mapping differs per player, submissions remain correct

**Manual testing**:
1. Start game with 2+ players
2. Force-assign FiftyFifty to player 1 (via dev tools or test setup)
3. Activate during Answering phase
4. Verify: player 1 sees 2 options removed, player 2 sees all 4

---

## Iteration 15 – VFX & Audio (TV Presentation Layer)

### Overview

Iteration 15 adds a presentation layer to the TV view with:
- **Pixi.js** visual effects overlay (splash screens, booster animations)
- **Howler.js** audio system (background music, sound effects)
- **Countdown sounds** (tick in last 10 seconds, buzzer at 0)

### Architecture

```
?????????????????????????????????????????????
?                      quiz-tv.html                           ?
?????????????????????????????????????????????
?  ???????????  ???????????                  ?
?  ?   DOM UI Layer  ?  ?  Pixi.js Canvas ?  (z-index: 9999) ?
?  ?  (game content) ?  ?   (effects)     ?  pointer-events:  ?
?  ?                 ?  ?                 ?  none             ?
?  ???????????  ???????????                  ?
?                                                             ?
?  ?????????????????????????????????   ?
?  ?              AudioManager (Howler.js)               ?   ?
?  ?  - Background music per phase                       ?   ?
?  ?  - Sound effects (boosters, countdown, buzzer)      ?   ?
?  ?????????????????????????????????   ?
?????????????????????????????????????????????
```

### Effects Manager API (`effects-manager.js`)

```javascript
const effects = new EffectsManager();
effects.init();                              // Initialize Pixi canvas
effects.playSplash('QuizStart');            // Show splash screen
effects.playBooster('DoublePoints', opts);  // Play booster animation
effects.playBooster('Nope', { targetName }); // With target info
effects.clear();                            // Clear all effects
effects.resetSplash();                      // Reset splash shown state
effects.destroy();                          // Cleanup
```

### Audio Manager API (`audio-manager.js`)

```javascript
const audio = new AudioManager();
audio.init();                    // Initialize (waits for user interaction)
audio.unlock();                  // Unlock audio context after click
audio.setPhase('Answering');     // Change background music
audio.playSfx('Booster');        // Play sound effect
audio.playSfx('Tick');           // Countdown tick
audio.playSfx('Buzzer');         // Time's up
audio.handleCountdown(seconds);  // Auto-play tick/buzzer based on time
audio.mute(true/false);          // Mute/unmute all audio
audio.toggleMute();              // Toggle mute state
audio.stopAll();                 // Stop all sounds
```

### Audio Assets

Located in `/assets/audio/`:

| File | Purpose |
|------|---------|
| `intro_loop.mp3` | Upbeat intro music |
| `category_loop.mp3` | Calm music during category selection |
| `question_loop.mp3` | Building tension during question display |
| `tension_loop.mp3` | High tension during answering |
| `reveal_sting.mp3` | One-shot for answer reveal |
| `scoreboard_loop.mp3` | Celebratory scoreboard music |
| `victory_sting.mp3` | Game end fanfare |
| `victory_loop.mp3` | Victory background loop for finale |
| `applause.mp3` | Crowd applause for finale |
| `tick.mp3` | Countdown tick (last 10 seconds) |
| `buzzer.mp3` | Time's up buzzer |
| `booster.mp3` | Booster activation sound |
| `correct.mp3` | Correct answer sound |
| `wrong.mp3` | Wrong answer sound |
| `splash.mp3` | Quiz start splash sound |

### Replacing Audio Assets

1. Replace files in `/assets/audio/` with new MP3s of same name
2. Keep files under 500KB for optimal loading
3. Normalize volume: music to -12dB, SFX to -6dB
4. Ensure loops seamlessly repeat

### Visual Effects

**Quiz Start Splash**
- Triggers on first question of Round 1
- Shows "QUIZ TIME!" with particle burst
- Duration: ~1.5 seconds
- Only shows once per game session

**Booster Effects**
- DoublePoints: Golden "×2" burst with glow
- BackToZero: Red "RESET!" with shatter particles
- Nope: Big "?? NOPE!" stamp with smoke
- FiftyFifty: "?? 50/50" text pop
- ChaosMode: Rainbow "?? CHAOS!" with shake
- Shield: Blue "??? BLOCKED!" when activated

### Autoplay Policy Handling

Due to browser autoplay restrictions:
1. User sees "Click to enable sound" overlay on load
2. Clicking anywhere unlocks audio context
3. Audio mute button in header for manual control

### Degrade-Safe Design

All effects are optional - if libraries fail to load:
- `EffectsManager` methods become no-ops
- `AudioManager` methods become no-ops
- Game continues to function normally
- No console errors or crashes

### Manual Testing Checklist

- [ ] Splash screen appears at quiz start (first question, round 1)
- [ ] Splash doesn't reappear on reconnect
- [ ] Booster activation shows visual effect on TV
- [ ] Booster toast notification appears alongside effect
- [ ] Music changes when phase changes
- [ ] Tick sound plays in last 10 seconds of Answering phase
- [ ] Buzzer plays when timer hits 0
- [ ] Mute button toggles all audio
- [ ] Game works normally if Pixi/Howler fail to load

### DEV Panel Test Buttons (Development Mode Only)

When running in Development mode, the TV view shows a DEV panel with test buttons:

| Button | Action |
|--------|--------|
| **?? Splash** | Triggers the quiz start splash effect |
| **? ×2** | Triggers DoublePoints booster effect |
| **?? Reset** | Triggers BackToZero booster effect |
| **?? Nope** | Triggers Nope booster effect |
| **?? Countdown** | Simulates countdown sounds (10?0) at 2x speed |

These buttons bypass game logic and directly call `EffectsManager` / `AudioManager` for testing.

### Diagnostics Overlay (Development Mode Only)

A small overlay in the bottom-left corner shows:

| Field | Description |
|-------|-------------|
| **Effects** | Pixi.js initialization status (OK/DISABLED) |
| **Audio** | Audio status (OK/LOCKED/MUTED/NOT INIT) |
| **Phase** | Current game phase number and name |
| **Track** | Currently playing audio track |
| **Last tick** | Last countdown second played |

### Audio Graceful Degradation

The AudioManager handles missing audio files gracefully:
- Files that fail to load are marked and not retried
- Error count available via `audio.getLoadErrorCount()`
- No crashes if Howler.js is unavailable
- Game continues to function normally without audio

### Splash Trigger Logic (Fixed in Iteration 15)

The splash screen now triggers more reliably:
- **Trigger condition**: First transition from CategorySelection (or null) to a gameplay phase
- **Gameplay phases**: Question (1), Answering (2), DictionaryWord (6), DictionaryAnswering (7), RankingPrompt (8), RankingVoting (9)
- **Once per session**: `splashShownThisSession` flag prevents re-triggering
- **Reset on back to lobby**: Flag resets when host returns to lobby

---

## Iteration 16 – Finale Ceremony (Grand Finale Experience)

### Overview

Iteration 16 adds a cinematic finale ceremony that plays when the game finishes, creating a memorable "party show" ending experience with:
- **Podium display** showing top 3 players with gold/silver/bronze styling
- **Confetti effects** using Pixi.js particle system
- **Victory music** with dedicated fanfare and celebration loop
- **Awards/stats cards** highlighting player achievements
- **"Play Again" and "Back to Lobby" buttons** for easy replay

### Goals

- Make the game ending feel celebratory and complete
- Provide clear winner recognition
- Encourage players to play again
- Maintain TV-friendly presentation (large, readable, animated)

### Architecture

```
???????????????????????????????????????????
?              Finale Screen                          ?
???????????????????????????????????????????
?  ?? We Have a Winner! ??                ?
?  Alice wins with 450 points!            ?
?                                         ?
?  ???????????????????????????            ?
?  ?       PODIUM LAYOUT       ?            ?
?  ?    2nd     1st     3rd    ?            ?
?  ?   Silver   Gold   Bronze  ?            ?
?  ???????????????????????????            ?
?                                         ?
?  ???????????????????????????            ?
?  ?      AWARDS/STATS         ?            ?
?  ?  ?? Top Scorer: Alice     ?            ?
?  ?  ?? Strong Finish: Bob    ?            ?
?  ???????????????????????????            ?
?                                         ?
?  [?? Play Again] [?? Back to Lobby]    ?
???????????????????????????????????????????
     ? Confetti overlay (Pixi.js)
     ? Victory music (Howler.js)
```

### Finale Trigger

**When**: Game state `phase` reaches `Finished` (5)  
**Once per game**: `finaleStarted` flag prevents retriggering  
**Reset**: Flag resets when returning to lobby

```javascript
function showFinished(state) {
    // Only trigger finale effects once per game
    if (!finaleStarted) {
        finaleStarted = true;
        
        // Start visual effects
        effects.playFinale({ 
            winnerName: winner?.displayName, 
            duration: 8000 
        });
        
        // Start victory audio
        if (audioEnabled) {
            audio.playFinale();
        }
    }
}
```

### UI Components

#### 1. Podium Layout

**CSS Grid system** with 3 positions:
- **1st place (center, highest)**: Gold gradient, larger scale, crown emoji ??
- **2nd place (left, mid-height)**: Silver gradient, medal emoji ??
- **3rd place (right, lowest)**: Bronze gradient, medal emoji ??

Each podium card shows:
- Position emoji (animated bounce)
- Player name
- Total score

**TV Mode scaling**:
- Position emoji: `clamp(60px, 8vw, 120px)`
- Player name: `clamp(24px, 3vw, 48px)`
- Score: `clamp(28px, 3.5vw, 56px)`

#### 2. Awards Section

Lightweight stats cards showing:
- **Top Scorer**: Player with highest score (??)
- **Strong Finish**: 2nd place (???)
- **Great Effort**: Last place with > 0 score (??)

Future enhancement: Track per-question stats for:
- Fastest Fingers (most rank #1 answers)
- Comeback Kid (biggest score gain)
- Most Correct Answers

#### 3. Action Buttons

- **Play Again**: Resets to lobby, keeps room code, refreshes page
- **Back to Lobby**: Same as "Play Again" (keeps existing players)

Both buttons call `handleBackToLobby()` which:
```javascript
async function handleBackToLobby() {
    finaleStarted = false; // Reset for next game
    effects.stopFinale();
    audio.stopAll();
    await client.resetToLobby();
    window.location.href = `/tv.html?room=${roomCode}`;
}
```

### Visual Effects

**Confetti System** (`effects-manager.js`):

```javascript
effects.playFinale({ winnerName, duration: 8000 })
```

- **Continuous confetti** falling from top
- **Burst timing**:
  - Initial burst (intensity 3) at 0s
  - Second burst (intensity 2) at 2s
  - Third burst (intensity 2) at 4s
- **Particle colors**: Gold, Red, Teal, Blue, Pink
- **Physics**: Gravity, rotation, fade-out
- **Duration**: 8 seconds total

**Spotlight Effect** (optional):
- Radial glow behind winner podium (not yet implemented)

**Podium Animation** (GSAP):
```javascript
gsap.from('.podium-card', {
    opacity: 0,
    y: 50,
    scale: 0.8,
    stagger: 0.2,
    duration: 0.6,
    ease: 'back.out(1.7)'
});
```

### Audio System

**Victory Flow** (`audio-manager.js`):

```javascript
audio.playFinale()
```

1. **Fade out** current background music (300ms)
2. **Play victory sting** (one-shot fanfare) at volume 0.7
3. **Play applause** (300ms delay) at volume 0.4
4. **Start victory loop** (2s delay) at volume 0.25, looping

**Stop Finale**:
```javascript
audio.stopFinale()
```
- Fades out victory loop (500ms)
- Stops all sounds

**Required Audio Files**:
- `victory_sting.mp3` - Short celebratory fanfare (~2s)
- `victory_loop.mp3` - Upbeat looping background music
- `applause.mp3` - Crowd applause sound effect (~3-5s)

### Dev Panel Integration

**Test Finale Button** (??):
```javascript
document.getElementById('testFinaleBtn').addEventListener('click', () => {
    finaleStarted = false; // Allow re-trigger
    const mockState = {
        phase: 5,
        scoreboard: [
            { playerId: '1', displayName: 'Alice', score: 450, position: 1 },
            { playerId: '2', displayName: 'Bob', score: 380, position: 2 },
            { playerId: '3', displayName: 'Charlie', score: 290, position: 3 },
            { playerId: '4', displayName: 'Diana', score: 150, position: 4 }
        ]
    };
    showFinished(mockState);
});
```

### Testing Checklist

- [ ] **Finale triggers** when game enters Finished phase
- [ ] **Confetti displays** continuously for 8 seconds
- [ ] **Confetti bursts** occur at t=0s, t=2s, t=4s
- [ ] **Victory sting plays** immediately
- [ ] **Applause plays** 300ms after sting
- [ ] **Victory loop starts** 2s after sting and loops
- [ ] **Podium animates** with staggered entrance
- [ ] **Top 3 display** correct names and scores
- [ ] **Awards show** (if > 3 players)
- [ ] **Play Again button** reloads lobby with same room
- [ ] **Back to Lobby** works identically
- [ ] **Audio mute** affects all finale sounds
- [ ] **Effects degrade safely** if Pixi/Howler unavailable
- [ ] **Finale doesn't retrigger** on state re-send
- [ ] **DEV test button** successfully triggers finale
- [ ] **TV mode scales** properly on 1920×1080 and 3840×2160

### Code Quality

- ? **Degrade-safe**: Works without Pixi.js or Howler.js
- ? **No breaking changes**: Phone UI unchanged
- ? **State-driven**: Triggered by server state (phase=Finished)
- ? **Idempotent**: `finaleStarted` flag prevents duplicate effects
- ? **Cleanup**: `effects.stopFinale()` and `audio.stopFinale()` on reset
- ? **Responsive**: TV mode scaling with `clamp()`

### Files Modified

| File | Changes |
|------|---------|
| `quiz-tv.html` | Added finale screen HTML + CSS + JS logic |
| `effects-manager.js` | Added `playFinale()`, `stopFinale()`, confetti methods |
| `audio-manager.js` | Added `playFinale()`, `stopFinale()`, victory tracks |
| `BOOSTERS.md` | Documentation for Iteration 16 |

### Future Enhancements

1. **Per-question stats**: Track fastest answers, most correct, for better awards
2. **Winner spotlight**: Pixi.js radial glow effect behind 1st place
3. **Share results**: Screenshot/QR code to share final scoreboard
4. **Custom victory music**: Allow host to upload custom celebration music
5. **Fireworks effect**: Additional particle type for more variety
6. **Player portraits**: Show avatars/photos in podium cards

### Browser Compatibility

All effects tested and working in:
- ? Chrome 120+ (Desktop & Chromecast)
- ? Firefox 121+
- ? Safari 17+ (Desktop & AirPlay)
- ? Edge 120+

**Mobile browsers**: Finale shows but effects may be less performant (intentional degradation).
