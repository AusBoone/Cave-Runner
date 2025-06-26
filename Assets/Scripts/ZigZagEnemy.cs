// ZigZagEnemy.cs
// -----------------------------------------------------------------------------
// Enemy behavior that moves leftward while oscillating vertically in a sine
// wave pattern. Designed for use with HazardSpawner so flying hazards can have
// varied movement. Movement halts automatically when the game is paused because
// it relies on Time.deltaTime. The timer resets whenever the object is enabled
// so pooled instances start from the beginning of the zig-zag pattern.
// -----------------------------------------------------------------------------
using UnityEngine;

/// <summary>
/// Enemy that travels left at a constant speed while moving up and down using
/// a sine wave. Attach this to a hazard prefab. Works correctly with pooling
/// because state is reset on <see cref="OnEnable"/>.
/// </summary>
public class ZigZagEnemy : MonoBehaviour
{
    [Tooltip("Horizontal movement speed in units per second.")]
    public float speed = 3f;

    [Tooltip("Vertical amplitude of the zig-zag motion.")]
    public float amplitude = 1f;

    [Tooltip("Oscillation frequency in cycles per second.")]
    public float frequency = 1f;

    // Original starting position for the oscillation.
    private Vector3 startPos;

    // Internal timer driving the sine wave.
    private float timer;

    /// <summary>
    /// Called when the object becomes enabled or is spawned from a pool.
    /// Resets the starting position and timer so motion begins from the
    /// current location.
    /// </summary>
    void OnEnable()
    {
        startPos = transform.position;
        timer = 0f;
    }

    /// <summary>
    /// Resets the timer when disabled so pooled instances do not resume
    /// mid-oscillation when re-enabled.
    /// </summary>
    void OnDisable()
    {
        timer = 0f;
    }

    /// <summary>
    /// Moves left and applies the vertical sine wave while the game is
    /// running. Does nothing if the GameManager indicates gameplay is not
    /// active.
    /// </summary>
    void Update()
    {
        if (GameManager.Instance == null || !GameManager.Instance.IsRunning())
        {
            return;
        }

        timer += Time.deltaTime;
        float yOffset = Mathf.Sin(timer * frequency * 2f * Mathf.PI) * amplitude;
        transform.Translate(Vector3.left * speed * Time.deltaTime);
        transform.position = new Vector3(transform.position.x, startPos.y + yOffset, transform.position.z);
    }
}
