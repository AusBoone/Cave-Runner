// SwoopingEnemy.cs
// -----------------------------------------------------------------------------
// Enemy behaviour that dives down from its spawn height then climbs back up
// while moving leftwards. Designed to add variety to aerial hazards spawned by
// HazardSpawner. State resets when pooled so each activation plays the swoop
// from the start.
// -----------------------------------------------------------------------------
using UnityEngine;

/// <summary>
/// Enemy that performs a single swoop motion using a sine curve. The object
/// travels left at a fixed speed while oscillating vertically downward and then
/// back up. Useful for enemies that dive toward the player.
/// </summary>
public class SwoopingEnemy : MonoBehaviour
{
    [Tooltip("Horizontal movement speed in units per second.")]
    public float speed = 3f;

    [Tooltip("Maximum height difference of the dive.")]
    public float amplitude = 2f;

    [Tooltip("Duration of one full dive cycle in seconds.")]
    public float duration = 1.5f;

    private Vector3 startPos;
    private float timer;

    void OnEnable()
    {
        startPos = transform.position;
        timer = 0f;
    }

    void OnDisable()
    {
        timer = 0f;
    }

    void Update()
    {
        if (GameManager.Instance == null || !GameManager.Instance.IsRunning())
        {
            return;
        }

        timer += Time.deltaTime;
        float progress = Mathf.Clamp01(timer / duration);
        float yOffset = -Mathf.Sin(progress * Mathf.PI) * amplitude;
        transform.Translate(Vector3.left * speed * Time.deltaTime);
        transform.position = new Vector3(transform.position.x, startPos.y + yOffset, transform.position.z);
    }
}
