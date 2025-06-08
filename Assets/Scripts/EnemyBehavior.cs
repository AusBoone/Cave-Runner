using UnityEngine;

/// <summary>
/// Simple enemy AI that chases the player while the game is running.
/// Used for flying bats or other mobile hazards spawned by
/// <see cref="HazardSpawner"/>.
/// </summary>
public class EnemyBehavior : MonoBehaviour
{
    public float speed = 3f;

    private Transform player;

    /// <summary>
    /// Locates the player object so the enemy can chase it.
    /// </summary>
    void Start()
    {
        GameObject obj = GameObject.FindGameObjectWithTag("Player");
        if (obj != null)
        {
            player = obj.transform;
        }
    }

    /// <summary>
    /// Moves toward the player each frame while the game is running.
    /// </summary>
    void Update()
    {
        if (player == null) return;
        if (GameManager.Instance == null || !GameManager.Instance.IsRunning()) return;

        Vector3 dir = (player.position - transform.position).normalized;
        transform.Translate(dir * speed * Time.deltaTime);
    }
}
