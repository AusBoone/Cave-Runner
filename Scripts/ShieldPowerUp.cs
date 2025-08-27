using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem; // Needed to pass the active gamepad
#endif

/// <summary>
/// Grants the player temporary invulnerability when collected.
/// </summary>
public class ShieldPowerUp : MonoBehaviour
{
    public float duration = 5f;
    public AudioClip collectClip;

    /// <summary>
    /// Grants a temporary shield when the player touches this power-up.
    /// </summary>
    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerShield shield = other.GetComponent<PlayerShield>();
            if (shield != null)
            {
                // Extend the shield duration by any purchased upgrade.
                float totalDuration = duration;
                if (ShopManager.Instance != null)
                {
                    totalDuration += ShopManager.Instance.GetUpgradeEffect(UpgradeType.ShieldDuration);
                }
                shield.ActivateShield(totalDuration);
                // Report shield activation to the daily challenge system
                if (DailyChallengeManager.Instance != null)
                {
                    DailyChallengeManager.Instance.RecordPowerUpUse(DailyChallengeManager.PowerUpType.Shield);
                }
            }
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySound(collectClip);
            }
            // Brief vibration feedback on pickup.
#if ENABLE_INPUT_SYSTEM
            // Rumble only the current player's controller to confirm shielding.
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
