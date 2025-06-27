using UnityEngine;

/// <summary>
/// Collectible coin that increments the player's coin total when the
/// player enters its trigger collider. Coins can optionally be pooled
/// for reuse by attaching a <see cref="PooledObject"/> component.
/// </summary>
public class Coin : MonoBehaviour
{
    public int value = 1;
    public AudioClip collectClip;

    /// <summary>
    /// Triggered when another collider enters the coin's trigger. If the
    /// collider belongs to the player, coins are added and the object is
    /// either returned to its pool or destroyed. The final value includes
    /// any combo or upgrade bonuses handled by <see cref="GameManager.AddCoins"/>.
    /// </summary>
    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.AddCoins(value);
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
