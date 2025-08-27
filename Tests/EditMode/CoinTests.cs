using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Basic edit mode tests verifying coin collection and high score logic.
/// These run inside the Unity Test Runner.
/// </summary>

public class CoinTests
{
    [Test]
    public void CollectingCoinAddsCoinsAndReturnsToPool()
    {
        // Set up GameManager instance to track coins
        var gmObj = new GameObject("gm");
        var gm = gmObj.AddComponent<GameManager>();
        gm.StartGame();

        // Create a pool and a coin that belongs to it
        var poolObj = new GameObject("pool");
        var pool = poolObj.AddComponent<ObjectPool>();
        pool.prefab = new GameObject("prefab");

        var coinObj = new GameObject("coin");
        var coin = coinObj.AddComponent<Coin>();
        var pooled = coinObj.AddComponent<PooledObject>();
        pooled.Pool = pool;
        var coinCollider = coinObj.AddComponent<BoxCollider2D>();
        coinCollider.isTrigger = true;

        // Fake player collider used to trigger collection
        var playerObj = new GameObject("player");
        playerObj.tag = "Player";
        var playerCollider = playerObj.AddComponent<BoxCollider2D>();

        coin.OnTriggerEnter2D(playerCollider);

        Assert.AreEqual(coin.value, gm.GetCoins());
        Assert.IsFalse(coinObj.activeSelf);

        Object.DestroyImmediate(pool.prefab);
        Object.DestroyImmediate(playerObj);
        Object.DestroyImmediate(coinObj);
        Object.DestroyImmediate(poolObj);
        Object.DestroyImmediate(gmObj);
    }

    [Test]
    public void GameOverUpdatesHighScore()
    {
        System.IO.File.Delete(System.IO.Path.Combine(
            Application.persistentDataPath, "savegame.json"));
        var saveObj = new GameObject("save");
        saveObj.AddComponent<SaveGameManager>();
        SaveGameManager.Instance.HighScore = 5;
        var gmObj = new GameObject("gm");
        var gm = gmObj.AddComponent<GameManager>();
        gm.StartGame();

        // Force the private distance field so GameOver records a known score
        var field = typeof(GameManager).GetField("distance", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field.SetValue(gm, 10f);

        gm.GameOver();

        Assert.AreEqual(10, SaveGameManager.Instance.HighScore);
        Object.DestroyImmediate(gmObj);
        Object.DestroyImmediate(saveObj);
    }
}
