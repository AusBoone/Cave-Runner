using NUnit.Framework;
using UnityEngine;
using System.Reflection;

/// <summary>
/// Tests for the PowerUpSpawner component verifying pools are
/// created and reused correctly.
/// </summary>
public class PowerUpSpawnerTests
{
    [Test]
    public void Start_CreatesPoolsForPrefabs()
    {
        var spawnerObj = new GameObject("spawner");
        var spawner = spawnerObj.AddComponent<PowerUpSpawner>();
        spawner.usePooling = true;
        var prefab1 = new GameObject("prefab1");
        var prefab2 = new GameObject("prefab2");
        spawner.powerUpPrefabs = new[] { prefab1, prefab2 };

        spawner.Start();

        var poolsField = typeof(PowerUpSpawner).GetField("pools", BindingFlags.NonPublic | BindingFlags.Instance);
        var pools = (System.Collections.Generic.Dictionary<GameObject, ObjectPool>)poolsField.GetValue(spawner);
        Assert.AreEqual(2, pools.Count);

        Object.DestroyImmediate(prefab1);
        Object.DestroyImmediate(prefab2);
        Object.DestroyImmediate(spawnerObj);
    }

    [Test]
    public void SpawnPowerUp_ReusesInstanceFromPool()
    {
        var spawnerObj = new GameObject("spawner");
        var spawner = spawnerObj.AddComponent<PowerUpSpawner>();
        spawner.usePooling = true;
        var prefab = new GameObject("prefab");
        spawner.powerUpPrefabs = new[] { prefab };
        spawner.spawnInterval = 1f;

        // Create pool with a single object
        spawner.Start();
        var poolsField = typeof(PowerUpSpawner).GetField("pools", BindingFlags.NonPublic | BindingFlags.Instance);
        var pools = (System.Collections.Generic.Dictionary<GameObject, ObjectPool>)poolsField.GetValue(spawner);
        var pool = pools[prefab];
        pool.initialSize = 1;
        pool.Start();

        // Spawn a power-up then return it
        var spawnMethod = typeof(PowerUpSpawner).GetMethod("SpawnPowerUp", BindingFlags.NonPublic | BindingFlags.Instance);
        spawnMethod.Invoke(spawner, null);
        var spawned = pool.transform.GetChild(0).gameObject;
        pool.ReturnObject(spawned);

        // Spawn again and ensure the same instance was reused
        spawnMethod.Invoke(spawner, null);
        var spawnedAgain = pool.transform.GetChild(0).gameObject;
        Assert.AreSame(spawned, spawnedAgain);

        Object.DestroyImmediate(prefab);
        Object.DestroyImmediate(spawnerObj);
    }
}
