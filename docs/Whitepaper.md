# Cave-Runner Whitepaper

## 1. Introduction
Cave-Runner is a 2D endless runner game built with **Unity 2022.3 LTS**. The project aims to provide a polished Jetpack Joyride–style experience with responsive controls, rich power-ups, and cross‑platform support. The repository contains C# scripts, basic project settings, and a suite of unit tests that can be imported into a new Unity project.

This whitepaper summarizes the design goals, gameplay systems, technical architecture, and future directions for Cave-Runner. It consolidates information from the README and documentation folder, offering a single reference for developers and stakeholders. **The whitepaper is informational only** – features may evolve over time and nothing here constitutes a binding roadmap or guarantee of future releases. The game targets Windows, macOS, Linux, iOS, and Android, though platform availability ultimately depends on build resources and publishing constraints.

This document does not supersede the repository's license or changelog. It is intended purely for planning and discussion. Contributors should consult the issue tracker for up-to-date priorities and the [`LICENSE`](../LICENSE) file for usage terms.

## 2. Goals and Motivation
The primary goals behind Cave-Runner are:

- **Fluid controls**: Tight jump, slide, and dash mechanics that feel responsive on both keyboard and controllers.
- **Replayability**: Endless procedural stages with increasing difficulty, varied hazards, and collectible power‑ups.
- **Accessibility**: Support for mobile touch controls, colorblind mode, and remappable inputs so anyone can play.
- **Extensibility**: Scriptable objects and modular managers allow new stages, enemies, and power‑ups to be added without code changes.
- **Analytics driven balance**: A lightweight analytics system feeds the adaptive difficulty manager and provides insight into player behavior.

## 3. Gameplay Overview
Players control an explorer running through a side‑scrolling cave. The goal is to travel as far as possible while avoiding obstacles and hazards. Distance traveled contributes to the score along with coins collected along the way. Progress unlocks new stages with unique backgrounds and enemy combinations. The shop allows coins to be spent on permanent upgrades that extend power‑up durations and award bonus coins.

### 3.1 Movement
The core movement options include:

- **Jumping and double jump** with variable height and short grace periods (coyote time and jump buffering).
- **Sliding** to duck under obstacles. Sliding mid‑air triggers a downward dive. Players can cancel the slide early for added control.
- **Fast fall** by holding the down input to descend faster and line up jumps precisely.
- **Air dash** when sliding with horizontal input. Dashes have a short cooldown and help players cross large gaps.

Detailed parameters such as gravity multipliers, buffer times, and dash forces are exposed in `PlayerController` so designers can tune the exact feel.

### 3.2 Power-Ups
Temporary power-ups spawn at intervals and include:

- **Magnet** that attracts nearby coins.
- **Speed Boost** for faster movement.
- **Shield** that absorbs one hit.
- **Coin Bonus** that multiplies coin value. Durations stack when multiple are collected.
- **Double Jump** and **Gravity Flip** as optional power-ups included in the scripts.

Upgrades purchased in the shop extend the duration of these effects or increase coin rewards.

### 3.3 Stages and Difficulty
The game world is split into stages defined by `StageDataSO` assets. Each stage specifies the background art, available obstacles, hazard probabilities, spawn multipliers, and music. `GameManager.stageGoals` defines the distance milestones that unlock subsequent stages. 

Difficulty ramps up over time through two mechanisms:
1. **Stage progression** introduces new hazards with higher base spawn rates.
2. **AdaptiveDifficultyManager** monitors recent run distances via `AnalyticsManager` and adjusts spawner multipliers so experienced players encounter a faster pace.

### 3.4 Controls
Cave-Runner supports both keyboard and controller inputs out of the box. Control mappings can be customized using the options described in [InputBindings](InputBindings.md). Touch controls are available on mobile builds via `TouchInputManager`.

### 3.5 Scoring and Progression
Distance traveled and coins collected contribute to the player’s final score. Longer runs unlock additional stages and fill progress bars for achievements. Coin totals persist across sessions and are spent in the shop to purchase upgrades or cosmetic skins.

## 4. Technical Architecture
The project is organized under `Assets/Scripts` with a focus on modularity and reuse. Key components include:

### 4.1 GameManager
Central coordinator that tracks score, speed, and stage progression. It raises events such as `OnGameStart`, `OnGameOver`, and `OnStageUnlocked` so other systems can react. It also handles saving high scores through `SaveGameManager`.

### 4.2 PlayerController
Implements the movement mechanics described earlier. Gravity scaling, input buffering, and dash logic reside here. The controller also communicates with the `CoinMagnet` component when magnet power-ups are active.

### 4.3 Spawners and Object Pooling
`ObstacleSpawner`, `HazardSpawner`, `CoinSpawner`, and `PowerUpSpawner` generate scrolling content just outside the camera’s right edge. Objects are recycled via `ObjectPool` to avoid frequent allocations. `Scroller` moves spawned objects leftward, giving the illusion of forward motion.

### 4.4 Power-Up System
Each power-up derives from a common base and implements `Activate` and `Deactivate` routines. `PowerUpSpawner` chooses prefabs from a weighted list. Effects such as magnet range or speed multiplier are configurable through serialized fields. A small `CoinBonusIndicator` UI element can show timers for active bonuses.

