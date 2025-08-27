<!--
  ArchitectureOverview.md
  -----------------------
  High-level description of the major systems composing Cave-Runner.
 -->

# Architecture Overview

This document summarizes the major systems that power **Cave-Runner** and how they interact. Each manager encapsulates a specific responsibility so gameplay, persistence, and the user interface remain decoupled.

## Manager Responsibilities

### InputManager
- Normalizes keyboard, gamepad and touch input into a unified API.
- Exposes high‑level queries such as `GetHorizontal`, `JumpPressed` and `PausePressed`.
- Saves user bindings to `PlayerPrefs` and disposes `InputAction` objects on shutdown to avoid leaks.

### GameManager
- Singleton that drives the endless‑runner loop.
- Listens to `InputManager` for player commands and updates distance, speed and coin totals each frame.
- Emits `OnStageUnlocked`, `OnGameOver` and similar events that other systems subscribe to.
- Notifies `SaveGameManager` when coins or options change and pushes score/state updates to `UIManager`.

### StageManager
- Subscribes to `GameManager.OnStageUnlocked`.
- Loads background sprites and spawner prefabs asynchronously using Addressables.
- Applies stage‑specific spawn probabilities, gravity multipliers and speed modifiers.

### SaveGameManager
- Serializes coins, upgrades and options to a JSON file under `Application.persistentDataPath`.
- Wraps saves with a SHA‑256 checksum and optional AES encryption for tamper detection.
- Queues asynchronous write requests and exposes an `Initialization` task so callers can await load completion.

### UIManager
- Controls start, pause, game‑over, settings and shop menus plus the heads‑up display.
- Displays loading and network‑activity spinners for asynchronous operations.
- Reads values from `GameManager` and `SaveGameManager` to populate labels and progress bars.
- Exposes a singleton for other managers to show or hide UI elements.

## Detailed Event Flow

```text
[Input Devices]
      │
      ▼
InputManager --(Jump/Slide/Pause events)--> GameManager
      │                                       │
      │                                       ├─ updates distance and coins
      │                                       ├─ raises OnStageUnlocked ──┐
      │                                       │                          ▼
      │                                       │                    StageManager
      │                                       │                          │
      │                                       ├─ notifies SaveGameManager ──> Disk
      │                                       │
      │                                       └─ updates UIManager ──> HUD & menus
      ▼
Player Controller
```

1. **InputManager** captures raw device input and exposes high‑level events.
2. **GameManager** reacts to those events, advancing gameplay and unlocking stages.
3. **StageManager** loads new assets and adjusts spawner settings when a stage unlocks.
4. **UIManager** refreshes labels, progress bars and menus based on the latest game state.
5. **SaveGameManager** queues an asynchronous save whenever coins, upgrades or options change.

## Dependency Hierarchy

- `GameManager`
  - requires `InputManager` for player actions.
  - drives `StageManager` and `UIManager` via events.
  - calls into `SaveGameManager` to persist progress.
- `InputManager`
  - operates independently and can be reused by tools or tests.
- `StageManager` and `UIManager`
  - depend on `GameManager` signals but are otherwise decoupled.
- `SaveGameManager`
  - independent of other managers; other systems invoke it when persistence is needed.

## Initialization Sequence

1. **SaveGameManager** begins loading persisted data in `Awake` and exposes an `Initialization` task for callers to await.
2. **GameManager** awaits `SaveGameManager.Initialization` to ensure coins, upgrades, and options are ready, then validates required references, registers for input and stage events, and sets baseline speed.
3. **StageManager** subscribes to `GameManager.OnStageUnlocked` and applies the initial stage in `Start`.
4. **UIManager** configures its singleton instance, loads optional mobile UI, locates the `ParallaxBackground`, and pulls values from `GameManager` and `SaveGameManager` to populate menus and HUD elements.
5. When the player presses Start, `GameManager.StartGame` triggers the run and other managers react accordingly.

## Runtime Updates

1. **GameManager** processes player input and advances distance, speed, and coin totals each frame.
2. Updated values are pushed to **UIManager** so HUD labels and progress bars reflect the current game state.
3. When coins or options change, **GameManager** notifies **SaveGameManager** to queue an asynchronous save, preserving progress without blocking gameplay.

## Game Over Handling

1. **GameManager** computes the final score and raises an `OnGameOver` event.
2. **UIManager** displays the game-over panel with distance, coins, and high-score information.
3. **SaveGameManager** serializes the latest state to disk so upgrades and scores persist for the next session.

## Communication Channels

- **Events:** `GameManager` publishes `OnGameOver`, `OnStageUnlocked`, and other events that `UIManager` or `SaveGameManager` subscribe to.
- **Direct Calls:** `GameManager` directly invokes save and UI methods when immediate action is required (e.g., updating HUD labels).
- **Data Sharing:** `UIManager` reads public properties on `GameManager` and `SaveGameManager` to render real-time information.

## Edge Cases and Error Handling

- If **SaveGameManager** fails to load data, it raises an error that **UIManager** can surface, while **GameManager** continues with default values.
- Save failures trigger warnings so players know progress might not persist.

This separation of concerns keeps the codebase modular and testable while making it clear where new features should integrate.

## Steam Achievements

Cave-Runner integrates with Steamworks.NET to grant achievements for notable
milestones. Achievements are identified by string IDs defined in
`GameManager` and mapped to localized names and descriptions in
`SteamManager`.

```csharp
// GameManager.cs – unique identifiers for each achievement
private const string AchDistance1000 = "ACH_DISTANCE_1000";
private const string AchCoins200   = "ACH_COINS_200";
```

```csharp
// SteamManager.cs – localization key mapping
private static readonly Dictionary<string, string> achievementNameKeys = new()
{
    { "ACH_DISTANCE_1000", "ach_distance_1000_name" },
    { "ACH_COINS_200",    "ach_coins_200_name" }
};
```

To add a new achievement:

1. Create the achievement in the Steamworks dashboard and record its ID.
2. Add a constant for the ID in `GameManager`.
3. Register the ID with appropriate localization keys in `SteamManager`'s
   `achievementNameKeys` and `achievementDescKeys` dictionaries.
4. Invoke `SteamManager.Instance.UnlockAchievement(id)` when gameplay logic
   requires it.

For guidance on presenting achievements within the UI and wiring them to
localization, see [`AchievementsMenu.md`](AchievementsMenu.md).

## Leaderboard Services

### Invocation points

End-of-run score uploads occur in `GameManager`. If Steam is unavailable, the
fallback `LeaderboardClient` posts to a REST service. `UIManager` retrieves the
current leaderboard for display.

```csharp
// GameManager.cs – upload final score
if (LeaderboardClient.Instance != null)
{
    StartCoroutine(LeaderboardClient.Instance.UploadScore(finalScore));
}

// UIManager.cs – download and present scores
StartCoroutine(leaderboardClient.GetTopScores(DisplayScores));
```

### Swapping or extending services

`LeaderboardClient` centralizes REST calls and exposes a virtual
`SendWebRequest` method. Subclass it to integrate alternate backends or
network stacks.

```csharp
public class CustomLeaderboardClient : LeaderboardClient
{
    protected override IEnumerator SendWebRequest(UnityWebRequest req,
        System.Action<bool, string, ErrorCode> cb)
    {
        // Replace this with calls to another service
        yield return base.SendWebRequest(req, cb);
    }
}
```

Attach the subclass in place of the default component or implement an entirely
new client exposing the same public API to swap out services.
