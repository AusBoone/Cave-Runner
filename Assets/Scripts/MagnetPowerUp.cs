using UnityEngine;

/// <summary>
/// Grants the player a temporary coin magnet effect when collected.
/// This power-up is typically spawned by <see cref="PowerUpSpawner"/>.
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
                // Notify the DailyChallengeManager so magnet use can count toward challenges
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
            InputManager.TriggerRumble(0.3f, 0.1f);
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
