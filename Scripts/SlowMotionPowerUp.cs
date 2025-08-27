/*
 * SlowMotionPowerUp.cs
 * --------------------
 * Purpose: Temporarily slows down time for the player, providing extra
 *          reaction time for hazards or precision jumps.
 * Typical usage: Attach to a prefab with a trigger collider and spawn using
 *                PowerUpSpawner. When the player enters the trigger the time
 *                scale is reduced for a set duration.
 * Example: Drop the SlowMotionPowerUp before a complex obstacle sequence; upon
 *          pickup the game runs in slow motion to aid navigation.
 * Assumptions: Requires GameManager, AudioManager, DailyChallengeManager, and
 *              InputManager singletons. The player object must carry the tag
 *              "Player".
 * Design decisions: Employs PooledObject for reuse, reports usage to
 *                   DailyChallengeManager, and allows configuration of duration
 *                   and time scale per instance.
 */

using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem; // Access Gamepad.current for rumble
#endif

/// <summary>
/// Grants a temporary slow motion effect when collected. Time scale is reduced
/// for the specified duration by calling <see cref="GameManager.ActivateSlowMotion"/>.
/// </summary>
public class SlowMotionPowerUp : MonoBehaviour
{
    [Tooltip("Seconds the slow motion lasts after pickup.")]
    public float duration = 3f;

    [Tooltip("Time scale applied while the effect is active (0-1)." )]
    public float timeScale = 0.5f;

    public AudioClip collectClip;

    /// <summary>
    /// When the player touches the power-up the slow motion effect is applied
    /// and the object is returned to its pool or destroyed.
    /// </summary>
    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            if (GameManager.Instance != null)
            {
                // Delegate time-scale manipulation to the GameManager so the
                // effect integrates with other global systems.
                GameManager.Instance.ActivateSlowMotion(duration, timeScale);
                // Inform daily challenge system of usage if configured.
                if (DailyChallengeManager.Instance != null)
                {
                    DailyChallengeManager.Instance.RecordPowerUpUse(DailyChallengeManager.PowerUpType.SlowMotion);
                }
            }
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySound(collectClip);
            }
#if ENABLE_INPUT_SYSTEM
            // Pulse the controller of the current player to highlight activation.
            InputManager.TriggerRumble(0.3f, 0.1f, Gamepad.current);
#else
            InputManager.TriggerRumble(0.3f, 0.1f);
#endif
            // Attempt to reuse the object via pooling to limit allocations.
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
