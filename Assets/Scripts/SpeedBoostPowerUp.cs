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
                GameManager.Instance.ActivateSpeedBoost(duration, speedMultiplier);
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
