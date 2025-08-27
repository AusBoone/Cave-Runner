using NUnit.Framework;
using UnityEngine;
using System.Reflection;

/// <summary>
/// Tests for <see cref="PooledObject"/> verifying it notifies its owning
/// <see cref="ObjectPool"/> when destroyed and gracefully handles being
/// destroyed without an assigned pool.
/// </summary>
public class PooledObjectTests
{
    /// <summary>
    /// Creates a pool and pooled object then destroys the instance to ensure
    /// the pool's bookkeeping is updated and the queue no longer references
    /// the destroyed object.
    /// </summary>
    [Test]
    public void OnDestroy_NotifiesPoolAndRemovesObject()
    {
        // Build an ObjectPool with a manual entry in its internal queue so we
        // can observe the side effects of OnPooledObjectDestroyed.
        var poolObj = new GameObject("pool");
        var pool = poolObj.AddComponent<ObjectPool>();

        // Create the pooled object and associate it with the pool.
        var itemObj = new GameObject("item");
        var pooled = itemObj.AddComponent<PooledObject>();
        pooled.Pool = pool;

        // Populate the pool's private fields via reflection to simulate that it
        // created and tracked the pooled object.
        var queue = new Queue<PooledObject>();
        queue.Enqueue(pooled);
        typeof(ObjectPool).GetField("objects", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(pool, queue);
        typeof(ObjectPool).GetField("pooledInstanceCount", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(pool, 1);

        // Destroying the pooled object should invoke OnPooledObjectDestroyed,
        // decrementing the count and removing it from the queue.
        Object.DestroyImmediate(itemObj);

        var updatedQueue = (Queue<PooledObject>)typeof(ObjectPool)
            .GetField("objects", BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(pool);

        Assert.AreEqual(0, pool.PooledInstanceCount,
            "Pool should decrement its instance count when a pooled object is destroyed");
        Assert.AreEqual(0, updatedQueue.Count,
            "Destroyed pooled object should be removed from the pool's queue");

        Object.DestroyImmediate(poolObj);
    }

    /// <summary>
    /// Ensures destroying a PooledObject with no associated pool does not throw
    /// an exception. The component should quietly ignore the missing reference.
    /// </summary>
    [Test]
    public void OnDestroy_NoPool_DoesNotThrow()
    {
        var itemObj = new GameObject("item");
        itemObj.AddComponent<PooledObject>();

        // Destroying should not raise an exception even though the Pool field is null.
        Assert.DoesNotThrow(() => Object.DestroyImmediate(itemObj));
    }
}

