using NUnit.Framework;
using UnityEngine;

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
}
