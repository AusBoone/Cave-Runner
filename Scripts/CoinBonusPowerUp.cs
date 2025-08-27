using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem; // Allows specifying which gamepad to rumble
#endif

/// <summary>
/// Grants a temporary multiplier to all coin pickups when collected. The
/// duration can be extended via the <see cref="UpgradeType.CoinBonusDuration"/>
/// upgrade in the shop. Multiple pickups stack their remaining time and use
/// the highest multiplier.
/// </summary>
public class CoinBonusPowerUp : MonoBehaviour
{
    [Tooltip("Seconds the coin bonus remains active after pickup.")]
    public float duration = 5f;

    [Tooltip("Multiplier applied to coins while active.")]
    public float multiplier = 2f;

    public AudioClip collectClip;

    // Triggered when the player touches the power-up.
    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
            return;

        // Activate the bonus using the GameManager singleton.
        if (GameManager.Instance != null)
        {
            float totalDuration = duration;
            if (ShopManager.Instance != null)
            {
                totalDuration += ShopManager.Instance.GetUpgradeEffect(UpgradeType.CoinBonusDuration);
            }
            GameManager.Instance.ActivateCoinBonus(totalDuration, multiplier);
            // Report usage for daily challenges if enabled.
            if (DailyChallengeManager.Instance != null)
            {
                DailyChallengeManager.Instance.RecordPowerUpUse(DailyChallengeManager.PowerUpType.CoinBonus);
            }
        }
        // Play a pickup sound if configured.
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySound(collectClip);
        }
#if ENABLE_INPUT_SYSTEM
        // Provide immediate tactile feedback on the active controller.
        InputManager.TriggerRumble(0.3f, 0.1f, Gamepad.current);
#else
        InputManager.TriggerRumble(0.3f, 0.1f);
#endif
        // Return to pool if pooled, otherwise destroy.
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
