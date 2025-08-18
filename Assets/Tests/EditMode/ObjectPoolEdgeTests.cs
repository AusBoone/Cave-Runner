using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Reflection;

/// <summary>
/// Additional unit tests for <see cref="ObjectPool"/> covering less common
/// scenarios. These tests verify that pools expand when depleted, reuse
/// returned instances and gracefully handle missing prefabs by emitting
/// clear warnings instead of failing silently.
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
        Assert.AreEqual(2, pool.transform.childCount,
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
}
