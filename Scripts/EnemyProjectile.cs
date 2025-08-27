// EnemyProjectile.cs
// -----------------------------------------------------------------------------
// Simple projectile fired by ShooterEnemy. Moves horizontally until it exits the
// screen then returns to its pool or destroys itself. Movement stops when the
// game is paused because Time.deltaTime will be zero.
// -----------------------------------------------------------------------------
using UnityEngine;

/// <summary>
/// Straight moving projectile used by <see cref="ShooterEnemy"/>. The projectile
/// travels left at a constant speed and is automatically recycled when it leaves
/// the screen.
/// </summary>
public class EnemyProjectile : MonoBehaviour
{
    [Tooltip("Horizontal speed of the projectile in units per second.")]
    public float speed = 8f;

    private PooledObject pooled;

    void Awake()
    {
        pooled = GetComponent<PooledObject>();
    }

    void OnEnable()
    {
        // Ensure the projectile starts active without any previous velocity.
    }

    void Update()
    {
        if (GameManager.Instance != null && !GameManager.Instance.IsRunning())
        {
            return;
        }
        transform.Translate(Vector3.left * speed * Time.deltaTime);
        if (transform.position.x < -20f)
        {
            if (pooled != null && pooled.Pool != null)
            {
                pooled.Pool.ReturnObject(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}
