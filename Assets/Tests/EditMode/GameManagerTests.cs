using NUnit.Framework;
using UnityEngine;
using System;
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

    /// <summary>
    /// Ensure ActivateSpeedBoost validates its duration argument and rejects
    /// zero or negative values to prevent unintended permanent boosts.
    /// </summary>
    [Test]
    public void ActivateSpeedBoost_RejectsNonPositiveDuration()
    {
        var go = new GameObject("gm");
        var gm = go.AddComponent<GameManager>();

        // Expect an ArgumentException when duration is zero.
        Assert.Throws<ArgumentException>(() => gm.ActivateSpeedBoost(0f, 1f));
        Object.DestroyImmediate(go);
    }

    /// <summary>
    /// Ensure ActivateSpeedBoost validates its multiplier argument and rejects
    /// zero or negative values to avoid stalling or reversing movement.
    /// </summary>
    [Test]
    public void ActivateSpeedBoost_RejectsNonPositiveMultiplier()
    {
        var go = new GameObject("gm");
        var gm = go.AddComponent<GameManager>();

        // Expect an ArgumentOutOfRangeException when multiplier is zero.
        Assert.Throws<ArgumentOutOfRangeException>(() => gm.ActivateSpeedBoost(1f, 0f));
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
    public void ActivateCoinBonus_MultipliesCoins()
    {
        var go = new GameObject("gm");
        var gm = go.AddComponent<GameManager>();
        gm.StartGame();

        gm.ActivateCoinBonus(1f, 2f);
        gm.AddCoins(1);
        Assert.AreEqual(2, gm.GetCoins());

        typeof(GameManager).GetField("coinBonusTimer", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(gm, 0f);
        gm.Update();
        gm.AddCoins(1);
        Assert.AreEqual(3, gm.GetCoins());

        Object.DestroyImmediate(go);
    }

    /// <summary>
    /// Verifies that coin bonus pickups stack their durations and keep the
    /// highest multiplier.
    /// </summary>
    [Test]
    public void ActivateCoinBonus_StacksDurationAndMultiplier()
    {
        var go = new GameObject("gm");
        var gm = go.AddComponent<GameManager>();
        gm.StartGame();

        gm.ActivateCoinBonus(1f, 2f);
        gm.ActivateCoinBonus(0.5f, 3f);

        var timerField = typeof(GameManager).GetField("coinBonusTimer", BindingFlags.NonPublic | BindingFlags.Instance);
        var multField = typeof(GameManager).GetField("coinBonusMultiplier", BindingFlags.NonPublic | BindingFlags.Instance);

        float timer = (float)timerField.GetValue(gm);
        float multiplier = (float)multField.GetValue(gm);

        Assert.AreEqual(1.5f, timer);
        Assert.AreEqual(3f, multiplier);

        Object.DestroyImmediate(go);
    }

    /// <summary>
    /// Ensures the public getters return the current bonus values for UI.
    /// </summary>
    [Test]
    public void GetCoinBonusMethods_ReturnCurrentValues()
    {
        var go = new GameObject("gm");
        var gm = go.AddComponent<GameManager>();
        gm.StartGame();

        gm.ActivateCoinBonus(1f, 2f);

        Assert.AreEqual(1f, gm.GetCoinBonusTimeRemaining());
        Assert.AreEqual(2f, gm.GetCoinBonusMultiplier());

        Object.DestroyImmediate(go);
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

    /// <summary>
    /// Enabling hardcore mode should multiply the player's speed.
    /// </summary>
    [Test]
    public void HardcoreMode_IncreasesSpeed()
    {
        // Need a SaveGameManager so the GameManager can persist settings
        var saveObj = new GameObject("save");
        saveObj.AddComponent<SaveGameManager>();

        var go = new GameObject("gm");
        var gm = go.AddComponent<GameManager>();
        gm.StartGame();

        float normal = gm.GetSpeed();
        gm.HardcoreMode = true;
        float hardcore = gm.GetSpeed();

        Assert.AreEqual(normal * gm.hardcoreSpeedMultiplier, hardcore, 0.001f);

        Object.DestroyImmediate(go);
        Object.DestroyImmediate(saveObj);
    }

    /// <summary>
    /// Hardcore mode selection should be saved and loaded across sessions.
    /// </summary>
    [Test]
    public void HardcoreMode_PersistedBetweenRuns()
    {
        // Enable hardcore mode and destroy the objects to force a save
        var saveObj = new GameObject("save");
        saveObj.AddComponent<SaveGameManager>();
        var gmObj = new GameObject("gm");
        var gm = gmObj.AddComponent<GameManager>();
        gm.HardcoreMode = true;
        Object.DestroyImmediate(gmObj);
        Object.DestroyImmediate(saveObj);

        // Creating new instances should load the persisted setting
        var saveObj2 = new GameObject("save2");
        saveObj2.AddComponent<SaveGameManager>();
        var gmObj2 = new GameObject("gm2");
        var gm2 = gmObj2.AddComponent<GameManager>();

        Assert.IsTrue(gm2.HardcoreMode);

        Object.DestroyImmediate(gmObj2);
        Object.DestroyImmediate(saveObj2);
    }

    /// <summary>
    /// Verifies that a duplicate <see cref="GameManager"/> exits early after
    /// destroying itself and therefore does not recreate missing singleton
    /// dependencies. This prevents null reference errors that would occur if
    /// initialization continued on the destroyed instance.
    /// </summary>
    [Test]
    public void Awake_DuplicateDoesNotReinitializeDependencies()
    {
        // Establish the primary manager which also spawns a SaveGameManager.
        var primaryObj = new GameObject("gmPrimary");
        primaryObj.AddComponent<GameManager>();

        // Simulate the supporting SaveGameManager being missing by destroying
        // it and clearing the static Instance field via reflection.
        if (SaveGameManager.Instance != null)
        {
            Object.DestroyImmediate(SaveGameManager.Instance.gameObject);
            var instField = typeof(SaveGameManager).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
            instField.SetValue(null, null);
        }

        // Create a second manager which should destroy itself and return
        // before recreating the SaveGameManager dependency.
        var duplicateObj = new GameObject("gmDuplicate");
        duplicateObj.AddComponent<GameManager>();

        // The dependency should remain absent, proving the duplicate aborted
        // initialization after self-destruction.
        Assert.IsNull(SaveGameManager.Instance);

        Object.DestroyImmediate(primaryObj);
        Object.DestroyImmediate(duplicateObj);
    }
}
