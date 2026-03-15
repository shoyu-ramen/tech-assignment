# Hijack Poker — Unity Game Client (Option D)

## Why Option D

I chose the Unity Game Client because it offered the most interesting rendering and state management challenges: building a real-time poker table viewer with animated card dealing, pot distribution, and winner presentation — all driven by a WebSocket event stream. Unity's programmatic UI approach also let me demonstrate strong software design without relying on scene files or visual editors.

## Setup Instructions

### Prerequisites

- **Unity 6** (6000.0+) — [Download](https://unity.com/releases/editor/archive)
- **Docker Desktop** with Docker Compose v2

### 1. Start the backend

```bash
cp .env.example .env
docker compose --profile engine up -d
```

Verify the backend is running:

```bash
curl http://localhost:3030/health
# → {"service":"holdem-processor","status":"ok","timestamp":"..."}
```

### 2. Open the Unity project

1. Open Unity Hub → Add project from disk → select `unity-client/`
2. Open the project in Unity 6
3. Press **Play** — the client bootstraps automatically via `[RuntimeInitializeOnLoadMethod]`

No scene setup required. `GameManager` creates the entire UI hierarchy programmatically.

### 3. Run tests

1. **Window > General > Test Runner**
2. Select the **EditMode** tab
3. Click **Run All**

## What's Implemented vs. Deferred

| Feature | Status | Notes |
|---------|--------|-------|
| Real-time hand viewer | Implemented | Full state machine playback for all 16 hand steps |
| WebSocket connection | Implemented | Primary transport with auto-reconnect |
| REST fallback | Implemented | Falls back to polling when WebSocket is unavailable |
| Animated card dealing | Implemented | Clockwise deal from center deck position |
| Card flip animations | Implemented | 3D-style flip with scale tweening |
| Pot distribution animation | Implemented | Chips fly from pot to winner stacks |
| Shuffle/reset animation | Implemented | End-of-hand sweep and visual shuffle |
| Winner glow + presentation | Implemented | Pulsing glow ring on winner seats |
| Community card reveal | Implemented | Staged reveal for flop (3), turn, river |
| HUD (phase, pot, blinds) | Implemented | Shimmering pot total, hand number, blind levels |
| Hand history log | Implemented | Collapsible scrollable step-by-step log |
| Auto-play mode | Implemented | Configurable speed (1-5x), continuous hand progression |
| Procedural audio | Implemented | Runtime-generated SFX (deal, chip, fold, win) |
| Connection status indicator | Implemented | Colored dot with state text |
| Table switching | Implemented | Text input to change table ID at runtime |
| Multi-platform builds | Implemented | macOS, WebGL, iOS build pipelines |
| Safe area support | Implemented | iOS notch/home indicator insets |
| Responsive layout | Implemented | Grid-based seat layout adapts to window size |
| Mute toggle | Implemented | Toggle all procedural audio |
| Card art sprites | Deferred | Cards display rank + suit as text |
| Rounded corners | Deferred | uGUI Image renders as rectangles |
| Player avatars | Deferred | Text-only player names |
| Persistence | Deferred | Settings reset each session (no PlayerPrefs) |
| Play Mode tests | Deferred | Would require MonoBehaviour lifecycle mocking |

## Architecture

### Data Flow

```
POST /process → WebSocket delivers state (or REST fallback)
                    ↓
            ConnectionManager
                    ↓
            TableStateManager.UpdateState()
                    ↓
            OnStateChanged event fires
                    ↓
    ┌───────────────┼───────────────┐
    ↓               ↓               ↓
 SeatViews    CommunityCards     HudView
 CardViews     HandHistory
```

### Key Architectural Decisions

| Decision | Rationale |
|----------|-----------|
| **Programmatic UI (no scene files)** | Scene YAML is impossible to merge in git. All layout logic lives in diffable `.cs` files. No hidden inspector state — every color, size, and position is visible in code. |
| **Singleton bootstrap via `[RuntimeInitializeOnLoadMethod]`** | Any scene works — no scene dependencies, no prefab wiring. Single entry point with `DontDestroyOnLoad`. |
| **Event-driven state (`TableStateManager`)** | Decouples data from rendering. Views subscribe to `OnStateChanged` and update independently. |
| **WebSocket primary, REST fallback** | WebSocket gives sub-100ms state delivery. If WS fails, the client transparently degrades to REST polling with no user action required. |
| **Cancel-snap animation pattern** | When users click "Next Step" mid-animation, all active tweens snap to their final values instantly, then the new step processes. No visual artifacts from interrupted animations. |
| **Procedural audio** | Zero external dependencies — SFX clips are generated at runtime via `AudioClip.Create` with synthesized waveforms. |
| **Deferred stack tweens** | Winner stack amounts update only after the pot fly-in animation lands, creating a causal visual sequence. |
| **async/await with `Awaitable`** | Unity 6 native async support. API calls, auto-play timing, and reconnection all use `Task`-based patterns. |

### Connection Management

1. Startup: health check with exponential backoff (1s → 2s → 4s → 8s)
2. WebSocket connect to `ws://localhost:3032`
3. If WS fails: REST-only mode via `GET /table/{tableId}`
4. After `POST /process`: wait up to 3s for WS state delivery, then fall back to REST
5. On WS disconnect: switch to REST, attempt WS reconnect in background

### Project Structure

```
Assets/Scripts/
├── Api/
│   ├── PokerApiClient.cs          REST client (UnityWebRequest)
│   ├── WebSocketClient.cs         WebSocket client (System.Net.WebSockets)
│   ├── WebGLWebSocketClient.cs    WebGL-specific WS via JS interop
│   └── ServerConfig.cs            Centralized server URL configuration
├── Animation/
│   ├── AnimationController.cs     Active tween tracker, CancelAll()
│   ├── Tweener.cs                 Static tween factories (float, color, position, flip, pulse)
│   ├── DealAnimator.cs            Clockwise card deal from center deck
│   ├── ShuffleAnimator.cs         End-of-hand sweep + shuffle visual
│   ├── PotDistributionAnimator.cs Pot fly-in to winner stacks
│   ├── SeatCardAnimator.cs        Per-seat card animation controller
│   └── SeatGlowController.cs      Winner glow pulse ring
├── Managers/
│   ├── GameManager.cs             Singleton bootstrap, orchestrator
│   ├── TableStateManager.cs       State holder, OnStateChanged event
│   ├── ConnectionManager.cs       WS/REST lifecycle, reconnection
│   ├── AutoPlayManager.cs         Timed auto-advance loop
│   └── AudioManager.cs            Procedural SFX generation and playback
├── Models/
│   ├── GameState.cs               Game data + API response DTOs
│   └── PlayerState.cs             Player data + status convenience properties
├── UI/
│   ├── UIFactory.cs               Static UI creation helpers, color palette
│   ├── TableView.cs               Background + oval felt surface
│   ├── SeatView.cs                Player seat with all sub-elements
│   ├── CardView.cs                Card rendering + flip animation
│   ├── BetChipView.cs             Bet amount chip display
│   ├── SeatBadgeView.cs           Action/status badge overlay
│   ├── CommunityCardsView.cs      5 center card slots with reveals
│   ├── HudView.cs                 Phase label, pot, hand #, blinds
│   ├── ControlsView.cs            Buttons, auto-play, speed, mute
│   ├── ConnectionStatusView.cs    Colored dot + state text
│   ├── HandHistoryView.cs         Scrollable step-by-step log
│   ├── TextureGenerator.cs        Runtime texture/sprite creation
│   ├── InputHandler.cs            Keyboard shortcut handler
│   ├── LayoutConfig.cs            Responsive layout constants
│   └── SafeAreaPanel.cs           iOS safe area inset handler
└── Utils/
    ├── CardUtils.cs               Parse card strings → rank, suit, symbol, color
    ├── MoneyFormatter.cs          $X,XXX.XX formatting
    ├── ShowdownLogic.cs           Card visibility rules (pure static)
    ├── PhaseLabels.cs             Step number → human-readable label
    └── MockStateFactory.cs        Test data factory

Assets/Tests/EditMode/
├── CardUtilsTests.cs
├── MoneyFormatterTests.cs
├── ShowdownLogicTests.cs
├── PhaseLabelsTests.cs
├── GameStateTests.cs
├── PlayerStateTests.cs
├── TableStateManagerTests.cs
├── ApiClientTests.cs
├── AnimationControllerTests.cs
├── TweenHandleTests.cs
├── TextureGeneratorTests.cs
└── StateIntegrationTests.cs
```

## API Documentation

The client consumes 3 REST endpoints and 1 WebSocket stream from `holdem-processor`.

### REST Endpoints

#### `GET /health`

Health check. Returns 200 when the service is ready.

```json
{
  "service": "holdem-processor",
  "status": "ok",
  "timestamp": "2024-01-15T10:30:00.000Z"
}
```

#### `POST /process`

Advances the hand by one state machine step.

**Request:**
```json
{ "tableId": 1 }
```

**Response:**
```json
{
  "success": true,
  "result": {
    "status": "ok",
    "tableId": 1,
    "step": 5,
    "stepName": "PRE_FLOP_BETTING"
  }
}
```

#### `GET /table/{tableId}`

Returns the full table state (game + players).

**Response:**
```json
{
  "game": {
    "id": 1,
    "tableId": 1,
    "tableName": "Table 1",
    "gameNo": 42,
    "handStep": 7,
    "stepName": "FLOP_BETTING",
    "dealerSeat": 1,
    "smallBlindSeat": 2,
    "bigBlindSeat": 3,
    "communityCards": ["JH", "7D", "2C"],
    "pot": 24.0,
    "sidePots": [],
    "move": 4,
    "status": "in_progress",
    "smallBlind": 1.0,
    "bigBlind": 2.0,
    "maxSeats": 6,
    "currentBet": 4.0,
    "winners": []
  },
  "players": [
    {
      "playerId": 101,
      "username": "Alice",
      "seat": 1,
      "stack": 96.0,
      "bet": 4.0,
      "totalBet": 6.0,
      "status": "1",
      "action": "raise",
      "cards": ["AH", "KD"],
      "handRank": "",
      "winnings": 0
    }
  ]
}
```

### WebSocket Stream

**URL:** `ws://localhost:3032`

The WebSocket broadcasts `TableResponse` JSON (same schema as `GET /table/{tableId}`) whenever the game state changes. The client subscribes on connect and receives push updates after each `POST /process` call.

No client-to-server messages are required — the WebSocket is receive-only.

## Testing

### Unit Tests (EditMode)

Run via **Window > General > Test Runner > EditMode > Run All**.

| Test File | Coverage |
|-----------|----------|
| `CardUtilsTests` | Card parsing, all ranks/suits, invalid input, symbols, colors |
| `MoneyFormatterTests` | Formatting, thousands separators, negatives, zero, large numbers |
| `ShowdownLogicTests` | Card visibility rules across all statuses, steps, and winnings |
| `PhaseLabelsTests` | All 16 step labels, out-of-range fallback |
| `GameStateTests` | IsShowdown/IsHandComplete properties, JSON deserialization, side pots |
| `PlayerStateTests` | Convenience properties, all status codes, JSON deserialization |
| `TableStateManagerTests` | State updates, event firing, clear, multiple subscribers |
| `ApiClientTests` | Request construction, URL building |
| `AnimationControllerTests` | Tween tracking, cancel-all, snap-to-final |
| `TweenHandleTests` | Completion, cancellation, double-cancel safety |
| `TextureGeneratorTests` | Runtime texture creation |
| `StateIntegrationTests` | Full hand progression, mixed table visibility, rapid updates, hand transitions |

### What's Tested vs. Not

| Covered (EditMode) | Not Covered (requires PlayMode) |
|--------------------|---------------------------------|
| All pure C# logic | MonoBehaviour lifecycle |
| State transitions | Visual rendering |
| Event flow | Animation visuals |
| Data formatting | WebSocket I/O |
| Card visibility rules | UI layout |

## Known Limitations

- **No rounded corners** — uGUI `Image` renders rectangles; border effects are approximated with color insets
- **Procedural audio** — synthesized sine/noise waves are functional but not production-quality
- **No card art** — cards display rank and suit as text
- **Single AudioSource** — overlapping SFX during rapid auto-play may blend
- **No persistence** — settings reset each session
