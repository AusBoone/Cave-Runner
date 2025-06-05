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

    void Start()
    {
        startPos = transform.position;
    }

    void Update()
    {
        float y = Mathf.Sin(Time.time * frequency) * amplitude;
        transform.position = new Vector3(transform.position.x, startPos.y + y, transform.position.z);
    }
}
