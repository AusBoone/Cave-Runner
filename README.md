<!--
  README.md
  ---------
  Project overview and documentation index.
-->
# Cave-Runner

A 2D endless runner built with Unity. This repository contains basic scripts for a Jetpack Joyride–style game with jumping and sliding mechanics. For an in-depth discussion of design decisions, see [`docs/Whitepaper.md`](docs/Whitepaper.md).

Additional documentation describing gameplay systems lives in the [`docs`](docs/) folder.
Key files include:
- **DeveloperSetup.md** – environment setup, required packages, and build instructions.
- **GameMechanics.md** – high level overview of movement and scoring.
- **InputBindings.md** – instructions for customizing controls.
- **MovementTuning.md** – how to adjust physics parameters for a snappy feel.
- **Testing.md** – running the included edit mode tests.
- **GraphicsSettings.md** – recommended quality and resolution options.
- **AchievementsMenu.md** – configuring an in-game achievements list and tying
  it to Steam and localization services.
- **AdaptiveDifficulty.md** – overview of the adaptive difficulty system.
- **Whitepaper.md** – in-depth design paper analyzing the event-driven
  architecture, spawn algorithms, and proposed areas of future research.
- **ArchitectureOverview.md** – summary of major systems, including how
  `GameManager`, `SaveGameManager`, and `UIManager` interact.

## Tooling

This Unity project intentionally avoids Node.js and related npm tooling. The
previous placeholder `package.json` has been removed so contributors can focus
solely on the Unity editor and command-line interface.

For contribution guidelines and testing instructions, see
[`CONTRIBUTING.md`](CONTRIBUTING.md).

## Getting Started
For full environment configuration see [docs/DeveloperSetup.md](docs/DeveloperSetup.md).
1. Install **Unity 2022.3 LTS** or newer using Unity Hub.
2. Clone this repository and open it with Unity.
3. Create a new scene and add the scripts found in `Assets/Scripts` to your GameObjects:
   - `GameManager` controls the scrolling speed, score and high score tracking.
   - `UIManager` shows the start menu, pause menu and game over screen.
   - `AudioManager` plays background music and sound effects.
  - `PlayerController` handles jumping, double jumping and sliding while
    applying jump/slide buffering, optional mid-air dive, fast-fall when holding
    the down key, dynamic gravity scaling, an air dash triggered by sliding with
    horizontal input, and slide canceling for responsive controls.
   - `ObstacleSpawner` generates stalagmites and stalactites with increasing difficulty.
   - `HazardSpawner` creates pits and bat swarms that spawn faster over time.
   - `Scroller` moves obstacles and scenery leftward.
   - `CoinSpawner` randomly generates collectible coins.
   - `Coin` awards coins on contact with the player.
   - **Coin Combo** increases coin value when you grab coins quickly, up to a configurable cap (x10 by default).
   - `PowerUpSpawner` spawns temporary power-up items.
   - `MagnetPowerUp` grants a short-lived coin magnet effect when collected.
  - `SpeedBoostPowerUp` temporarily increases the player's speed.
  - `CoinBonusPowerUp` multiplies coin pickups for a short time. Durations
    stack when multiple are collected and an optional UI label can display the
    remaining time.
  - `CoinMagnet` attaches to the player and pulls coins in while the effect is active.
   - `CameraFollow` keeps the camera tracking the player.
   - `ParallaxBackground` scrolls looping background sprites.
   - `SteamManager` initializes the Steamworks API and saves high scores to the cloud.
    - `WorkshopManager` uploads and downloads level or skin packs from the Steam Workshop.
    - `ObjectPool` provides reusable objects for the spawners.
    - `AnalyticsManager` logs run data locally and can post it to a remote URL you provide.
    - `AdaptiveDifficultyManager` scales spawn rates based on recent analytics.
   - `ShopManager` persists coins and upgrades via `SaveGameManager` so players can buy bonuses between runs. Upgrades extend power-up durations (magnet, speed boost, shield, coin bonus) and award extra coins per pickup.
