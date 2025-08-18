using UnityEngine;
using System.Collections.Generic;

/*
 * MODIFICATION SUMMARY:
 * Added explicit validation and developer-facing warnings for missing prefabs.
 * Start now emits a warning instead of failing silently, leaving the pool empty
 * when no prefab is configured. GetObject also warns and returns null if invoked
 * without a prefab to prevent confusing null reference errors during gameplay.
 */

/// <summary>
/// Simple object pooling component that reuses inactive instances of a prefab
/// to avoid expensive <c>Instantiate</c>/<c>Destroy</c> calls. A prefab must be
/// assigned; otherwise the pool remains empty and will warn when misconfigured.
///
/// <para>Example usage:</para>
/// <code>
/// var projectile = projectilePool.GetObject(position, rotation);
/// // ... use projectile ...
/// projectilePool.ReturnObject(projectile);
/// </code>
///
/// <para>Assumptions:</para>
/// <list type="bullet">
/// <item>The assigned prefab contains all required components for spawned objects.</item>
/// <item>Consumers handle a <c>null</c> return from <see cref="GetObject"/> when the pool is misconfigured.</item>
/// </list>
/// </summary>
public class ObjectPool : MonoBehaviour
{
    /// <summary>
    /// Prefab that will be instantiated when the pool needs more objects.
    /// </summary>
    public GameObject prefab;

    /// <summary>
    /// Number of instances created on <c>Start</c> so the pool has
    /// objects ready before gameplay begins.
    /// </summary>
    public int initialSize = 5;

    private Queue<PooledObject> objects = new Queue<PooledObject>();

    // Delay initialization until Start so prefab can be assigned by spawners
    // before the pool creates its initial objects.
    /// <summary>
    /// Instantiates a set number of objects at start so the pool has
    /// instances ready for immediate use.
    /// </summary>
    void Start()
    {
        // Validate pool configuration before preloading objects. If no prefab is
        // supplied the pool cannot create instances. Rather than failing silently,
        // warn the developer so the misconfiguration is obvious and the pool
        // remains empty until fixed.
        if (prefab == null)
        {
            Debug.LogWarning($"{nameof(ObjectPool)} on {name} has no prefab assigned; no objects were preloaded.");
        }
        else
        {
            for (int i = 0; i < initialSize; i++)
            {
                CreateNew();
            }
        }
    }

    /// <summary>
    /// Creates a new pooled instance of the prefab and adds it to the
    /// internal queue.
    /// </summary>
    PooledObject CreateNew()
    {
        // Instantiate a new object and parent it under the pool so the
        // hierarchy stays clean in the editor.
        GameObject obj = Instantiate(prefab, transform);
        obj.SetActive(false);
        PooledObject po = obj.AddComponent<PooledObject>();
        po.Pool = this;
        objects.Enqueue(po);
        return po;
    }

    /// <summary>
    /// Retrieves an instance from the pool, expanding it if necessary.
    /// The object is positioned, activated and returned.
    /// </summary>
    public GameObject GetObject(Vector3 position, Quaternion rotation)
    {
        // Guard against requests when the pool lacks a configured prefab. Logging
        // a warning and returning null clearly communicates the issue and avoids
        // a potential null reference exception in callers.
        if (prefab == null)
        {
            Debug.LogWarning($"{nameof(ObjectPool)} on {name} cannot spawn because prefab is not assigned.");
            return null;
        }

        if (objects.Count == 0)
        {
            CreateNew();
        }

        PooledObject po = objects.Dequeue();
        GameObject obj = po.gameObject;
        obj.transform.SetPositionAndRotation(position, rotation);
        obj.SetActive(true);
        return obj;
    }

    /// <summary>
    /// Deactivates an object and puts it back into the pool for later
    /// reuse.
    /// </summary>
    public void ReturnObject(GameObject obj)
    {
        // Disable the object and parent it back under this pool so
        // inactive instances remain organized.
        obj.SetActive(false);
        obj.transform.SetParent(transform);
        PooledObject po = obj.GetComponent<PooledObject>();
        if (po != null)
        {
            objects.Enqueue(po);
        }
        else
        {
            Destroy(obj);
        }
    }
}
