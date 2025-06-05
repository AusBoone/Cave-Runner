using UnityEngine;

/// <summary>
/// Temporarily flips global gravity when collected.
/// </summary>
public class GravityFlipPowerUp : MonoBehaviour
{
    public float duration = 5f;
    public AudioClip collectClip;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.ActivateGravityFlip(duration);
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
