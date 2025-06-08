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
    /// Stores the starting position to calculate the oscillation offset.
    /// </summary>
    void Start()
    {
        startPos = transform.position;
    }

    /// <summary>
    /// Oscillates the platform vertically using a sine wave.
    /// </summary>
    void Update()
    {
        float y = Mathf.Sin(Time.time * frequency) * amplitude;
        transform.position = new Vector3(transform.position.x, startPos.y + y, transform.position.z);
    }
}
