/*
 * GravityFlipPowerUp.cs
 * ---------------------
 * Purpose: Reverses the direction of gravity for a short period, allowing the
 *          player to traverse ceilings or avoid ground hazards.
 * Typical usage: Attach to a prefab with a trigger collider and spawn through
 *                PowerUpSpawner. The effect triggers when the Player enters the
 *                collider.
 * Example: Place the power-up above a row of spikes; collecting it flips
 *          gravity so the player can safely pass overhead.
 * Assumptions: Depends on GameManager, AudioManager, DailyChallengeManager, and
 *              InputManager singletons. The player object must be tagged
 *              "Player".
 * Design decisions: Uses object pooling via PooledObject and reports usage to
 *                   DailyChallengeManager. No upgrade interactions are currently
 *                   defined.
 */

using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem; // Needed for specifying the target gamepad
#endif

/// <summary>
/// Temporarily flips global gravity when collected.
/// </summary>
public class GravityFlipPowerUp : MonoBehaviour
{
    public float duration = 5f;
    public AudioClip collectClip;

    /// <summary>
    /// Activates gravity flipping when the player collects this item.
    /// </summary>
    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            if (GameManager.Instance != null)
            {
                // Delegate the gravity inversion to the GameManager which handles
                // physics changes globally.
                GameManager.Instance.ActivateGravityFlip(duration);
                // Log the usage for daily challenges so progress can be tracked.
                if (DailyChallengeManager.Instance != null)
                {
                    DailyChallengeManager.Instance.RecordPowerUpUse(DailyChallengeManager.PowerUpType.GravityFlip);
                }
            }
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySound(collectClip);
            }
#if ENABLE_INPUT_SYSTEM
            // Route rumble to the active controller for tactile feedback.
            InputManager.TriggerRumble(0.3f, 0.1f, Gamepad.current);
#else
            InputManager.TriggerRumble(0.3f, 0.1f);
#endif
            // Return to pool if possible, otherwise destroy to free resources.
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
