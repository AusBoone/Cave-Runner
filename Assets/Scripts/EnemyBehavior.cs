// EnemyBehavior.cs
// -----------------------------------------------------------------------------
// AI controller for simple flying enemies that pursue the player horizontally.
// Movement now uses world coordinates so rotation does not influence the
// direction of travel, preventing erratic paths for rotated enemies.
// -----------------------------------------------------------------------------
using UnityEngine;

/// <summary>
/// Simple enemy AI that chases the player while the game is running. Designed
/// for flying bats or other mobile hazards spawned by
/// <see cref="HazardSpawner"/>.
/// </summary>
public class EnemyBehavior : MonoBehaviour
{
    // Movement speed in world units per second.
    public float speed = 3f;

    // Cached reference to the player transform for efficient lookups.
    private Transform player;

    /// <summary>
    /// Locates the player object so the enemy can chase it. If no player is
    /// found, the enemy remains idle. Assumes a GameObject tagged "Player"
    /// exists in the scene.
    /// </summary>
    void Start()
    {
        GameObject obj = GameObject.FindGameObjectWithTag("Player");

        // If the player is not found the enemy cannot move; leaving player null
        // gracefully halts Update movement logic.
        if (obj != null)
        {
            player = obj.transform;
        }
    }

    /// <summary>
    /// Moves toward the player each frame while the game is running. Movement
    /// occurs in world space so the enemy's rotation does not alter the chase
    /// direction, keeping pursuit behavior consistent.
    /// </summary>
    void Update()
    {
        // Abort if no player has been located or the game is currently paused
        // or stopped.
        if (player == null)
        {
            return;
        }
        if (GameManager.Instance == null || !GameManager.Instance.IsRunning())
        {
            return;
        }

        // Calculate the normalized direction vector toward the player and move
        // the enemy using world coordinates to remain independent of rotation.
        Vector3 dir = (player.position - transform.position).normalized;
        transform.Translate(dir * speed * Time.deltaTime, Space.World);
    }
}
