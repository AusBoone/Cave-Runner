using UnityEngine;
using System.Collections;
using System.Collections.Generic;

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

    /// <summary>
    /// Maximum number of objects the pool is allowed to create. A value of
    /// <c>0</c> disables the limit and allows unbounded growth. When the cap is
    /// reached, requests for additional objects will log a warning and return
    /// <c>null</c>.
    /// </summary>
    public int maxSize = 0;

    private Queue<PooledObject> objects = new Queue<PooledObject>();

    /// <summary>
    /// Tracks how many objects this pool has created that still exist.
    /// <para>
    /// Using a dedicated counter instead of relying on
    /// <c>transform.childCount</c> ensures that only legitimate pooled
    /// instances are considered when enforcing <see cref="maxSize"/>,
    /// avoiding interference from unrelated children under the pool's
    /// transform.
    /// </para>
    /// </summary>
    private int pooledInstanceCount = 0;

    /// <summary>
    /// Read-only access for tests and diagnostics to see how many pooled
    /// instances currently exist. External systems should not modify the
    /// counter directly.
    /// </summary>
    public int PooledInstanceCount => pooledInstanceCount;

    // Delay initialization until Start so prefab can be assigned by spawners
    // before the pool creates its initial objects.
    /// <summary>
    /// Instantiates a set number of objects at start so the pool has
    /// instances ready for immediate use. Objects are created over multiple
    /// frames to avoid long hitches during scene load.
    /// </summary>
    void Start()
    {
        // Validate pool configuration before preloading objects. If no prefab is
        // supplied the pool cannot create instances. Rather than failing silently,
        // warn the developer so the misconfiguration is obvious and the pool
        // remains empty until fixed.
        if (prefab == null)
        {
            LoggingHelper.LogWarning($"{nameof(ObjectPool)} on {name} has no prefab assigned; no objects were preloaded."); // Warn through helper so gating applies.
            return;
        }

        // Kick off asynchronous preloading. The coroutine yields after each
        // instantiation so that heavy creation work is spread across frames and
        // does not stall gameplay or editor responsiveness.
        StartCoroutine(PreloadCoroutine());
    }

    /// <summary>
    /// Coroutine responsible for lazily creating the initial pool objects. It
    /// yields after each instantiation so the work is amortised across frames.
    /// </summary>
    IEnumerator PreloadCoroutine()
    {
        // Determine how many objects we are allowed to preload. Honour the
        // maxSize limit if one is set; otherwise fall back to initialSize.
        int target = initialSize;
        if (maxSize > 0)
        {
            target = Mathf.Min(initialSize, maxSize);
        }

        for (int i = 0; i < target; i++)
        {
            // Stop preloading early if CreateNew refuses due to size limits.
            if (CreateNew() == null)
            {
                yield break;
            }

            // Wait a frame before creating the next object so we avoid frame
            // spikes from bulk instantiation.
            yield return null;
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
        // Enforce the optional maxSize limit using the dedicated counter so
        // only objects created by this pool contribute to the limit. This
        // avoids issues where unrelated children under the transform would
        // otherwise block expansion.
        if (maxSize > 0 && pooledInstanceCount >= maxSize)
        {
            LoggingHelper.LogWarning($"{nameof(ObjectPool)} on {name} cannot expand beyond max size of {maxSize}."); // Helper maintains consistent warning policy.
            return null;
        }

        // Instantiate a new object and parent it under the pool so the
        // hierarchy stays clean in the editor.
        GameObject obj = Instantiate(prefab, transform);
        obj.SetActive(false);

        // Update our internal counter now that a new pooled object exists. The
        // decrement occurs in <see cref="OnPooledObjectDestroyed"/> when the
        // instance is destroyed.
        pooledInstanceCount++;

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
            LoggingHelper.LogWarning($"{nameof(ObjectPool)} on {name} cannot spawn because prefab is not assigned."); // Uses helper to respect verbosity flag.
            return null;
        }

        PooledObject po;
        if (objects.Count == 0)
        {
            // Attempt to expand the pool if all instances are in use. When
            // maxSize is reached CreateNew will return null, signalling to the
            // caller that no object is available.
            po = CreateNew();
            if (po == null)
            {
                return null;
            }
        }
        else
        {
            po = objects.Dequeue();
        }
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
            LoggingHelper.LogWarning($"{nameof(ObjectPool)} on {name} received an object that does not belong to this pool; destroying to maintain integrity."); // Central logging for integrity warnings.
            Destroy(obj);
        }
    }

    /// <summary>
    /// Called by <see cref="PooledObject"/> when a pooled instance is
    /// destroyed instead of returned. This keeps the internal counter
    /// accurate so the pool can spawn replacements if needed.
    /// </summary>
    /// <param name="po">The pooled object being destroyed.</param>
    internal void OnPooledObjectDestroyed(PooledObject po)
    {
        // Reduce the count but ensure it never drops below zero in case the
        // notification is sent unexpectedly.
        if (pooledInstanceCount > 0)
        {
            pooledInstanceCount--;
        }

        // Remove the object from the queue if it was waiting there to avoid
        // stale references. Create a new queue to filter out the destroyed
        // instance while preserving ordering.
        if (objects.Contains(po))
        {
            var remaining = new Queue<PooledObject>(objects.Count);
            while (objects.Count > 0)
            {
                var item = objects.Dequeue();
                if (item != po)
                {
                    remaining.Enqueue(item);
                }
            }
            objects = remaining;
        }
    }
}
