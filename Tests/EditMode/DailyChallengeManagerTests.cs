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

    /// <summary>
    /// Verifies a new challenge is generated when the stored one has expired.
    /// </summary>
    [Test]
    public void LoadOrGenerate_ExpiredChallenge_Regenerates()
    {
        var old = new PrivateChallengeState
        {
            type = DailyChallengeManager.ChallengeType.Distance,
            target = 1,
            progress = 0,
            powerUp = DailyChallengeManager.PowerUpType.Magnet,
            expires = System.DateTime.UtcNow.AddDays(-1).Ticks,
            completed = false
        };
        PlayerPrefs.SetString("DailyChallengeData", JsonUtility.ToJson(old));

        var obj = new GameObject("dc");
        var dc = obj.AddComponent<DailyChallengeManager>();

        var json = PlayerPrefs.GetString("DailyChallengeData");
        var refreshed = JsonUtility.FromJson<PrivateChallengeState>(json);
        Assert.Greater(refreshed.expires, System.DateTime.UtcNow.Ticks);

        Object.DestroyImmediate(obj);
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

    /// <summary>
    /// Completing a challenge should unlock the daily achievement via
    /// SteamManager. A dummy implementation captures the unlock call.
    /// </summary>
    [Test]
    public void CompleteChallenge_UnlocksAchievement()
    {
        var state = new PrivateChallengeState
        {
            type = DailyChallengeManager.ChallengeType.Coins,
            target = 1,
            progress = 0,
            powerUp = DailyChallengeManager.PowerUpType.Magnet,
            expires = System.DateTime.UtcNow.AddDays(1).Ticks,
            completed = false
        };
        PlayerPrefs.SetString("DailyChallengeData", JsonUtility.ToJson(state));

        var steamObj = new GameObject("steam");
        var steam = steamObj.AddComponent<DummySteamManager>();

        var gmObj = new GameObject("gm");
        var gm = gmObj.AddComponent<GameManager>();
        gm.StartGame();

        var dcObj = new GameObject("dc");
        var dc = dcObj.AddComponent<DailyChallengeManager>();

        typeof(GameManager).GetField("coins", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(gm, 1);
        dc.Update();

        Assert.AreEqual("ACH_DAILY_COMPLETE", steam.unlockedId);

        Object.DestroyImmediate(dcObj);
        Object.DestroyImmediate(gmObj);
        Object.DestroyImmediate(steamObj);
    }

    private class DummySteamManager : SteamManager
    {
        public string unlockedId;

        public override void UnlockAchievement(string id)
        {
            unlockedId = id;
        }
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

    /// <summary>
    /// Verifies progress is buffered: values below the threshold are not saved
    /// immediately, but once the threshold is met the state is persisted for
    /// both distance and coin challenges.
    /// </summary>
    [Test]
    public void Update_OnlySavesAfterThreshold()
    {
        int threshold = (int)typeof(DailyChallengeManager)
            .GetField("ProgressSaveThreshold", BindingFlags.NonPublic | BindingFlags.Static)
            .GetValue(null);

        var gmObj = new GameObject("gm");
        var gm = gmObj.AddComponent<GameManager>();
        gm.StartGame();

        // --- Distance challenge threshold test ---
        var state = new PrivateChallengeState
        {
            type = DailyChallengeManager.ChallengeType.Distance,
            target = threshold * 2,
            progress = 0,
            powerUp = DailyChallengeManager.PowerUpType.Magnet,
            expires = System.DateTime.UtcNow.AddDays(1).Ticks,
            completed = false
        };
        PlayerPrefs.SetString("DailyChallengeData", JsonUtility.ToJson(state));

        var dcObj = new GameObject("dc");
        var dc = dcObj.AddComponent<DailyChallengeManager>();

        typeof(GameManager).GetField("distance", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(gm, (float)(threshold - 1));
        dc.Update();
        var loaded = JsonUtility.FromJson<PrivateChallengeState>(PlayerPrefs.GetString("DailyChallengeData"));
        Assert.AreEqual(0, loaded.progress, "Progress below threshold should not persist");

        typeof(GameManager).GetField("distance", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(gm, (float)threshold);
        dc.Update();
        loaded = JsonUtility.FromJson<PrivateChallengeState>(PlayerPrefs.GetString("DailyChallengeData"));
        Assert.AreEqual(threshold, loaded.progress, "Progress should persist once threshold reached");

        Object.DestroyImmediate(dcObj);

        // --- Coin challenge threshold test ---
        state.type = DailyChallengeManager.ChallengeType.Coins;
        state.progress = 0;
        PlayerPrefs.SetString("DailyChallengeData", JsonUtility.ToJson(state));

        var dcObj2 = new GameObject("dc2");
        var dc2 = dcObj2.AddComponent<DailyChallengeManager>();

        typeof(GameManager).GetField("coins", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(gm, threshold - 1);
        dc2.Update();
        loaded = JsonUtility.FromJson<PrivateChallengeState>(PlayerPrefs.GetString("DailyChallengeData"));
        Assert.AreEqual(0, loaded.progress, "Coin progress below threshold should not persist");

        typeof(GameManager).GetField("coins", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(gm, threshold);
        dc2.Update();
        loaded = JsonUtility.FromJson<PrivateChallengeState>(PlayerPrefs.GetString("DailyChallengeData"));
        Assert.AreEqual(threshold, loaded.progress, "Coin progress should persist once threshold reached");

        Object.DestroyImmediate(dcObj2);
        Object.DestroyImmediate(gmObj);
    }

    /// <summary>
    /// Even small progress changes are eventually flushed when the save interval
    /// elapses to prevent data loss.
    /// </summary>
    [Test]
    public void Update_SavesAfterInterval()
    {
        float interval = (float)typeof(DailyChallengeManager)
            .GetField("SaveInterval", BindingFlags.NonPublic | BindingFlags.Static)
            .GetValue(null);

        var state = new PrivateChallengeState
        {
            type = DailyChallengeManager.ChallengeType.Distance,
            target = 10,
            progress = 0,
            powerUp = DailyChallengeManager.PowerUpType.Magnet,
            expires = System.DateTime.UtcNow.AddDays(1).Ticks,
            completed = false
        };
        PlayerPrefs.SetString("DailyChallengeData", JsonUtility.ToJson(state));

        var gmObj = new GameObject("gm");
        var gm = gmObj.AddComponent<GameManager>();
        gm.StartGame();

        var dcObj = new GameObject("dc");
        var dc = dcObj.AddComponent<DailyChallengeManager>();

        // Progress by a small amount; no save should occur yet.
        typeof(GameManager).GetField("distance", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(gm, 1f);
        dc.Update();
        var loaded = JsonUtility.FromJson<PrivateChallengeState>(PlayerPrefs.GetString("DailyChallengeData"));
        Assert.AreEqual(0, loaded.progress, "Initial small progress should be buffered");

        // Force the timer to appear elapsed then update again so progress saves.
        typeof(DailyChallengeManager)
            .GetField("lastSaveTime", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(dc, Time.time - interval - 0.1f);
        dc.Update();
        loaded = JsonUtility.FromJson<PrivateChallengeState>(PlayerPrefs.GetString("DailyChallengeData"));
        Assert.AreEqual(1, loaded.progress, "Progress should save after interval passes");

        Object.DestroyImmediate(dcObj);
        Object.DestroyImmediate(gmObj);
    }
}
