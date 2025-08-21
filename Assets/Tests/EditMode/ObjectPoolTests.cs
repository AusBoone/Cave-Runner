using NUnit.Framework;
using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Simple unit tests for the ObjectPool class to ensure instances are
/// reused correctly. Run through Unity's EditMode testing framework.
/// </summary>

// EditMode tests can be run through Unity's Test Runner window (Window > General > Test Runner).
public class ObjectPoolTests
{
    [Test]
    public void GetAndReturn_ReusesInstance()
    {
        // Create a simple pool with a dummy prefab
        var poolGO = new GameObject("pool");
        var pool = poolGO.AddComponent<ObjectPool>();
        pool.prefab = new GameObject("prefab");

        // Fetch an instance then return it to the pool
        var first = pool.GetObject(Vector3.zero, Quaternion.identity);
        pool.ReturnObject(first);
        var second = pool.GetObject(Vector3.one, Quaternion.identity);

        // The same instance should be reused after being returned
        Assert.AreSame(first, second);
        Object.DestroyImmediate(first);
        Object.DestroyImmediate(pool.prefab);
        Object.DestroyImmediate(poolGO);
    }

    /// <summary>
    /// Ensures the <see cref="ObjectPool.maxSize"/> cap prevents growth beyond
    /// the configured limit and that requests beyond the limit yield
    /// <c>null</c> with a developer-facing warning. This protects games from
    /// runaway instantiation when pools are misconfigured or objects are never
    /// returned.
    /// </summary>
    [Test]
    public void GetObject_ReturnsNullWhenExceedingMaxSize()
    {
        var poolGO = new GameObject("pool");
        var pool = poolGO.AddComponent<ObjectPool>();
        pool.prefab = new GameObject("prefab");
        pool.maxSize = 1; // Only one instance allowed in total

        // Manually invoke Start to initialise and preload according to the
        // coroutine. With maxSize set to one, only a single object will be
        // created.
        typeof(ObjectPool).GetMethod("Start", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .Invoke(pool, null);

        var first = pool.GetObject(Vector3.zero, Quaternion.identity);

        // The pool has reached its cap; requesting another instance should log
        // a warning and return null.
        LogAssert.Expect(LogType.Warning,
            "ObjectPool on pool cannot expand beyond max size of 1.");
        var second = pool.GetObject(Vector3.zero, Quaternion.identity);
        Assert.IsNull(second, "Pool should return null once max size is reached");

        Object.DestroyImmediate(first);
        Object.DestroyImmediate(pool.prefab);
        Object.DestroyImmediate(poolGO);
    }

    /// <summary>
    /// Verifies that preloading is spread across multiple frames by the
    /// coroutine started in <c>Start</c>. This avoids frame spikes during scene
    /// initialisation. The test advances the coroutine and ensures that objects
    /// appear gradually instead of all at once.
    /// </summary>
    [UnityTest]
    public IEnumerator Start_PreloadsOverMultipleFrames()
    {
        var poolGO = new GameObject("pool");
        var pool = poolGO.AddComponent<ObjectPool>();
        pool.prefab = new GameObject("prefab");
        pool.initialSize = 2; // Request two objects to observe multi-frame behaviour

        // Start the coroutine-driven preload.
        typeof(ObjectPool).GetMethod("Start", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .Invoke(pool, null);

        // Immediately after Start only the first object should exist because
        // the coroutine yields after each instantiation.
        Assert.AreEqual(1, pool.PooledInstanceCount,
            "First preload iteration should run immediately");

        // Allow one frame for the coroutine to continue and create the second object.
        yield return null;
        Assert.AreEqual(2, pool.PooledInstanceCount,
            "Second preload iteration should occur on following frame");

        Object.DestroyImmediate(pool.prefab);
        Object.DestroyImmediate(poolGO);
    }
}
