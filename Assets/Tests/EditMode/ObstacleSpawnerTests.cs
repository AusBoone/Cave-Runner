using NUnit.Framework;
using UnityEngine;
using System.Reflection;
using System.Collections.Generic;

/// <summary>
/// Tests for <see cref="ObstacleSpawner"/> confirming that spawn
/// rates respond to stage multipliers and that pooled objects are
/// reused rather than instantiated each time.
/// </summary>
public class ObstacleSpawnerTests
{
    [Test]
    public void Update_UsesSpawnMultiplierForTimer()
    {
        // GameManager must report the game is running for Update to spawn.
        var gmObj = new GameObject("gm");
        var gm = gmObj.AddComponent<GameManager>();
        typeof(GameManager).GetField("isRunning", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(gm, true);

        // Spawner with a single ground obstacle.
        var spawnerObj = new GameObject("spawner");
        var spawner = spawnerObj.AddComponent<ObstacleSpawner>();
        spawner.usePooling = false;
        spawner.groundObstacles = new[] { new GameObject("prefab") };
        spawner.spawnInterval = 1f;
        spawner.spawnMultiplier = 2f; // expect faster spawns
        spawner.spawnRateCurve = AnimationCurve.Constant(0f, 1f, 1f);

        // Force the timer to trigger spawning immediately.
        typeof(ObstacleSpawner).GetField("timer", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(spawner, 0f);

        spawner.Update();

        float timer = (float)typeof(ObstacleSpawner).GetField("timer", BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(spawner);

        // With multiplier 2 the next spawn should occur in half the interval.
        Assert.AreEqual(0.5f, timer, 0.0001f,
            "Spawn timer should be divided by the multiplier when greater than 1");

        Object.DestroyImmediate(spawner.groundObstacles[0]);
        Object.DestroyImmediate(spawnerObj);
        Object.DestroyImmediate(gmObj);
    }

    [Test]
    public void Spawn_UsesObjectPoolWhenEnabled()
    {
        // Setup running GameManager like before.
        var gmObj = new GameObject("gm");
        var gm = gmObj.AddComponent<GameManager>();
        typeof(GameManager).GetField("isRunning", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(gm, true);

        var spawnerObj = new GameObject("spawner");
        var spawner = spawnerObj.AddComponent<ObstacleSpawner>();
        spawner.usePooling = true;

        var prefab = new GameObject("prefab");
        spawner.groundObstacles = new[] { prefab };

        // Initialize internal object pools
        typeof(ObstacleSpawner).GetMethod("Start", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(spawner, null);
        var poolsField = typeof(ObstacleSpawner).GetField("pools", BindingFlags.NonPublic | BindingFlags.Instance);
        var pools = (Dictionary<GameObject, ObjectPool>)poolsField.GetValue(spawner);
        var pool = pools[prefab];
        pool.initialSize = 1;
        typeof(ObjectPool).GetMethod("Start", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(pool, null);

        // Invoke spawn and ensure an instance from the pool was used.
        typeof(ObstacleSpawner).GetMethod("Spawn", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(spawner, null);
        var spawned = pool.transform.GetChild(0).gameObject;

        Assert.IsTrue(spawned.activeSelf, "Object should be activated when spawned from the pool");
        Assert.AreEqual(pool.transform, spawned.transform.parent, "Spawned instance should remain parented to its pool");

        Object.DestroyImmediate(prefab);
        Object.DestroyImmediate(spawnerObj);
        Object.DestroyImmediate(gmObj);
    }
}
