using UnityEngine;
using System.Collections.Generic;

/*
 * MODIFICATION SUMMARY:
 * Added explicit validation and developer-facing warnings for missing prefabs.
 * Start now emits a warning instead of failing silently, leaving the pool empty
 * when no prefab is configured. GetObject also warns and returns null if invoked
 * without a prefab to prevent confusing null reference errors during gameplay.
 * ReturnObject now validates ownership of returned instances, warning and
 * destroying any foreign objects to prevent cross-pool corruption.
 * CreateNew now reuses a prefab-defined PooledObject component when present,
 * avoiding duplicate components and clarifying that prefabs may include the
 * component for debugging or customised setup.
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
    /// Prefabs may include a <see cref="PooledObject"/> component for debugging
    /// or custom initialisation; if so, it is reused rather than duplicated.
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
    ///
    /// <para>Some prefabs may already include a <see cref="PooledObject"/>
    /// component for debugging or custom setup. In that case the existing
    /// component is reused rather than blindly adding another one, which
    /// would result in duplicate tracking components and confusing pool
    /// behaviour.</para>
    /// </summary>
    PooledObject CreateNew()
    {
        // Instantiate a new object and parent it under the pool so the
        // hierarchy stays clean in the editor.
        GameObject obj = Instantiate(prefab, transform);
        obj.SetActive(false);

        // Check if the instantiated object already contains a PooledObject
        // component. Prefabs can define it ahead of time to perform custom
        // setup or to aid debugging in the editor. Only add a new component
        // when one is missing so we avoid duplicating components and
        // corrupting the pool's bookkeeping.
        PooledObject po = obj.GetComponent<PooledObject>();
        if (po == null)
        {
            po = obj.AddComponent<PooledObject>();
        }

        // Record which pool this instance belongs to so it can be returned
        // correctly after use.
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
    /// Deactivates an object and attempts to put it back into the pool for
    /// later reuse. Only objects originally spawned by this pool are
    /// accepted; foreign objects are destroyed to avoid corrupting the pool's
    /// queue with unexpected instances.
    /// </summary>
    public void ReturnObject(GameObject obj)
    {
        // Disable the object before any further processing so behaviour
        // scripts cease immediately.
        obj.SetActive(false);

        PooledObject po = obj.GetComponent<PooledObject>();

        // Ensure the object actually belongs to this pool before enqueuing it.
        // Without this ownership check, a stray object from another pool could
        // be added, leading to double-use or destruction of the wrong instance
        // when the queue is later serviced.
        if (po != null && po.Pool == this)
        {
            // Safe to reparent and queue because the object originated from this
            // pool. Reparenting keeps inactive instances organised under the pool
            // in the hierarchy view.
            obj.transform.SetParent(transform);
            objects.Enqueue(po);
        }
        else
        {
            // Warn developers about the misuse and destroy the object to keep
            // the pool's internal state consistent and free of foreign entries.
            Debug.LogWarning($"{nameof(ObjectPool)} on {name} received an object that does not belong to this pool; destroying to maintain integrity.");
            Destroy(obj);
        }
    }
}
