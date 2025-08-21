using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Reflection;

/// <summary>
/// Additional unit tests for <see cref="ObjectPool"/> covering less common
/// scenarios. These tests verify that pools expand when depleted, reuse
/// returned instances, reject foreign objects that do not belong to the pool,
/// gracefully handle missing prefabs by emitting clear warnings instead of
/// failing silently, and respect PooledObject components already defined on
/// prefabs without duplicating them.
/// </summary>
public class ObjectPoolEdgeTests
{
    [Test]
    public void GetObject_ExpandsWhenDepleted()
    {
        // Pool starts with a single instance so requesting two should
        // automatically create an additional object.
        var poolGO = new GameObject("pool");
        var pool = poolGO.AddComponent<ObjectPool>();
        pool.prefab = new GameObject("prefab");
        pool.initialSize = 1;

        // Manually populate initial objects by invoking Start.
        typeof(ObjectPool).GetMethod("Start", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(pool, null);

        // Dequeue the existing instance then request another.
        var first = pool.GetObject(Vector3.zero, Quaternion.identity);
        var second = pool.GetObject(Vector3.one, Quaternion.identity);

        // Two children under the pool indicates it expanded.
        Assert.AreEqual(2, pool.PooledInstanceCount,
            "Pool should instantiate a new object when empty");

        Object.DestroyImmediate(first);
        Object.DestroyImmediate(second);
        Object.DestroyImmediate(pool.prefab);
        Object.DestroyImmediate(poolGO);
    }

    [Test]
    public void GetObject_ReturnsNullAndWarnsWhenPrefabMissing()
    {
        // Without a prefab assigned, the pool cannot create objects and should
        // warn developers so the configuration issue is obvious.
        var poolGO = new GameObject("pool");
        var pool = poolGO.AddComponent<ObjectPool>();

        // Expect the warning emitted by GetObject's validation.
        LogAssert.Expect(LogType.Warning,
            "ObjectPool on pool cannot spawn because prefab is not assigned.");

        var obj = pool.GetObject(Vector3.zero, Quaternion.identity);

        Assert.IsNull(obj, "GetObject should yield null when no prefab is set");
        Object.DestroyImmediate(poolGO);
    }

    [Test]
    public void Start_WarnsWhenPrefabMissing()
    {
        // Start should log a warning if the pool is initialized without a
        // prefab so developers catch the misconfiguration during setup.
        var poolGO = new GameObject("pool");
        var pool = poolGO.AddComponent<ObjectPool>();

        LogAssert.Expect(LogType.Warning,
            "ObjectPool on pool has no prefab assigned; no objects were preloaded.");

        // Invoke Start manually because EditMode tests do not automatically
        // run Unity lifecycle methods.
        typeof(ObjectPool).GetMethod("Start", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(pool, null);

        Object.DestroyImmediate(poolGO);
    }

    [Test]
    public void ReturnedObject_IsReused()
    {
        // After an object is returned it should be provided again on the
        // next request rather than instantiating a new one.
        var poolGO = new GameObject("pool");
        var pool = poolGO.AddComponent<ObjectPool>();
        pool.prefab = new GameObject("prefab");

        var first = pool.GetObject(Vector3.zero, Quaternion.identity);
        pool.ReturnObject(first);
        var second = pool.GetObject(Vector3.zero, Quaternion.identity);

        Assert.AreSame(first, second, "Returned instances must be reused");
        Object.DestroyImmediate(first);
        Object.DestroyImmediate(pool.prefab);
        Object.DestroyImmediate(poolGO);
    }

    [Test]
    public void ReturnObject_ForeignInstanceIsDestroyed()
    {
        // Returning an object from a different pool should log a warning and
        // destroy the object so the pool remains free of unexpected entries.
        var poolAGO = new GameObject("poolA");
        var poolA = poolAGO.AddComponent<ObjectPool>();
        poolA.prefab = new GameObject("prefabA");

        var foreign = poolA.GetObject(Vector3.zero, Quaternion.identity);

        var poolBGO = new GameObject("poolB");
        var poolB = poolBGO.AddComponent<ObjectPool>();
        poolB.prefab = new GameObject("prefabB");

        // Expect a warning indicating the object did not originate from poolB.
        LogAssert.Expect(LogType.Warning,
            "ObjectPool on poolB received an object that does not belong to this pool; destroying to maintain integrity.");

        poolB.ReturnObject(foreign);

        // PoolB should spawn its own instance rather than reusing the foreign one.
        var own = poolB.GetObject(Vector3.zero, Quaternion.identity);
        Assert.AreNotSame(foreign, own, "Foreign objects must not be enqueued");

        // Clean up all created objects to avoid polluting subsequent tests.
        Object.DestroyImmediate(foreign);
        Object.DestroyImmediate(own);
        Object.DestroyImmediate(poolA.prefab);
        Object.DestroyImmediate(poolB.prefab);
        Object.DestroyImmediate(poolAGO);
        Object.DestroyImmediate(poolBGO);
    }

    [Test]
    public void PrefabWithExistingPooledObject_IsNotDuplicated()
    {
        // Some prefabs may already carry a PooledObject component for custom
        // initialisation or debugging. The pool should reuse this component
        // instead of adding an extra one, which could corrupt bookkeeping.
        var poolGO = new GameObject("pool");
        var pool = poolGO.AddComponent<ObjectPool>();

        var prefab = new GameObject("prefab");
        prefab.AddComponent<PooledObject>();
        pool.prefab = prefab;

        // Request an object which will be created from the prefab carrying the
        // PooledObject component.
        var obj = pool.GetObject(Vector3.zero, Quaternion.identity);

        // Ensure only a single PooledObject component exists on the instance.
        Assert.AreEqual(1, obj.GetComponents<PooledObject>().Length,
            "Prefab-defined PooledObject should be reused, not duplicated");

        Object.DestroyImmediate(obj);
        Object.DestroyImmediate(pool.prefab);
        Object.DestroyImmediate(poolGO);
    }

    /// <summary>
    /// Ensures that unrelated children under the pool's transform do not
    /// contribute to <see cref="ObjectPool.maxSize"/>. Only objects created by
    /// the pool should affect the cap, allowing designers to organise helper
    /// objects under the pool without blocking instantiation.
    /// </summary>
    [Test]
    public void MaxSize_IgnoresNonPooledChildren()
    {
        var poolGO = new GameObject("pool");
        var pool = poolGO.AddComponent<ObjectPool>();
        pool.prefab = new GameObject("prefab");
        pool.maxSize = 1;

        // Add an unrelated child object to simulate design-time helpers or
        // markers that should not count toward pooled instance limits.
        var helper = new GameObject("helper");
        helper.transform.SetParent(poolGO.transform);

        // Request an object; despite the extra child the pool should still
        // instantiate because only pooled objects are counted.
        var obj = pool.GetObject(Vector3.zero, Quaternion.identity);
        Assert.IsNotNull(obj, "Pooled object should be created even with foreign children present");

        Object.DestroyImmediate(obj);
        Object.DestroyImmediate(helper);
        Object.DestroyImmediate(pool.prefab);
        Object.DestroyImmediate(poolGO);
    }

    /// <summary>
    /// Verifies that destroying a pooled instance outside of
    /// <see cref="ObjectPool.ReturnObject"/> properly decrements the internal
    /// counter so the pool can create replacements when under its
    /// <see cref="ObjectPool.maxSize"/> limit.
    /// </summary>
    [Test]
    public void DestroyedInstance_DecrementsCounter()
    {
        var poolGO = new GameObject("pool");
        var pool = poolGO.AddComponent<ObjectPool>();
        pool.prefab = new GameObject("prefab");
        pool.maxSize = 1;

        // Create and immediately destroy an instance to simulate external
        // destruction without returning to the pool.
        var obj = pool.GetObject(Vector3.zero, Quaternion.identity);
        Object.DestroyImmediate(obj);

        // The counter should reflect that no pooled instances remain.
        Assert.AreEqual(0, pool.PooledInstanceCount,
            "Counter must decrement when pooled instance is destroyed");

        // With the count reduced, requesting another object should succeed.
        var replacement = pool.GetObject(Vector3.zero, Quaternion.identity);
        Assert.IsNotNull(replacement,
            "Pool should spawn replacement after destruction frees slot");

        Object.DestroyImmediate(replacement);
        Object.DestroyImmediate(pool.prefab);
        Object.DestroyImmediate(poolGO);
    }
}
