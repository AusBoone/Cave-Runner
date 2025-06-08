using UnityEngine;

/// <summary>
/// Handles a temporary invulnerability shield for the player.
/// When active, collisions with obstacles or hazards are ignored.
/// </summary>
public class PlayerShield : MonoBehaviour
{
    private float shieldTimer;

    public bool IsActive => shieldTimer > 0f;

    /// <summary>
    /// Counts down the remaining shield duration each frame.
    /// </summary>
    void Update()
    {
        if (shieldTimer > 0f)
        {
            shieldTimer -= Time.deltaTime;
        }
    }

    /// <summary>
    /// Enables the shield for the specified duration in seconds.
    /// </summary>
    public void ActivateShield(float duration)
    {
        shieldTimer = duration;
    }

    /// <summary>
    /// Consumes the shield immediately, typically after blocking a hit.
    /// </summary>
    public void AbsorbHit()
    {
        shieldTimer = 0f;
    }
}