### 4.5 SaveGameManager and Shop
Player progress is serialized to `savegame.json` in `Application.persistentDataPath`. The manager migrates older PlayerPrefs data on first run. `ShopManager` consumes these values to allow persistent upgrades between sessions.

### 4.6 Input and UI
`InputManager` abstracts input sources. It supports both Unity’s new Input System and the older `KeyCode` approach, falling back automatically when the package is missing. Touch controls are provided via `MobileUI` and `TouchInputManager` for smartphone builds.

`UIManager` presents the start menu, pause menu, game over screen, leaderboard panel, and settings. UI transitions are handled by `PanelAnimator`.

### 4.7 Analytics and Adaptive Difficulty
`AnalyticsManager` logs distance traveled, coins collected, and death causes. Data can optionally be posted to a remote server. `AdaptiveDifficultyManager` queries recent analytics at the start of each run and nudges spawner multipliers up or down to keep players challenged.

### 4.8 Achievements, Leaderboards, and Workshop
Integration with **Steamworks.NET** enables Steam achievements and cloud saves. `SteamManager` exposes convenience methods to unlock achievements and submit scores to Steam leaderboards. For non-Steam builds, `LeaderboardClient` can post scores to a simple REST service.

`WorkshopManager` lets players upload or download level and skin packs via the Steam Workshop API. Packs are stored in a user folder and loaded at runtime.

### 4.9 Asset Pipeline
All sprites, audio clips, and prefabs live under `Assets/` in clearly labeled folders. Artists follow a strict naming scheme (`<Category>_<Variant>_v###`) so automated import scripts can set packing tags, compression settings, and track revisions. A pre-build step runs `SpriteAtlasGenerator.cs` to collect sprites into atlases and normalizes audio via `AudioBankBuilder.cs`. While the project runs without Addressables, enabling the package allows remote content delivery and incremental updates for mobile releases.

### 4.10 Build and Deployment
`BuildAll.cs` reads the semantic version from `ProjectSettings/version.txt` and sets `PlayerSettings.bundleVersion` before invoking `UnityEditor.BuildPipeline`. Output is placed under `Builds/<version>/<platform>` and zipped as `Cave-Runner-<version>-<platform>.zip`. The CI workflow installs the required Unity editor, triggers these scripts when tests succeed, and uploads the artifacts. Desktop builds target Windows, macOS, and Linux, while mobile builds produce Android APKs or iOS archives. Completed artifacts can be uploaded to a private server or directly to Steam for playtesting.

## 5. Testing and Continuous Integration
A robust set of **EditMode** tests lives under `Assets/Tests`. These cover movement mechanics, spawner behaviour, power-ups, and utility classes such as `ObjectPool`. The repository’s GitHub Actions workflow installs Unity in a CI environment and runs the tests automatically on each pull request, producing XML results and logs.

The tests are organized by feature so contributors can quickly locate failures. For example, `PlayerControllerTests` validate jump buffering edge cases while `SaveGameTests` ensure progress persists between sessions. When a pull request is opened, the workflow downloads the Unity editor, imports the project, executes the tests in batch mode, and fails the build if any assertion fails. Test results and logs are archived under the `TestResults` artifact so developers can review the output.
Optional code coverage reports can be generated locally with the `-enableCodeCoverage` flag and aggregated in CI for further analysis.

Developers can run the suite locally through Unity’s Test Runner or via the command line:
```bash
Unity -batchmode -projectPath <path> -runTests -testPlatform editmode \
  -testResults Results.xml -logFile test.log -quit
```

## 6. Extending Cave-Runner
The modular design makes it straightforward to add new content:

- **New stages**: Create a `StageDataSO` asset, populate its fields, and add it to `StageManager.stages`.
- **New power-ups**: Derive from the base power-up class and implement activation logic. Register the prefab with `PowerUpSpawner`.
- **Custom enemies**: Implement a new behaviour (e.g., `ZigZagEnemy`) and include the prefab in the appropriate spawner array or stage data.
- **Additional achievements or analytics**: Extend `SteamManager` or `AnalyticsManager` with minimal coupling.

## 7. Future Directions
Potential enhancements include:

- **Procedurally generated level segments** for greater variety.
- **Dynamic weather or lighting** effects driven by stage data.
- **Multiplayer races** using Unity networking.
- **More sophisticated AI** for enemies such as pathfinding or predictive shooting.
- **Expanded mod tools** beyond the current workshop uploader.

## 8. Licensing
The project is released under a proprietary license. See [`LICENSE`](../LICENSE) for details. Redistribution or modification without permission is prohibited.

## 9. Conclusion
Cave-Runner demonstrates how a tightly scoped endless runner can be built in Unity with extensible systems for movement, spawning, power‑ups, and progression. The codebase balances simplicity with flexibility so designers can iterate quickly while keeping performance in mind. We welcome contributions that add creative stages, enemies, and polish while preserving the responsive feel that defines Cave-Runner.
Further documentation in the [`docs`](.) directory provides deeper dives into movement tuning, adaptive difficulty, stage configuration, graphics recommendations, and testing procedures. Reading those files alongside this whitepaper will help new contributors ramp up quickly and understand the project’s design philosophy.