4. Add prefabs for your player, obstacles, hazards, and coins, then assign them in the inspector. Link the coin label and combo label fields of `GameManager` to UI Text elements.
5. Create a GameObject with the `ShopManager` script so coins and upgrades persist between runs. `SaveGameManager` is automatically created by `GameManager`, so no setup is required for the save file. Add a shop panel and assign it to `UIManager.shopPanel`.
6. Tag any obstacle or hazard prefab with **Obstacle** or **Hazard** so collisions trigger a restart. Tag coin prefabs with **Coin** so they can be collected.
7. Press Play to run the game. Use the start menu's **Play** button to begin. Press **Esc** during play to pause and resume. The score counts how far you travel and the speed increases over time. Collect coins for bonus points—grabbing several in quick succession will build a combo that multiplies their value up to the configured cap (x10 by default). If the player hits an obstacle or hazard, a game-over screen shows your distance, coin total, and the best score so far, allowing you to restart.
## Additional Setup Steps
This repository primarily provides the C# scripts. Minimal `ProjectSettings`
files are now included so automated tests and builds can run. You should still
review and customize these settings for your own project before shipping.

1. Create a new Unity **2022.3 LTS** project and import the scripts from
   `Assets/Scripts` into the new project's `Assets` folder.
2. Provide or create your own scenes, prefabs, sprites and audio clips for the
   player, obstacles and UI elements.
