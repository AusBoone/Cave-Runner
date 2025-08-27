/*
 * SpeedBoostPowerUp.cs
 * --------------------
 * Purpose: Temporarily increases the player's movement speed when picked up.
 *          Useful for navigating obstacles or reaching distant coins.
 * Typical usage: Attach to a trigger-enabled prefab spawned by
 *                PowerUpSpawner. OnTriggerEnter2D activates the effect for the
 *                colliding player.
 * Example: Place the SpeedBoostPowerUp prefab in a challenging corridor. When
 *          the player touches it, GameManager.ActivateSpeedBoost is invoked.
 * Assumptions: Relies on GameManager, AudioManager, ShopManager,
 *              DailyChallengeManager, and InputManager singletons. The player
 *              tag must be "Player" and a trigger collider must surround the
 *              power-up.
 * Design decisions: Effect duration can be extended by upgrades from
 *                   ShopManager. Object pooling via PooledObject avoids
 *                   frequent allocations. Usage is tracked for daily challenges.
 */

using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem; // Needed for specifying target gamepad
#endif

/// <summary>
/// Grants the player a temporary speed multiplier when collected. Typically
/// spawned by the <see cref="PowerUpSpawner"/>.
/// </summary>
public class SpeedBoostPowerUp : MonoBehaviour
{
    public float duration = 5f;
    public float speedMultiplier = 2f;
    public AudioClip collectClip;

    /// <summary>
    /// Activates the speed boost when the player touches this power-up
    /// and then returns the object to its pool or destroys it.
    /// </summary>
    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            if (GameManager.Instance != null)
            {
                // Add any purchased upgrade effect to the base duration so
                // higher levels extend the boost time.
                float totalDuration = duration;
                if (ShopManager.Instance != null)
                {
                    totalDuration += ShopManager.Instance.GetUpgradeEffect(UpgradeType.SpeedBoostDuration);
                }
                // Apply the boost through the GameManager which manages player state.
                GameManager.Instance.ActivateSpeedBoost(totalDuration, speedMultiplier);
                // Inform the DailyChallengeManager of the power-up usage for challenge tracking.
                if (DailyChallengeManager.Instance != null)
                {
                    DailyChallengeManager.Instance.RecordPowerUpUse(DailyChallengeManager.PowerUpType.SpeedBoost);
                }
            }
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySound(collectClip);
            }
            // Provide subtle feedback on collection.
#if ENABLE_INPUT_SYSTEM
            // Notify the player via their current controller about the speed boost.
            InputManager.TriggerRumble(0.3f, 0.1f, Gamepad.current);
#else
            InputManager.TriggerRumble(0.3f, 0.1f);
#endif
            // Recycle the power-up through its pool if available.
            PooledObject po = GetComponent<PooledObject>();
            if (po != null && po.Pool != null)
            {
                po.Pool.ReturnObject(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}
