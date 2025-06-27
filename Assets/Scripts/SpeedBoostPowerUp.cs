using UnityEngine;

/// <summary>
/// Grants the player a temporary speed multiplier when collected.
/// Typically spawned by the <see cref="PowerUpSpawner"/>.
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
                GameManager.Instance.ActivateSpeedBoost(totalDuration, speedMultiplier);
                // Inform the DailyChallengeManager of the power-up usage
                if (DailyChallengeManager.Instance != null)
                {
                    DailyChallengeManager.Instance.RecordPowerUpUse(DailyChallengeManager.PowerUpType.SpeedBoost);
                }
            }
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySound(collectClip);
            }
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
