/*
 * MagnetPowerUp.cs
 * -----------------
 * Purpose: Grants the player a temporary magnetic field that attracts coins
 *          within range. Used to help players quickly gather collectibles.
 * Typical usage: Attach to a prefab with a 2D trigger collider and spawn it
 *                via the PowerUpSpawner. When the Player enters the trigger
 *                the magnet effect is activated automatically.
 * Example: In a cave scene, drop the MagnetPowerUp prefab under a spawner or
 *          place it manually in the level. Upon the player touching it the
 *          CoinMagnet component on the player is enabled for a duration.
 * Assumptions: Requires global singletons such as GameManager and
 *              AudioManager. Assumes the player has a CoinMagnet component and
 *              that InputManager can trigger controller rumble.
 * Design decisions: Utilizes object pooling through PooledObject to reduce
 *                   instantiation cost. Duration is extended by any purchased
 *                   upgrades from ShopManager, and usage is reported to the
 *                   DailyChallengeManager for progress tracking.
 */

using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem; // Specify gamepad for rumble
#endif

/// <summary>
/// Grants the player a temporary coin magnet effect when collected. This
/// power-up is typically spawned by <see cref="PowerUpSpawner"/>.
/// </summary>
public class MagnetPowerUp : MonoBehaviour
{
    public float duration = 5f;
    public AudioClip collectClip;

    /// <summary>
    /// When the player touches this power-up the magnet effect is
    /// activated and the object is returned to its pool or destroyed.
    /// </summary>
    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            // Attempt to retrieve the CoinMagnet component from the player.
            CoinMagnet magnet = other.GetComponent<CoinMagnet>();
            if (magnet != null)
            {
                // Include any purchased upgrade so the magnet lasts longer
                float totalDuration = duration;
                if (ShopManager.Instance != null)
                {
                    totalDuration += ShopManager.Instance.GetUpgradeEffect(UpgradeType.MagnetDuration);
                }
                magnet.ActivateMagnet(totalDuration);
                // Notify the DailyChallengeManager so magnet use can count toward
                // challenges that require magnet activation.
                if (DailyChallengeManager.Instance != null)
                {
                    DailyChallengeManager.Instance.RecordPowerUpUse(DailyChallengeManager.PowerUpType.Magnet);
                }
            }
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySound(collectClip);
            }
            // Light rumble to acknowledge the pickup.
#if ENABLE_INPUT_SYSTEM
            // Signal activation through the player's current controller.
            InputManager.TriggerRumble(0.3f, 0.1f, Gamepad.current);
#else
            InputManager.TriggerRumble(0.3f, 0.1f);
#endif
            // Prefer returning to the object pool for reuse; destroy if not pooled.
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
