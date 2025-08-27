using NUnit.Framework;
using UnityEngine;
using System.Reflection;
using System.Collections.Generic;

/// <summary>
/// Tests for <see cref="HazardSpawner"/> ensuring spawn timing respects
/// the stage multiplier and that hazards are obtained from pools when
/// pooling is enabled.
/// </summary>
public class HazardSpawnerTests
{
    [Test]
    public void Update_UsesSpawnMultiplierForTimer()
    {
        // Create a running GameManager to satisfy the spawner's check.
        var gmObj = new GameObject("gm");
        var gm = gmObj.AddComponent<GameManager>();
        typeof(GameManager).GetField("isRunning", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(gm, true);

        var spawnerObj = new GameObject("spawner");
        var spawner = spawnerObj.AddComponent<HazardSpawner>();
        spawner.usePooling = false;
        spawner.pitPrefabs = new[] { new GameObject("prefab") };
        spawner.spawnInterval = 2f;
        spawner.spawnMultiplier = 0.5f; // slower spawns when below 1
        spawner.spawnRateCurve = AnimationCurve.Constant(0f, 1f, 1f);

        typeof(HazardSpawner).GetField("timer", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(spawner, 0f);

        spawner.Update();

        float timer = (float)typeof(HazardSpawner).GetField("timer", BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(spawner);

        // Multiplier below 1 should lengthen the interval: 2f / (1 * 0.5) = 4f
        Assert.AreEqual(4f, timer, 0.0001f,
            "Spawn timer should increase when multiplier is less than 1");

        Object.DestroyImmediate(spawner.pitPrefabs[0]);
        Object.DestroyImmediate(spawnerObj);
        Object.DestroyImmediate(gmObj);
    }

    [Test]
    public void SpawnHazard_UsesObjectPoolWhenEnabled()
    {
        var gmObj = new GameObject("gm");
        var gm = gmObj.AddComponent<GameManager>();
        typeof(GameManager).GetField("isRunning", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(gm, true);

        var spawnerObj = new GameObject("spawner");
        var spawner = spawnerObj.AddComponent<HazardSpawner>();
        spawner.usePooling = true;

        var prefab = new GameObject("prefab");
        spawner.pitPrefabs = new[] { prefab };

        // Build object pools
        typeof(HazardSpawner).GetMethod("Start", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(spawner, null);
        var poolsField = typeof(HazardSpawner).GetField("pools", BindingFlags.NonPublic | BindingFlags.Instance);
        var pools = (Dictionary<GameObject, ObjectPool>)poolsField.GetValue(spawner);
        var pool = pools[prefab];
        pool.initialSize = 1;
        typeof(ObjectPool).GetMethod("Start", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(pool, null);

        // Spawn a hazard from the pool
        typeof(HazardSpawner).GetMethod("SpawnHazard", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(spawner, null);
        var spawned = pool.transform.GetChild(0).gameObject;

        Assert.IsTrue(spawned.activeSelf, "Hazard should be activated from the pool");
        Assert.AreEqual(pool.transform, spawned.transform.parent, "Spawned hazard should stay parented to its pool");

        Object.DestroyImmediate(prefab);
        Object.DestroyImmediate(spawnerObj);
        Object.DestroyImmediate(gmObj);
    }
}