3. [Install Steamworks.NET](https://steamworks.github.io/) if you plan to use
   the `SteamManager` or `WorkshopManager` features.
4. Customize the provided `ProjectSettings` folder as needed for your project.

## Essential Prefabs and Tags
You will need prefabs for your player character, obstacle or hazard objects, and
collectible coins. Tag them so the scripts can detect collisions:
**Player** on the player, **Obstacle** or **Hazard** on anything that ends the
run, and **Coin** on coins.

Organize custom art and audio clips under the following folders so the included
scripts can load them easily:

```
Assets/
  Art/Resources/   // sprites and animations
  Audio/Resources/ // music and sound effects
```

If you use the Steam features, create a `steam_appid.txt` in the project root
with your app's ID so Steamworks initializes correctly.

## Scene Setup Tips
- Add a main camera and attach the **CameraFollow** script. Assign the player transform to the script's *target* field so the camera smoothly follows the character.
- Create one or more background sprites and attach the **ParallaxBackground** script so they loop seamlessly as the player moves.
- Attach two `AudioSource` components to a new `AudioManager` GameObject—one for music and one for sound effects—then assign them in the inspector.
- Place `ObstacleSpawner`, `HazardSpawner`, and `CoinSpawner` objects slightly off the right side of the screen (around x = 10) so spawned prefabs scroll in from the side. The spawners automatically reuse objects with an internal pool for smoother performance.
- Add a `PowerUpSpawner` object with magnet or speed boost prefabs so players can collect temporary power-ups. Attach a `CoinMagnet` component to the player for magnet bonuses.
- For phone and tablet builds the game automatically instantiates the simplified
  `MobileUI` prefab from `Resources/UI`. You can still drop the older
  `MobileControls` prefab onto an existing canvas if you prefer to customize the
  layout manually.
- Add a `StageManager` object and assign background sprite names and obstacle
  prefabs for each stage. The manager listens to `GameManager.OnStageUnlocked`
  and swaps the active background plus spawner lists when new stages begin.
- Include a shop panel in the canvas and wire its buttons to `UIManager.ShowShop` and `UIManager.HideShop`.
This project now includes simple menus, audio hooks, and escalating difficulty but you can further expand it with custom art, music, and polished effects.
## Stage Configuration
See [docs/StageConfiguration.md](docs/StageConfiguration.md) for a detailed
guide to creating `StageDataSO` assets and unlocking stages.

`GameManager` uses the `stageGoals` array to determine when stages unlock.
Each element maps to an entry on `StageManager`. Provide the background sprite
name along with obstacle and hazard prefabs for that stage so tougher hazards
appear as players progress.


## Steam Integration
The project optionally supports Steam achievements and cloud saves using the
[Steamworks.NET](https://steamworks.github.io/) plugin.

1. Import the Steamworks.NET package into your Unity project.
2. Create a new GameObject called `SteamManager` and attach the provided
   `SteamManager` script.
3. Ensure your app ID is set up in `steam_appid.txt` when running in the editor.

`SteamManager` saves the player's high score to the Steam Cloud and unlocks two
example achievements:

- **ACH_DISTANCE_1000** – travel 1000 distance.
- **ACH_COINS_50** – collect 50 coins.

You can define additional achievements in Steamworks and unlock them using
`SteamManager.Instance.UnlockAchievement("ID")` from your scripts.

The `SteamManager` component also exposes a **leaderboardId** field. Set this to
the Steam leaderboard identifier you wish to use for global high scores. The
UI's leaderboard panel queries this ID when downloading and uploading scores.

For non-Steam versions of the game a lightweight HTTP leaderboard service is
available via the new `LeaderboardClient` component. Configure its `serviceUrl`
with an **HTTPS** endpoint and reference it from `UIManager` to display scores
retrieved from a REST endpoint. The field is empty by default, preventing
insecure or accidental requests. If the service cannot be reached the local high
score is shown instead. See [docs/LeaderboardSetup.md](docs/LeaderboardSetup.md)
for deployment guidance.

### Workshop Content
The included `WorkshopManager` script uses Steamworks.NET's UGC API so you can
share level or skin packs on the Steam Workshop.

1. Attach `WorkshopManager` to a persistent GameObject.
2. Call `WorkshopManager.UploadItem()` to publish a folder of content.
3. Players can browse subscribed packs through the **Workshop** menu option and
   apply them in game.

## Save Files and Migration
`SaveGameManager` replaces the previous `PlayerPrefs` based save system. On
startup it loads `savegame.json` from `Application.persistentDataPath` and
migrates any existing `PlayerPrefs` values the first time it runs. Coins,
upgrade levels and the high score are serialized to this file so progress
persists across sessions.

## Analytics and Feedback
the player died. Statistics are stored locally using Unity's `PlayerPrefs` and
can be sent to your own server by entering a URL in the manager's
`remoteEndpoint` field. If left blank, no network requests are made and the
data remains on the player's machine.  A `feedbackUrl` on `UIManager` can open
an external survey or bug‑report form to collect additional feedback from
players.

## Daily Challenges & Accessibility
`DailyChallengeManager` automatically loads or creates a daily objective at
startup. Goals are randomly chosen to track distance traveled, coins collected
or the number of times a power-up is used. Progress is stored in `PlayerPrefs`
so a challenge persists even after closing the game. When the target is reached
the manager saves the completion state, awards bonus coins and triggers the
`ACH_DAILY_COMPLETE` achievement if Steam is present.

Players can enable colorblind mode from the **Settings** menu. The toggle in
`SettingsMenu` calls `ColorblindManager.SetEnabled` which writes the option to
`PlayerPrefs` and notifies any `ColorblindMode` components to update their
colors. The state is restored on startup so the preference remains across
sessions.

To display progress in your UI add a `DailyChallengeUI` component to a panel and
link a text field (and optional progress bar). Open the settings screen during
play to toggle colorblind mode at any time.

## Input System Setup
This project now supports Unity's **Input System** package. Install the package
through the Package Manager and enable the *Input System Package* when prompted.
The `InputManager` script exposes actions for **jump**, **slide**, and **pause**
which automatically map to keyboard, Xbox and PlayStation controllers. If the package is
not installed the scripts fall back to the legacy `KeyCode` input checks with common
joystick button mappings. WASD movement with the spacebar to jump is enabled by
default. Call `InputManager.SetMoveLeftKey` and `InputManager.SetMoveRightKey`
to change the horizontal keys when using the legacy input manager.
Rebinding can be triggered from the in-game settings menu when the new system is
available.

## Running Edit Mode Tests
This repository includes a small suite of EditMode tests.

1. In the Unity editor open **Window > General > Test Runner**.
2. Select the **EditMode** tab and click **Run All** to execute the tests.

To automate testing in CI you can call the Unity executable in batch mode:

```bash
Unity -batchmode -projectPath <path> -runTests -testPlatform editmode \
  -testResults Results.xml -logFile test.log -quit
```

The command writes an XML report (`Results.xml`) and a detailed log file. Both
can be archived as build artifacts to review pass/fail status.

The test scripts are located in `Assets/Tests/EditMode`.
New tests exercise behaviour for components like **MovingPlatform**,
**PowerUpSpawner**, and **CoinMagnet** to ensure pausing and pooled
objects work correctly.

## Opening, Running, and Building in Unity 2022.3 LTS
1. Launch **Unity Hub** and make sure **Unity 2022.3 LTS** is installed.
2. In Unity Hub, click **Open** and select this repository's folder to load the project.
3. After the editor finishes importing, open or create a scene and assign the scripts listed above.
4. Click the **Play** button in the editor to try the game.
5. To create a build, open **File > Build Settings...**, click **Add Open Scenes**, choose your target platform, and then click **Build** to generate the executable.

This project is covered by a [proprietary license](LICENSE).
