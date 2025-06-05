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
                magnet.ActivateMagnet(duration);
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
