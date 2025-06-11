using UnityEngine;

/// <summary>
/// Moves an obstacle up and down in a ping-pong pattern while it scrolls
/// left using the <see cref="Scroller"/> component.
/// </summary>
public class MovingPlatform : MonoBehaviour
{
    /// <summary>
    /// How far the platform moves above and below its starting position.
    /// </summary>
    public float amplitude = 1f;

    /// <summary>
    /// Speed of the up and down oscillation in cycles per second.
    /// </summary>
    public float frequency = 1f;
    /// <summary>
    /// Original local position used as the center of the sine wave motion.
    /// </summary>
    private Vector3 startPos;

    /// <summary>
    /// Internal timer used to drive the sine wave motion. Incremented
    /// using <see cref="Time.deltaTime"/> so it halts when
    /// <c>Time.timeScale</c> is zero.
    /// </summary>
    private float timer;

    /// <summary>
    /// Stores the starting position to calculate the oscillation offset.
    /// </summary>
    void Start()
    {
        startPos = transform.position;
    }

    /// <summary>
    /// Called whenever the object becomes enabled. Resets the starting
    /// position and timer so pooled instances start from their new
    /// location without continuing the previous animation cycle.
    /// </summary>
    void OnEnable()
    {
        startPos = transform.position;
        timer = 0f;
    }

    /// <summary>
    /// Oscillates the platform vertically using a sine wave.
    /// </summary>
    void Update()
    {
        timer += Time.deltaTime;
        float y = Mathf.Sin(timer * frequency) * amplitude;
        transform.position = new Vector3(transform.position.x, startPos.y + y, transform.position.z);
    }
}
