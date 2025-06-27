using NUnit.Framework;
using UnityEngine;
using System.Reflection;

/// <summary>
/// Tests for core GameManager functionality such as coin counting and
/// speed boosts. Executed through Unity's EditMode Test Runner.
/// </summary>

// EditMode tests can be run through Unity's Test Runner window.
// Create this file under Assets/Tests/EditMode and open Window > General > Test Runner.
// Select EditMode and run the tests.

public class GameManagerTests
{
    [Test]
    public void AddCoins_IncreasesTotal()
    {
        // Create a temporary GameManager instance
        var go = new GameObject("gm");
        var gm = go.AddComponent<GameManager>();

        gm.AddCoins(2);
        gm.AddCoins(3);

        // Total coins should equal the sum added
        Assert.AreEqual(5, gm.GetCoins());
        Object.DestroyImmediate(go);
    }

    [Test]
    public void ActivateSpeedBoost_MultipliesSpeed()
    {
        // GameManager must be running for speed boosts to apply
        var go = new GameObject("gm");
        var gm = go.AddComponent<GameManager>();
        gm.StartGame();

        var baseSpeed = gm.GetSpeed();
        gm.ActivateSpeedBoost(1f, 2f); // double the speed for one second
        // Verify the multiplier applied immediately
        Assert.AreEqual(baseSpeed * 2f, gm.GetSpeed());
        Object.DestroyImmediate(go);
    }

    [Test]
    public void CoinCombo_IncrementsAndResets()
    {
        // Validate that coins picked up quickly increase the combo multiplier
        var go = new GameObject("gm");
        var gm = go.AddComponent<GameManager>();

        gm.AddCoins(1);            // first coin, multiplier = 1
        gm.AddCoins(1);            // within combo window, multiplier now 2

        // Force the combo timer to expire
        var timerField = typeof(GameManager).GetField("coinComboTimer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        timerField.SetValue(gm, 0f);

        gm.AddCoins(1);            // new combo after timer reset

        // Calculation: 1 + 2 + 1 = 4 total coins
        Assert.AreEqual(4, gm.GetCoins());
        Object.DestroyImmediate(go);
    }

    private class TestGameManager : GameManager
    {
        public int comboCalls;
        protected override void OnComboIncreased()
        {
            comboCalls++;
        }
    }

    [Test]
    public void AddCoins_TriggersComboFeedback()
    {
        // Ensure OnComboIncreased is invoked when combo multiplier rises
        var go = new GameObject("gm");
        var gm = go.AddComponent<TestGameManager>();

        gm.AddCoins(1);    // multiplier = 1
        gm.AddCoins(1);    // multiplier increments -> should trigger feedback

        Assert.AreEqual(1, gm.comboCalls);
        Object.DestroyImmediate(go);
    }

    [Test]
    public void AddCoins_AppliesUpgradeMultiplier()
    {
        System.IO.File.Delete(System.IO.Path.Combine(
            Application.persistentDataPath, "savegame.json"));
        var saveObj = new GameObject("save");
        saveObj.AddComponent<SaveGameManager>();
        var data = new ShopManager.UpgradeData { type = UpgradeType.CoinMultiplier, cost = 1, effect = 1f };

        var shopObj = new GameObject("shop");
        var sm = shopObj.AddComponent<ShopManager>();
        sm.availableUpgrades = new[] { data };
        typeof(ShopManager).GetMethod("LoadState", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(sm, null);

        var dictField = typeof(ShopManager).GetField("upgradeLevels", BindingFlags.NonPublic | BindingFlags.Instance);
        var levels = (Dictionary<UpgradeType, int>)dictField.GetValue(sm);
        levels[UpgradeType.CoinMultiplier] = 1;
        dictField.SetValue(sm, levels);

        var go = new GameObject("gm");
        var gm = go.AddComponent<GameManager>();

        gm.AddCoins(1);

        Assert.AreEqual(2, gm.GetCoins());

        Object.DestroyImmediate(go);
        Object.DestroyImmediate(shopObj);
        Object.DestroyImmediate(saveObj);
    }

    [Test]
    public void ActivateSlowMotion_ChangesTimeScale()
    {
        var go = new GameObject("gm");
        var gm = go.AddComponent<GameManager>();
        gm.StartGame();

        gm.ActivateSlowMotion(0.5f, 0.5f);
        Assert.AreEqual(0.5f, Time.timeScale);

        // Fast-forward timer via reflection
        var timerField = typeof(GameManager).GetField("slowMotionTimer", BindingFlags.NonPublic | BindingFlags.Instance);
        timerField.SetValue(gm, 0f);
        var update = typeof(GameManager).GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance);
        update.Invoke(gm, null);

        Assert.AreEqual(1f, Time.timeScale);
        Object.DestroyImmediate(go);
    }
}
