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

    void Start()
    {
        GameObject obj = GameObject.FindGameObjectWithTag("Player");
        if (obj != null)
        {
            player = obj.transform;
        }
    }

    void Update()
    {
        if (player == null) return;
        if (GameManager.Instance == null || !GameManager.Instance.IsRunning()) return;

        Vector3 dir = (player.position - transform.position).normalized;
        transform.Translate(dir * speed * Time.deltaTime);
    }
}
