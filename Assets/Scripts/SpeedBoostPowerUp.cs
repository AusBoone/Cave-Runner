using UnityEngine;

// Grants a temporary speed multiplier when collected.
public class SpeedBoostPowerUp : MonoBehaviour
{
    public float duration = 5f;
    public float speedMultiplier = 2f;
    public AudioClip collectClip;

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
            Destroy(gameObject);
        }
    }
}
