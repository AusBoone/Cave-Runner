# Cave-Runner

A 2D endless runner built with Unity. This repository contains basic scripts for a Jetpack Joyride–style game with jumping and sliding mechanics.

## Getting Started
1. Install **Unity 2022.3 LTS** or newer using Unity Hub.
2. Clone this repository and open it with Unity.
3. Create a new scene and add the scripts found in `Assets/Scripts` to your GameObjects:
   - `GameManager` controls the scrolling speed and score.
   - `PlayerController` handles jumping, double jumping, and sliding.
   - `ObstacleSpawner` generates stalagmites and stalactites.
   - `HazardSpawner` creates pits and bat swarms that require quick reactions.
   - `Scroller` moves obstacles and scenery leftward.
4. Add prefabs for your player, obstacles, and hazards, then assign them in the inspector.
5. Tag any obstacle or hazard prefab with **Obstacle** or **Hazard** so collisions trigger a restart.
6. Press Play to run the game. The score counts how far you travel and the speed increases over time. If the player hits an obstacle or hazard, the scene reloads.

This is only a starting point—you can expand on it with animations, art, sound, and additional mechanics.
