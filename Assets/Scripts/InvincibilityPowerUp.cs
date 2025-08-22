using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem; // Allow targeting the player's gamepad
#endif

/// <summary>
/// Provides temporary invulnerability identical to <see cref="PlayerShield"/> but
/// themed as a separate power-up. Duration scales with the
/// <see cref="UpgradeType.InvincibilityDuration"/> shop upgrade.
/// </summary>
public class InvincibilityPowerUp : MonoBehaviour
{
    [Tooltip("Seconds the player remains invulnerable after pickup.")]
    public float duration = 5f;

    [Tooltip("Clip played upon collection.")]
    public AudioClip collectClip;

    [Tooltip("Optional visual effect prefab spawned when activated.")]
    public GameObject pickupEffect;

    /// <summary>
    /// Grants invulnerability when the player collides with this power-up.
    /// </summary>
    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerShield shield = other.GetComponent<PlayerShield>();
            if (shield != null)
            {
                float total = duration;
                if (ShopManager.Instance != null)
                {
                    total += ShopManager.Instance.GetUpgradeEffect(UpgradeType.InvincibilityDuration);
                }
                shield.ActivateShield(total);
                if (DailyChallengeManager.Instance != null)
                {
                    DailyChallengeManager.Instance.RecordPowerUpUse(DailyChallengeManager.PowerUpType.Invincibility);
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
            // Vibrate the active gamepad to reinforce invulnerability pickup.
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
