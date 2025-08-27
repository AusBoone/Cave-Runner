// ShooterEnemy.cs
// -----------------------------------------------------------------------------
// Enemy that moves left at a constant speed and periodically fires projectiles.
// When used with object pooling the shooting timer resets on enable/disable so
// pooled instances behave consistently.
// -----------------------------------------------------------------------------
using UnityEngine;

/// <summary>
/// Flying enemy that shoots <see cref="EnemyProjectile"/> objects at regular
/// intervals. Optionally uses an <see cref="ObjectPool"/> for the projectiles to
/// avoid instantiation overhead.
/// </summary>
public class ShooterEnemy : MonoBehaviour
{
    [Tooltip("Horizontal movement speed in units per second.")]
    public float speed = 2f;

    [Tooltip("Prefab of the projectile this enemy fires.")]
    public GameObject projectilePrefab;

    [Tooltip("Time in seconds between shots.")]
    public float shootInterval = 2f;

    [Tooltip("Whether to reuse projectile instances using an ObjectPool.")]
    public bool usePooling = true;

    private float shootTimer;
    private ObjectPool projectilePool;

    void Awake()
    {
        if (usePooling && projectilePrefab != null)
        {
            GameObject obj = new GameObject(projectilePrefab.name + "_Pool");
            obj.transform.SetParent(transform);
            projectilePool = obj.AddComponent<ObjectPool>();
            projectilePool.prefab = projectilePrefab;
        }
    }

    void OnEnable()
    {
        shootTimer = shootInterval;
    }

    void OnDisable()
    {
        shootTimer = shootInterval;
    }

    void Update()
    {
        if (GameManager.Instance == null || !GameManager.Instance.IsRunning())
        {
            return;
        }

        transform.Translate(Vector3.left * speed * Time.deltaTime);

        shootTimer -= Time.deltaTime;
        if (shootTimer <= 0f && projectilePrefab != null)
        {
            SpawnProjectile();
            shootTimer = shootInterval;
        }
    }

    /// <summary>
    /// Spawns or retrieves a projectile and positions it at the enemy's
    /// current location.
    /// </summary>
    private void SpawnProjectile()
    {
        Vector3 pos = transform.position;
        if (usePooling && projectilePool != null)
        {
            projectilePool.GetObject(pos, Quaternion.identity);
        }
        else
        {
            Instantiate(projectilePrefab, pos, Quaternion.identity);
        }
    }
}
