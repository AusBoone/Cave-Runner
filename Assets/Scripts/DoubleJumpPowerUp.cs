using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem; // Enable routing rumble to active gamepad
#endif

/// <summary>
/// Grants the player an additional air jump for a limited time when collected.
/// Duration can be extended via the shop upgrade of type <see cref="UpgradeType.DoubleJumpDuration"/>.
/// </summary>
public class DoubleJumpPowerUp : MonoBehaviour
{
    [Tooltip("Seconds the double jump ability lasts after pickup.")]
    public float duration = 5f;

    [Tooltip("Sound played when the power-up is collected.")]
    public AudioClip collectClip;

    [Tooltip("Optional effect spawned at the player's position on activation.")]
    public GameObject pickupEffect;

    /// <summary>
    /// Activates the double jump ability for the player when they collide with this power-up.
    /// </summary>
    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerController pc = other.GetComponent<PlayerController>();
            if (pc != null)
            {
                float total = duration;
                if (ShopManager.Instance != null)
                {
                    total += ShopManager.Instance.GetUpgradeEffect(UpgradeType.DoubleJumpDuration);
                }
                pc.ActivateDoubleJump(total);

                // Notify the daily challenge system of usage.
                if (DailyChallengeManager.Instance != null)
                {
                    DailyChallengeManager.Instance.RecordPowerUpUse(DailyChallengeManager.PowerUpType.DoubleJump);
                }
            }

            if (pickupEffect != null)
            {
                Instantiate(pickupEffect, other.transform.position, Quaternion.identity);
            }
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySound(collectClip);
            }
#if ENABLE_INPUT_SYSTEM
            // Rumble the player's current controller to acknowledge the pickup.
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
