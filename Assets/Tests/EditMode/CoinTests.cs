using NUnit.Framework;
using UnityEngine;

public class CoinTests
{
    [Test]
    public void CollectingCoinAddsCoinsAndReturnsToPool()
    {
        // Set up GameManager instance
        var gmObj = new GameObject("gm");
        var gm = gmObj.AddComponent<GameManager>();
        gm.StartGame();

        // Set up pool and coin
        var poolObj = new GameObject("pool");
        var pool = poolObj.AddComponent<ObjectPool>();
        pool.prefab = new GameObject("prefab");

        var coinObj = new GameObject("coin");
        var coin = coinObj.AddComponent<Coin>();
        var pooled = coinObj.AddComponent<PooledObject>();
        pooled.Pool = pool;
        var coinCollider = coinObj.AddComponent<BoxCollider2D>();
        coinCollider.isTrigger = true;

        // Create player collider
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
        PlayerPrefs.SetInt("HighScore", 5);
        var gmObj = new GameObject("gm");
        var gm = gmObj.AddComponent<GameManager>();
        gm.StartGame();

        // Set internal distance via reflection
        var field = typeof(GameManager).GetField("distance", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field.SetValue(gm, 10f);

        gm.GameOver();

        Assert.AreEqual(10, PlayerPrefs.GetInt("HighScore"));
        Object.DestroyImmediate(gmObj);
    }
}
