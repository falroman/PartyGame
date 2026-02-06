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

---

## Iteration 15 – VFX & Audio (TV Presentation Layer)

### Overview

Iteration 15 adds a presentation layer to the TV view with:
- **Pixi.js** visual effects overlay (splash screens, booster animations)
- **Howler.js** audio system (background music, sound effects)
- **Countdown sounds** (tick in last 10 seconds, buzzer at 0)

### Architecture

```
???????????????????????????????????????????????????????????????
?                      quiz-tv.html                           ?
???????????????????????????????????????????????????????????????
?  ???????????????????  ???????????????????                  ?
?  ?   DOM UI Layer  ?  ?  Pixi.js Canvas ?  (z-index: 9999) ?
?  ?  (game content) ?  ?   (effects)     ?  pointer-events:  ?
?  ?                 ?  ?                 ?  none             ?
?  ???????????????????  ???????????????????                  ?
?                                                             ?
?  ???????????????????????????????????????????????????????   ?
?  ?              AudioManager (Howler.js)               ?   ?
?  ?  - Background music per phase                       ?   ?
?  ?  - Sound effects (boosters, countdown, buzzer)      ?   ?
?  ???????????????????????????????????????????????????????   ?
???????????????????????????????????????????????????????????????
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

## Iteration 16 – Responsive TV Mode

### Overview

The host (TV) UI is now fully responsive, automatically scaling for:
- Laptop displays (1366×768)
- Desktop monitors (1920×1080)
- Large TVs (4K: 3840×2160)
- Everything in between

### TV Mode Detection

TV mode is automatically activated when `window.innerWidth >= 1200px`:

```javascript
function updateTvMode() {
    const isTvMode = window.innerWidth >= 1200;
    document.documentElement.classList.toggle('tv-mode', isTvMode);
}
window.addEventListener('resize', updateTvMode);
updateTvMode();
```

No user toggle is provided - detection is fully automatic.

### CSS Variables System

**Key insight**: Using `min(vw, vh)` ensures proportional scaling on widescreen displays where width-only scaling would make elements too wide but not tall enough.

**Base values (mobile/laptop)** - Fixed pixel values:
```css
:root {
    --font-base: 16px;
    --gap: 12px;
    /* etc. */
}
```

**TV Mode values** - Scale with `min(vw, vh * aspect)`:
```css
.tv-mode {
    /* Typography - scales with SMALLER of width or height */
    --font-xs: clamp(14px, min(1vw, 1.8vh), 18px);
    --font-sm: clamp(16px, min(1.2vw, 2.2vh), 22px);
    --font-base: clamp(18px, min(1.4vw, 2.5vh), 26px);
    --font-lg: clamp(22px, min(1.8vw, 3.2vh), 36px);
    --font-xl: clamp(28px, min(2.4vw, 4.3vh), 52px);
    --font-2xl: clamp(36px, min(3vw, 5.4vh), 72px);
    --font-3xl: clamp(48px, min(4vw, 7.2vh), 96px);
    --font-4xl: clamp(64px, min(5vw, 9vh), 120px);
    
    /* Spacing - scales proportionally */
    --gap-xs: clamp(6px, min(0.5vw, 0.9vh), 12px);
    --gap-sm: clamp(10px, min(0.8vw, 1.4vh), 20px);
    --gap: clamp(14px, min(1.2vw, 2.2vh), 28px);
    --gap-lg: clamp(20px, min(1.6vw, 2.9vh), 40px);
    --gap-xl: clamp(28px, min(2.4vw, 4.3vh), 56px);
    
    /* TV Safe Margins */
    --tv-safe-margin: clamp(24px, min(3vw, 5vh), 80px);
}
```

### Why `min(vw, vh)` instead of just `vw`?

On a 16:9 TV (1920×1080), using `vw` alone causes:
- Elements become too wide
- Vertical space is wasted
- Text doesn't fit proportionally

Using `min(vw, vh * 1.78)` ensures:
- Elements scale proportionally in both dimensions
- Content fills the screen better
- Readable from across the room

### Why `clamp()` Over `transform: scale()`

We use `clamp()` instead of `transform: scale()` because:

1. **Sharp fonts**: Text remains pixel-perfect at any size
2. **Correct hitboxes**: Click/touch targets match visual size
3. **CSS Grid compatibility**: Layout calculations work correctly
4. **Performance**: No composite layer overhead
5. **Accessibility**: Browser zoom works as expected

### TV Safe Margins

Content doesn't touch screen edges on TV displays:

```css
.tv-mode body {
    padding: var(--tv-safe-margin);
    min-height: 100vh;
    box-sizing: border-box;
}

/* Remove double padding from nested containers */
.tv-mode .tv-layout,
.tv-mode .quiz-container {
    padding: 0;
    min-height: calc(100vh - var(--tv-safe-margin) * 2);
}
```

This ensures:
- QR codes are fully visible
- Room codes aren't cut off
- Touch targets aren't at extreme edges
- Content doesn't "stick" to the top of the screen

### Files Updated

| File | Changes |
|------|---------|
| `css/styles.css` | Added CSS variables with `min(vw, vh)`, TV mode classes |
| `tv.html` | Added `updateTvMode()` JS, base pixel values for mobile |
| `quiz-tv.html` | Added `updateTvMode()` JS, TV mode overrides |

### Testing Checklist

- [ ] **Laptop 1366×768**: Layout normal, fonts readable (base pixel values)
- [ ] **Desktop 1920×1080**: TV mode active, proportional scaling
- [ ] **4K 3840×2160**: Everything scales proportionally in both dimensions
- [ ] **Ultrawide monitor**: Content doesn't become too wide
- [ ] **Window resize**: TV mode toggles at 1200px threshold
- [ ] **Phone UI unchanged**: `quiz-phone.html` and `join.html` still work
- [ ] **TV safe margins**: Content doesn't touch screen edges
- [ ] **Room code readable**: Large and clear on all sizes
- [ ] **Scoreboard readable**: From across the room on TV

### Guidelines for New Components

When adding new UI components, follow these rules:

1. **Use fixed pixel values** for base (mobile/laptop) styles
2. **Add `.tv-mode` overrides** that use CSS variables
3. **Variables use `min(vw, vh)`** for proportional scaling
4. **Always use `clamp(min, preferred, max)`** to set bounds
5. **Test at 1366px, 1920px, and 3840px** widths
6. **Minimum touch target**: 44px on mobile, larger on TV

### Browser Support

`min()` and `clamp()` are supported in:
- Chrome 79+
- Firefox 75+
- Safari 13.1+
- Edge 79+

All modern browsers used for Chromecast/AirPlay support these features.
