using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Collections;
using System.Reflection;

/// <summary>
/// Tests targeting <see cref="CoinSpawner"/> to ensure coins spawn only while
/// the game is running and that pooled instances correctly reset their
/// transform state when reused.
/// </summary>
public class CoinSpawnerTests
{
    /// <summary>
    /// Minimal <see cref="GameManager"/> subclass used to control the running
    /// state during tests without invoking the production Awake logic which
    /// depends on many other systems.
    /// </summary>
    private class MockGameManager : GameManager
    {
        public new void Awake()
        {
            // Directly assign the singleton instance via reflection.
            typeof(GameManager).GetProperty("Instance", BindingFlags.Static | BindingFlags.Public)
                .SetValue(null, this, null);
        }

        public void SetRunning(bool running)
        {
            typeof(GameManager).GetField("isRunning", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(this, running);
        }

        public void SetDistance(float dist)
        {
            typeof(GameManager).GetField("distance", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(this, dist);
        }
    }

    /// <summary>
    /// Ensures <see cref="CoinSpawner"/> performs no spawning when the game is
    /// not running, but instantiates a coin once <see cref="GameManager"/>
    /// reports an active run.
    /// </summary>
    [UnityTest]
    public IEnumerator Update_SpawnsOnlyWhenRunning()
    {
        // Create and configure the mock GameManager.
        var gmObj = new GameObject("gm");
        var gm = gmObj.AddComponent<MockGameManager>();
        gm.Awake();
        gm.SetRunning(false); // start with game stopped
        gm.SetDistance(0f);

        // Prepare the spawner with a single coin prefab and pooling enabled.
        var spawnerObj = new GameObject("spawner");
        var spawner = spawnerObj.AddComponent<CoinSpawner>();
        spawner.usePooling = true;
        spawner.coinPrefabs = new[] { new GameObject("coin") };
        spawner.spawnInterval = 0.01f;

        // Manually invoke Start so the internal pools are created.
        typeof(CoinSpawner).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(spawner, null);

        // Run Update with the game stopped; no coins should spawn.
        typeof(CoinSpawner).GetField("timer", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(spawner, 0f); // force spawn attempt
        typeof(CoinSpawner).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(spawner, null);
        Assert.IsNull(GameObject.Find("coin(Clone)"),
            "Coin spawned despite game not running");

        // Now mark the game as running and try again.
        gm.SetRunning(true);
        gm.SetDistance(10f);
        typeof(CoinSpawner).GetField("timer", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(spawner, 0f);
        typeof(CoinSpawner).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(spawner, null);
        Assert.IsNotNull(GameObject.Find("coin(Clone)"),
            "Coin failed to spawn when game running");

        Object.DestroyImmediate(gmObj);
        Object.DestroyImmediate(spawner.coinPrefabs[0]);
        Object.DestroyImmediate(spawnerObj);
    }

    /// <summary>
    /// Verifies that coins retrieved from an <see cref="ObjectPool"/> have their
    /// position reset when reused, preventing stale transform data from previous
    /// activations from leaking into new spawns.
    /// </summary>
    [UnityTest]
    public IEnumerator PooledCoin_ResetsPositionOnReuse()
    {
        var gmObj = new GameObject("gm");
        var gm = gmObj.AddComponent<MockGameManager>();
        gm.Awake();
        gm.SetRunning(true);
        gm.SetDistance(0f);

        var spawnerObj = new GameObject("spawner");
        var spawner = spawnerObj.AddComponent<CoinSpawner>();
        spawner.usePooling = true;
        spawner.coinPrefabs = new[] { new GameObject("coin") };
        spawner.spawnInterval = 0.01f;
        spawner.spawnX = 1f;
        spawner.minY = spawner.maxY = 1f; // deterministic position

        typeof(CoinSpawner).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(spawner, null);

        // Spawn first coin and move it somewhere unexpected.
        typeof(CoinSpawner).GetField("timer", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(spawner, 0f);
        typeof(CoinSpawner).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(spawner, null);
        var coin = GameObject.Find("coin(Clone)");
        coin.transform.position = new Vector3(5f, 5f, 0f);

        // Return the coin to the pool.
        coin.GetComponent<PooledObject>().Pool.ReturnObject(coin);

        // Spawn again at a different configured location.
        spawner.spawnX = 2f;
        spawner.minY = spawner.maxY = 2f;
        typeof(CoinSpawner).GetField("timer", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(spawner, 0f);
        typeof(CoinSpawner).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(spawner, null);
        var coin2 = GameObject.Find("coin(Clone)");
        Assert.AreEqual(new Vector3(2f, 2f, 0f), coin2.transform.position,
            "Pooled coin did not reset to new spawn position");

        Object.DestroyImmediate(gmObj);
        Object.DestroyImmediate(spawner.coinPrefabs[0]);
        Object.DestroyImmediate(spawnerObj);
    }
}

