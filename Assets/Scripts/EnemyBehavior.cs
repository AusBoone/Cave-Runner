// EnemyBehavior.cs
// -----------------------------------------------------------------------------
// AI controller for simple flying enemies that pursue a designated target.
// The original implementation searched the scene for a GameObject tagged
// "Player" during Start(), which was both fragile and performed an expensive
// lookup.  This revision exposes a public SetTarget method so spawners can
// explicitly provide the player Transform when enemies are created. Movement
// continues to use world coordinates so rotation does not influence the chase
// direction, preventing erratic paths for rotated enemies.
// -----------------------------------------------------------------------------
using UnityEngine;

/// <summary>
/// Simple enemy AI that chases the player while the game is running. Designed
/// for flying bats or other mobile hazards spawned by
/// <see cref="HazardSpawner"/>.
/// </summary>
public class EnemyBehavior : MonoBehaviour
{
    // Movement speed in world units per second. Public so designers can tune
    // values in the Unity inspector.
    public float speed = 3f;

    // Cached reference to the target the enemy should pursue. Marked
    // [SerializeField] so a target can optionally be assigned in the inspector
    // for scenes that do not use the provided SetTarget method. When left null,
    // the enemy simply idles.
    [SerializeField, Tooltip("Transform this enemy will chase. Set via SetTarget or assign in inspector.")]
    private Transform player;

    /// <summary>
    /// Assigns the Transform the enemy should chase.
    /// </summary>
    /// <param name="target">Transform of the player or other object to pursue. May be null to clear the target.</param>
    public void SetTarget(Transform target)
    {
        // Direct assignment is sufficient—callers control when the target is
        // valid. Passing null intentionally leaves the enemy idle.
        player = target;
    }

    /// <summary>
    /// Moves toward the player each frame while the game is running. Movement
    /// occurs in world space so the enemy's rotation does not alter the chase
    /// direction, keeping pursuit behavior consistent.
    /// </summary>
    void Update()
    {
        // Abort when no target is assigned. The check prevents null reference
        // errors if SetTarget has not been called or the caller intentionally
        // cleared the target so the enemy should remain idle.
        if (player == null)
        {
            return;
        }

        // Ensure the game is currently running before applying movement. This
        // mirrors the behaviour of other scripts that pause when gameplay is
        // halted by GameManager.
        if (GameManager.Instance == null || !GameManager.Instance.IsRunning())
        {
            return;
        }

        // Calculate the normalized direction vector toward the target and move
        // the enemy using world coordinates. Using Space.World keeps pursuit
        // behaviour consistent regardless of the enemy's own rotation.
        Vector3 dir = (player.position - transform.position).normalized;
        transform.Translate(dir * speed * Time.deltaTime, Space.World);
    }
}
