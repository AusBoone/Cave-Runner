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
                GameManager.Instance.ActivateGravityFlip(duration);
                // Log the usage for daily challenges
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
