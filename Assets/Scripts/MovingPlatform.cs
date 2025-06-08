using UnityEngine;

/// <summary>
/// Moves an obstacle up and down in a ping-pong pattern while it scrolls
/// left using the <see cref="Scroller"/> component.
/// </summary>
public class MovingPlatform : MonoBehaviour
{
    public float amplitude = 1f;
    public float frequency = 1f;
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
