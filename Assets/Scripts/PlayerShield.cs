using UnityEngine;

// -----------------------------------------------------------------------------
// PlayerShield
// -----------------------------------------------------------------------------
// Component that grants temporary invulnerability. When activated the host
// ignores damage sources until the timer expires or the shield is manually
// consumed. Attach this to the player character and call ActivateShield from
// power-up pickups or other gameplay events.
// -----------------------------------------------------------------------------
/// <summary>
/// Provides a lightweight invulnerability mechanic for the player. While active
/// the owning GameObject ignores collisions with hazards and obstacles. Typical
/// usage spawns a visual effect when <see cref="ActivateShield"/> is called and
/// relies on <see cref="IsActive"/> checks within <see cref="PlayerController"/>
/// before applying damage.
/// </summary>
public class PlayerShield : MonoBehaviour
{
    private float shieldTimer;

    public bool IsActive => shieldTimer > 0f;

    /// <summary>
    /// Decrements the shield timer each frame. Once it reaches zero the shield
    /// effect ends automatically.
    /// </summary>
    void Update()
    {
        if (shieldTimer > 0f)
        {
            shieldTimer -= Time.deltaTime;
        }
    }

    /// <summary>
    /// Enables the shield for the specified duration in seconds. Passing a
    /// non-positive value throws to alert callers of invalid usage.
    /// </summary>
    public void ActivateShield(float duration)
    {
        if (duration <= 0f)
            throw new System.ArgumentException("duration must be positive", nameof(duration));

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
