# Stage Configuration

The `StageManager` controls which background sprite, obstacles and hazards are active as the player travels further. Each stage's settings are stored in a `StageDataSO` asset so they can be tweaked in the inspector and reused between scenes.

## StageManager.StageData Fields

Every `StageDataSO` contains one `StageData` struct. Each field affects gameplay when its stage becomes active:

- **backgroundSprite** – Addressable reference to the sprite that replaces the current background when the stage starts.
- **groundObstacles** – Addressable prefabs spawned by `ObstacleSpawner` on the ground.
- **ceilingObstacles** – Addressable prefabs spawned upside‑down on the ceiling.
- **movingPlatforms** – Addressable platform prefabs that move horizontally or vertically.
- **rotatingHazards** – Addressable hazards that spin or rotate as they move across the screen.
- **pits** – Addressable pit prefabs for the `HazardSpawner`.
- **bats** – Addressable flying hazard prefabs spawned from above.
- **zigZagEnemies** – Addressable enemies that zig‑zag horizontally.
- **swoopingEnemies** – Addressable enemies that swoop toward the player in an arc.
- **shooterEnemies** – Addressable enemies that fire projectiles at the player.
- **stageMusic** – Names of music clips located under `Resources/Audio`.
  One clip is randomly chosen when the stage begins and cross-faded in.
- **obstacleSpawnMultiplier** – Scales how frequently obstacles spawn.
- **hazardSpawnMultiplier** – Scales how frequently hazards spawn.
- **groundObstacleChance** – Relative probability that a ground obstacle spawns compared to other obstacle types.
- **ceilingObstacleChance** – Relative probability of a ceiling obstacle.
- **movingPlatformChance** – Relative probability for moving platforms.
- **rotatingHazardChance** – Relative probability for rotating hazards.
- **pitChance** – Relative probability that a pit spawns.
- **batChance** – Relative probability for bats.
- **zigZagChance** – Relative probability for zig‑zag enemies.
- **swoopChance** – Relative probability for swooping enemies.
- **shooterChance** – Relative probability for shooter enemies.
- **speedMultiplier** – Multiplies the base game speed while the stage is active.
- **gravityScale** – Multiplies the global gravity value when the stage begins.

## Creating StageDataSO Assets

1. In the **Project** window right‑click and select **Create → CaveRunner → Stage Data**.
2. Fill out the fields described above in the inspector. Leave arrays empty if a stage should not spawn that type of hazard.
3. Add each asset to the `StageManager.stages` array in the desired order.

## Unlocking Stages

`GameManager` exposes a `stageGoals` array of distance values. When the player's distance crosses one of these values, the game increases the current stage index and triggers `OnStageUnlocked`. `StageManager` listens to this event and applies the corresponding `StageDataSO` from its `stages` array.

```
// Example setup in the inspector
GameManager.stageGoals = [100, 300, 600];
StageManager.stages = [stage0, stage1, stage2, stage3];
```

In this example `stage0` is active at the start. When the player travels 100 units `stage1` unlocks, changing the background and updating the spawners. Progressing past 300 units unlocks `stage2`, and so on.

You can subscribe to `GameManager.OnStageUnlocked` from other scripts if additional reactions are needed:

```csharp
void Start()
{
    GameManager.Instance.OnStageUnlocked += index => LoggingHelper.Log($"Stage {index} unlocked");
}
```

Refer to `StageManager.cs` and `StageDataSO.cs` for the implementation details.
