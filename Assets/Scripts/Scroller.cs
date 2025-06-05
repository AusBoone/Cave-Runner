using UnityEngine;

/// <summary>
/// Moves an object leftwards at the current game speed and recycles it
/// via a <see cref="PooledObject"/> when it exits the screen.
/// </summary>
public class Scroller : MonoBehaviour
{
    private PooledObject pooledObject;

    /// <summary>
    /// Cache the optional <see cref="PooledObject"/> component.
    /// </summary>
    void Awake()
    {
        pooledObject = GetComponent<PooledObject>();
    }

    /// <summary>
    /// Scrolls left every frame and destroys or returns the object when
    /// it moves beyond a threshold.
    /// </summary>
    void Update()
    {
        float speed = GameManager.Instance != null ? GameManager.Instance.GetSpeed() : 5f;
        transform.Translate(Vector3.left * speed * Time.deltaTime);
        if (transform.position.x < -20f)
        {
            if (pooledObject != null && pooledObject.Pool != null)
            {
                pooledObject.Pool.ReturnObject(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}
