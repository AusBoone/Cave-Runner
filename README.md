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
4. Add prefabs for your player, obstacles, and hazards, then assign them in the inspector.
5. Tag any obstacle or hazard prefab with **Obstacle** or **Hazard** so collisions trigger a restart.
6. Press Play to run the game. Use the start menu's **Play** button to begin. Press **Esc** during play to pause and resume. The score counts how far you travel and the speed increases over time. If the player hits an obstacle or hazard, a game-over screen shows your distance and the best score, allowing you to restart.

This project now includes simple menus, audio hooks, and escalating difficulty but you can further expand it with custom art, music, and polished effects.
