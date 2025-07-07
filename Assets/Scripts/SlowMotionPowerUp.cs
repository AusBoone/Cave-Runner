using UnityEngine;

/// <summary>
/// Grants a temporary slow motion effect when collected. Time scale is
/// reduced for the specified duration by calling <see cref="GameManager.ActivateSlowMotion"/>.
/// </summary>
public class SlowMotionPowerUp : MonoBehaviour
{
    [Tooltip("Seconds the slow motion lasts after pickup.")]
    public float duration = 3f;

    [Tooltip("Time scale applied while the effect is active (0-1)." )]
    public float timeScale = 0.5f;

    public AudioClip collectClip;

    /// <summary>
    /// When the player touches the power-up the slow motion effect is applied
    /// and the object is returned to its pool or destroyed.
    /// </summary>
    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.ActivateSlowMotion(duration, timeScale);
                // Inform daily challenge system of usage if configured
                if (DailyChallengeManager.Instance != null)
                {
                    DailyChallengeManager.Instance.RecordPowerUpUse(DailyChallengeManager.PowerUpType.SlowMotion);
                }
            }
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySound(collectClip);
            }
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
