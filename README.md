# Cave-Runner

A 2D endless runner built with Unity. This repository contains basic scripts for a Jetpack Joyride–style game with jumping and sliding mechanics.

## Getting Started
1. Install **Unity 2022.3 LTS** or newer using Unity Hub.
2. Clone this repository and open it with Unity.
3. Create a new scene and add the scripts found in `Assets/Scripts` to your GameObjects:
   - `GameManager` controls the scrolling speed, score and high score tracking.
   - `UIManager` shows the start menu, pause menu and game over screen.
   - `AudioManager` plays background music and sound effects.
   - `PlayerController` handles jumping, double jumping, sliding and plays SFX.
   - `ObstacleSpawner` generates stalagmites and stalactites with increasing difficulty.
   - `HazardSpawner` creates pits and bat swarms that spawn faster over time.
   - `Scroller` moves obstacles and scenery leftward.
   - `CoinSpawner` randomly generates collectible coins.
   - `Coin` awards coins on contact with the player.
   - `PowerUpSpawner` spawns temporary power-up items.
   - `MagnetPowerUp` grants a short-lived coin magnet effect when collected.
   - `SpeedBoostPowerUp` temporarily increases the player's speed.
   - `CoinMagnet` attaches to the player and pulls coins in while the effect is active.
   - `CameraFollow` keeps the camera tracking the player.
   - `ParallaxBackground` scrolls looping background sprites.
   - `SteamManager` initializes the Steamworks API and saves high scores to the cloud.
    - `WorkshopManager` uploads and downloads level or skin packs from the Steam Workshop.
    - `ObjectPool` provides reusable objects for the spawners.
    - `AnalyticsManager` logs run data locally and can post it to a remote URL you provide.
4. Add prefabs for your player, obstacles, hazards, and coins, then assign them in the inspector. Link the coin label field of `GameManager` to a UI Text element.
5. Tag any obstacle or hazard prefab with **Obstacle** or **Hazard** so collisions trigger a restart. Tag coin prefabs with **Coin** so they can be collected.
6. Press Play to run the game. Use the start menu's **Play** button to begin. Press **Esc** during play to pause and resume. The score counts how far you travel and the speed increases over time. Collect coins for bonus points. If the player hits an obstacle or hazard, a game-over screen shows your distance, coin total, and the best score so far, allowing you to restart.


## Scene Setup Tips
- Add a main camera and attach the **CameraFollow** script. Assign the player transform to the script's *target* field so the camera smoothly follows the character.
- Create one or more background sprites and attach the **ParallaxBackground** script so they loop seamlessly as the player moves.
- Attach two `AudioSource` components to a new `AudioManager` GameObject—one for music and one for sound effects—then assign them in the inspector.
- Place `ObstacleSpawner`, `HazardSpawner`, and `CoinSpawner` objects slightly off the right side of the screen (around x = 10) so spawned prefabs scroll in from the side. The spawners automatically reuse objects with an internal pool for smoother performance.
- Add a `PowerUpSpawner` object with magnet or speed boost prefabs so players can collect temporary power-ups. Attach a `CoinMagnet` component to the player for magnet bonuses.
This project now includes simple menus, audio hooks, and escalating difficulty but you can further expand it with custom art, music, and polished effects.

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

### Workshop Content
The included `WorkshopManager` script uses Steamworks.NET's UGC API so you can
share level or skin packs on the Steam Workshop.

1. Attach `WorkshopManager` to a persistent GameObject.
2. Call `WorkshopManager.UploadItem()` to publish a folder of content.
3. Players can browse subscribed packs through the **Workshop** menu option and
   apply them in game.

## Analytics and Feedback
`AnalyticsManager` keeps a log of each run's distance, coin count and whether
the player died. Statistics are stored locally using Unity's `PlayerPrefs` and
can be sent to your own server by entering a URL in the manager's
`remoteEndpoint` field. If left blank, no network requests are made and the
data remains on the player's machine.  A `feedbackUrl` on `UIManager` can open
an external survey or bug‑report form to collect additional feedback from
players.

## Opening, Running, and Building in Unity 2022.3 LTS
1. Launch **Unity Hub** and make sure **Unity 2022.3 LTS** is installed.
2. In Unity Hub, click **Open** and select this repository's folder to load the project.
3. After the editor finishes importing, open or create a scene and assign the scripts listed above.
4. Click the **Play** button in the editor to try the game.
5. To create a build, open **File > Build Settings...**, click **Add Open Scenes**, choose your target platform, and then click **Build** to generate the executable.

This project is covered by a [proprietary license](LICENSE).
