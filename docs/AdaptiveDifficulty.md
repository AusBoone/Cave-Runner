# Adaptive Difficulty

The `AdaptiveDifficultyManager` scales obstacle and hazard spawn rates based on player performance.
It queries `AnalyticsManager` for the average distance of recent runs and adjusts multipliers
accordingly.

## Overview
- StageManager registers its spawners so the manager can modify their `spawnMultiplier` values.
- `GameManager` triggers an adjustment at the start of each run.
- If the average distance exceeds `targetDistance`, multipliers increase by `increaseStep`.
- If the distance falls short, multipliers decrease by `decreaseStep`.
- Multipliers are clamped between `minMultiplier` and `maxMultiplier` to avoid extremes.

## Usage Example
1. Add an `AdaptiveDifficultyManager` object to your initial scene.
2. Ensure an `AnalyticsManager` component is present so run data is recorded.
3. StageManager automatically registers its spawners in `Awake`.
4. Start a run and the system will adjust spawn rates based on past performance.

The design focuses on gradual adjustments to keep difficulty feeling fair while
rewarding skilled players with faster pacing.
