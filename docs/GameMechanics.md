# Game Mechanics Overview

This document summarizes the core gameplay mechanics for **Cave-Runner** and how they interact.

## Movement
- **Jumping** – The player can jump while grounded and once again in mid-air. Jump input is buffered for a short period so pressing jump slightly before landing still triggers a hop. Releasing the jump key early results in a shorter jump due to additional gravity.
- **Sliding** – Triggered with the slide key when grounded. If pressed while airborne the input is buffered and automatically activates upon landing. Initiating a slide mid-air applies a downward dive impulse for a quicker landing. Releasing the key early cancels the slide immediately.
- **Fast Fall** – Holding the down key while airborne multiplies gravity causing a faster descent. This allows players to adjust their position precisely without committing to a full slide.
- **Air Dash** – Tap slide while holding left or right in the air to dash horizontally. Useful for clearing gaps or correcting overshoots.

## Power-Ups
- **Magnet** – Attracts coins within a radius for a limited time.
- **Speed Boost** – Temporarily increases player speed.
- **Shield** – Absorbs one obstacle or hazard hit before breaking.
- **Gravity Flip** – `GravityFlipPowerUp` invokes `GameManager.ActivateGravityFlip`
  to invert `Physics2D.gravity` for a 5‑second `duration`. Grabbing another
  pickup simply refreshes `gravityFlipTimer`; there are no upgrades or stacking
  interactions.
- **Slow Motion** – `SlowMotionPowerUp` uses `GameManager.ActivateSlowMotion`
  to set `Time.timeScale` to 0.5 for 3 seconds. Additional pickups reset the
  `slowMotionTimer`, and the effect cannot be upgraded or intensified.
- **Double Jump** – `DoubleJumpPowerUp` calls `PlayerController.ActivateDoubleJump`
  to allow one extra mid‑air jump for 5 seconds. The `UpgradeType.DoubleJumpDuration`
  shop upgrade adds seconds to the `duration`; extra pickups refresh the timer
  without granting more jumps.
- **Invincibility** – `InvincibilityPowerUp` enables `PlayerShield.ActivateShield`
  to ignore all damage for 5 seconds. The `UpgradeType.InvincibilityDuration`
  upgrade extends the `duration`; subsequent pickups only refresh the shield and
  the immunity does not stack.
- **Coin Bonus** – Temporarily multiplies the value of collected coins. Multiple
  bonuses stack their remaining time and a UI indicator can show the countdown.

## Scoring
- Distance traveled increments continuously while alive.
- Coins increase the score and can be spent on upgrades in the shop.
- Collecting coins quickly builds a combo multiplier up to **x10** by default.

## Saving and Progression
- Player coins, upgrades and high scores are stored via `SaveGameManager`.
- Stages unlock at set distance milestones defined in `GameManager.stageGoals`.
- Adaptive difficulty adjusts spawn rates based on recent run performance. See **AdaptiveDifficultyManager**.

Refer to the main [README](../README.md) for setup instructions and a full list of available scripts.
