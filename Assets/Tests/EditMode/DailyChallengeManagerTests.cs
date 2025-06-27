using NUnit.Framework;
using UnityEngine;
using System.Reflection;

/// <summary>
/// Tests verifying generation and completion logic of <see cref="DailyChallengeManager"/>.
/// </summary>
public class DailyChallengeManagerTests
{
    [SetUp]
    public void ClearPrefs()
    {
        PlayerPrefs.DeleteAll();
    }

    [Test]
    public void GenerateChallenge_WhenPrefsMissing_CreatesNewEntry()
    {
        var go = new GameObject("dc");
        var dc = go.AddComponent<DailyChallengeManager>();

        Assert.IsTrue(PlayerPrefs.HasKey("DailyChallengeData"));
        string json = PlayerPrefs.GetString("DailyChallengeData");
        Assert.IsNotEmpty(json);

        Object.DestroyImmediate(go);
    }

    [Test]
    public void CompleteChallenge_RewardsCoins()
    {
        // Prepare a simple distance challenge in PlayerPrefs
        var state = new PrivateChallengeState
        {
            type = DailyChallengeManager.ChallengeType.Distance,
            target = 5,
            progress = 0,
            powerUp = DailyChallengeManager.PowerUpType.Magnet,
            expires = System.DateTime.UtcNow.AddDays(1).Ticks,
            completed = false
        };
        string json = JsonUtility.ToJson(state);
        PlayerPrefs.SetString("DailyChallengeData", json);

        // Game and shop instances to track coins
        var shopObj = new GameObject("shop");
        var shop = shopObj.AddComponent<ShopManager>();
        shop.availableUpgrades = new ShopManager.UpgradeData[0];
        typeof(ShopManager).GetMethod("LoadState", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(shop, null);

        var gmObj = new GameObject("gm");
        var gm = gmObj.AddComponent<GameManager>();
        gm.StartGame();

        var dcObj = new GameObject("dc");
        var dc = dcObj.AddComponent<DailyChallengeManager>();

        // Force distance so challenge completes
        var distField = typeof(GameManager).GetField("distance", BindingFlags.NonPublic | BindingFlags.Instance);
        distField.SetValue(gm, 6f);

        dc.Update();

        Assert.IsTrue(PlayerPrefs.GetString("DailyChallengeData").Contains("\"completed\":true"));
        Assert.Greater(shop.Coins, 0);

        Object.DestroyImmediate(dcObj);
        Object.DestroyImmediate(gmObj);
        Object.DestroyImmediate(shopObj);
    }

    // Small helper struct to craft challenge JSON for tests
    private class PrivateChallengeState
    {
        public DailyChallengeManager.ChallengeType type;
        public DailyChallengeManager.PowerUpType powerUp;
        public int target;
        public int progress;
        public long expires;
        public bool completed;
    }
}
