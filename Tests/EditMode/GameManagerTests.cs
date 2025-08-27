using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using TMPro;               // Needed for assigning TMP_Text references
using System;
using System.Collections;  // Provides IEnumerator for Unity tests
using System.Reflection;
using System.Text.RegularExpressions;

// EditMode tests can be run through Unity's Test Runner window.
// Create this file under Assets/Tests/EditMode and open Window > General > Test Runner.
// Select EditMode and run the tests.

public class GameManagerTests
{
    /// <summary>
    /// Creates a <see cref="GameManager"/> (or subclass) with the required UI
    /// label references. Parenting the labels to the manager ensures they clean
    /// up automatically when the manager is destroyed.
    /// </summary>
    private T CreateGameManagerWithUI<T>() where T : GameManager
    {
        var go = new GameObject("gm");
        var gm = go.AddComponent<T>();

        gm.scoreLabel = new GameObject("scoreLabel").AddComponent<TextMeshProUGUI>();
        gm.scoreLabel.transform.SetParent(go.transform);

        gm.highScoreLabel = new GameObject("highScoreLabel").AddComponent<TextMeshProUGUI>();
        gm.highScoreLabel.transform.SetParent(go.transform);

        gm.coinLabel = new GameObject("coinLabel").AddComponent<TextMeshProUGUI>();
        gm.coinLabel.transform.SetParent(go.transform);

        gm.comboLabel = new GameObject("comboLabel").AddComponent<TextMeshProUGUI>();
        gm.comboLabel.transform.SetParent(go.transform);

        return gm;
    }

    // Convenience overload for tests that use the base GameManager type
    private GameManager CreateGameManagerWithUI()
    {
        return CreateGameManagerWithUI<GameManager>();
    }

    [Test]
    public void AddCoins_IncreasesTotal()
    {
        // Create a temporary GameManager instance with required UI labels
        var gm = CreateGameManagerWithUI();
        var go = gm.gameObject;

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
        var gm = CreateGameManagerWithUI();
        var go = gm.gameObject;

        // Assign a dummy player reference to satisfy GameManager's runtime validation.
        var player = new GameObject("player");
        typeof(GameManager).GetField("playerObject", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(gm, player);

        gm.StartGame();

        var baseSpeed = gm.GetSpeed();
        gm.ActivateSpeedBoost(1f, 2f); // double the speed for one second
        // Verify the multiplier applied immediately
        Assert.AreEqual(baseSpeed * 2f, gm.GetSpeed());
        Object.DestroyImmediate(go);
        Object.DestroyImmediate(player);
    }

    /// <summary>
    /// Ensure ActivateSpeedBoost validates its duration argument and rejects
    /// zero or negative values to prevent unintended permanent boosts.
    /// </summary>
    [Test]
    public void ActivateSpeedBoost_RejectsNonPositiveDuration()
    {
        var gm = CreateGameManagerWithUI();
        var go = gm.gameObject;

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
        var gm = CreateGameManagerWithUI();
        var go = gm.gameObject;

        // Expect an ArgumentOutOfRangeException when multiplier is zero.
        Assert.Throws<ArgumentOutOfRangeException>(() => gm.ActivateSpeedBoost(1f, 0f));
        Object.DestroyImmediate(go);
    }

    /// <summary>
    /// Verifies that the GameManager's internal speed never surpasses
    /// <see cref="GameManager.MaxSpeed"/> even after many updates. This ensures
    /// difficulty managers cannot cause runaway acceleration that would make
    /// gameplay unmanageable.
    /// </summary>
    [UnityTest]
    public IEnumerator Update_DoesNotExceedMaxSpeed()
    {
        var gm = CreateGameManagerWithUI();
        var go = gm.gameObject;

        // Assign required player reference for StartGame validation.
        var player = new GameObject("player");
        typeof(GameManager).GetField("playerObject", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(gm, player);

        gm.baseSpeed = 0f;                    // start from standstill
        gm.speedIncrease = 10f;               // aggressive acceleration
        typeof(GameManager).GetField("maxSpeed", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(gm, 1f);                // low cap to hit quickly

        gm.StartGame();                       // begin the run

        // Allow several frames for Update to execute and attempt to exceed the cap.
        for (int i = 0; i < 10; i++)
        {
            yield return null;                // each frame applies speed increase
        }

        // Retrieve the private currentSpeed field to ensure clamping occurred.
        var speedField = typeof(GameManager).GetField("currentSpeed", BindingFlags.NonPublic | BindingFlags.Instance);
        float currentSpeed = (float)speedField.GetValue(gm);

        // Speed should not surpass the configured maximum value.
        Assert.LessOrEqual(currentSpeed, 1f + 0.0001f);

        Object.DestroyImmediate(go);
        Object.DestroyImmediate(player);
    }

    [Test]
    public void CoinCombo_IncrementsAndResets()
    {
        // Validate that coins picked up quickly increase the combo multiplier
        var gm = CreateGameManagerWithUI();
        var go = gm.gameObject;

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
        var gm = CreateGameManagerWithUI<TestGameManager>();
        var go = gm.gameObject;

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

        var gm = CreateGameManagerWithUI();
        var go = gm.gameObject;

        gm.AddCoins(1);

        Assert.AreEqual(2, gm.GetCoins());

        Object.DestroyImmediate(go);
        Object.DestroyImmediate(shopObj);
        Object.DestroyImmediate(saveObj);
    }

    /// <summary>
    /// Ensures <see cref="GameManager.StartGame"/> exits immediately when the
    /// player reference is missing so the game remains idle and configured
    /// starting power-ups do not spawn.
    /// </summary>
    [Test]
    public void StartGame_NoPlayer_DoesNotRunOrSpawnPowerUps()
    {
        // Create required singletons and grant one starting power-up upgrade.
        // If StartGame proceeded normally, this upgrade would trigger a spawn.
        var saveObj = new GameObject("save");
        saveObj.AddComponent<SaveGameManager>();
        var shopObj = new GameObject("shop");
        var shop = shopObj.AddComponent<ShopManager>();
        shop.availableUpgrades = new[]
        {
            new ShopManager.UpgradeData
            {
                type = UpgradeType.StartingPowerUp,
                cost = 1,
                effect = 1f
            }
        };
        typeof(ShopManager).GetMethod("LoadState", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(shop, null);
        var dictField = typeof(ShopManager).GetField("upgradeLevels", BindingFlags.NonPublic | BindingFlags.Instance);
        var levels = (System.Collections.Generic.Dictionary<UpgradeType, int>)dictField.GetValue(shop);
        levels[UpgradeType.StartingPowerUp] = 1;
        dictField.SetValue(shop, levels);

        // Configure the GameManager with a power-up prefab but intentionally omit the player assignment.
        var gm = CreateGameManagerWithUI();
        var go = gm.gameObject;
        gm.startingPowerUps = new[] { new GameObject("PowerUp") };

        // Expect an error about the missing player and ensure StartGame returns without running.
        LogAssert.Expect(LogType.Error, new Regex("Player object reference not set"));
        gm.StartGame();

        // Check the private isRunning flag to confirm no run was initiated.
        var runningField = typeof(GameManager).GetField("isRunning", BindingFlags.NonPublic | BindingFlags.Instance);
        bool running = (bool)runningField.GetValue(gm);
        Assert.IsFalse(running);

        // Verify that the starting power-up prefab was not instantiated.
        Assert.IsNull(GameObject.Find("PowerUp(Clone)"));

        // Cleanup all temporary objects to keep later tests isolated.
        Object.DestroyImmediate(gm.startingPowerUps[0]);
        Object.DestroyImmediate(go);
        Object.DestroyImmediate(shopObj);
        Object.DestroyImmediate(saveObj);
        LogAssert.NoUnexpectedReceived();
    }

    /// <summary>
    /// Ensures <see cref="GameManager.StartGame"/> gracefully skips null entries
    /// in <see cref="GameManager.startingPowerUps"/>. A warning should be logged
    /// and valid prefabs still spawn so misconfigured arrays do not crash the
    /// game at runtime.
    /// </summary>
    [Test]
    public void StartGame_NullStartingPowerUp_SkipsAndWarns()
    {
        // Enable verbose logging so the warning about null entries is emitted
        // during the test but restore the original state afterward to avoid
        // affecting other tests.
        bool prevVerbose = LoggingHelper.VerboseEnabled;
        LoggingHelper.VerboseEnabled = true;

        // Create required singletons and grant one starting power-up upgrade.
        var saveObj = new GameObject("save");
        saveObj.AddComponent<SaveGameManager>();
        var shopObj = new GameObject("shop");
        var shop = shopObj.AddComponent<ShopManager>();
        shop.availableUpgrades = new[]
        {
            new ShopManager.UpgradeData
            {
                type = UpgradeType.StartingPowerUp,
                cost = 1,
                effect = 1f
            }
        };
        typeof(ShopManager).GetMethod("LoadState", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(shop, null);
        var dictField = typeof(ShopManager).GetField("upgradeLevels", BindingFlags.NonPublic | BindingFlags.Instance);
        var levels = (System.Collections.Generic.Dictionary<UpgradeType, int>)dictField.GetValue(shop);
        levels[UpgradeType.StartingPowerUp] = 1;
        dictField.SetValue(shop, levels);

        // GameManager configured with one valid prefab and one null entry.
        var gm = CreateGameManagerWithUI();
        var go = gm.gameObject;
        gm.startingPowerUps = new[] { new GameObject("PowerUp"), null };

        // Assign player reference so StartGame proceeds normally.
        var player = new GameObject("player");
        typeof(GameManager).GetField("playerObject", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(gm, player);

        // Expect a warning about the null entry and ensure no exception is thrown.
        LogAssert.Expect(LogType.Warning, new Regex("startingPowerUps contains null"));
        Assert.DoesNotThrow(() => gm.StartGame());

        // Only the valid prefab should have spawned; the null entry is skipped.
        Assert.IsNotNull(GameObject.Find("PowerUp(Clone)"));

        LogAssert.NoUnexpectedReceived();

        // Clean up spawned objects and singletons to keep the test isolated.
        Object.DestroyImmediate(GameObject.Find("PowerUp(Clone)"));
        Object.DestroyImmediate(go);
        Object.DestroyImmediate(player);
        Object.DestroyImmediate(shopObj);
        Object.DestroyImmediate(saveObj);

        // Restore the original verbose logging setting.
        LoggingHelper.VerboseEnabled = prevVerbose;
    }

    [Test]
    public void ActivateCoinBonus_MultipliesCoins()
    {
        var gm = CreateGameManagerWithUI();
        var go = gm.gameObject;

        // Provide the required player reference for StartGame.
        var player = new GameObject("player");
        typeof(GameManager).GetField("playerObject", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(gm, player);

        gm.StartGame();

        gm.ActivateCoinBonus(1f, 2f);
        gm.AddCoins(1);
        Assert.AreEqual(2, gm.GetCoins());

        typeof(GameManager).GetField("coinBonusTimer", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(gm, 0f);
        gm.Update();
        gm.AddCoins(1);
        Assert.AreEqual(3, gm.GetCoins());

        Object.DestroyImmediate(go);
        Object.DestroyImmediate(player);
    }

    /// <summary>
    /// Verifies that coin bonus pickups stack their durations and keep the
    /// highest multiplier.
    /// </summary>
    [Test]
    public void ActivateCoinBonus_StacksDurationAndMultiplier()
    {
        var gm = CreateGameManagerWithUI();
        var go = gm.gameObject;

        // Supply player reference so combo bonus logic can run safely.
        var player = new GameObject("player");
        typeof(GameManager).GetField("playerObject", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(gm, player);

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
        Object.DestroyImmediate(player);
    }

    /// <summary>
    /// Ensures the public getters return the current bonus values for UI.
    /// </summary>
    [Test]
    public void GetCoinBonusMethods_ReturnCurrentValues()
    {
        var gm = CreateGameManagerWithUI();
        var go = gm.gameObject;

        // Assign player reference for StartGame validation.
        var player = new GameObject("player");
        typeof(GameManager).GetField("playerObject", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(gm, player);

        gm.StartGame();

        gm.ActivateCoinBonus(1f, 2f);

        Assert.AreEqual(1f, gm.GetCoinBonusTimeRemaining());
        Assert.AreEqual(2f, gm.GetCoinBonusMultiplier());

        Object.DestroyImmediate(go);
        Object.DestroyImmediate(player);
    }

    [Test]
    public void ActivateSlowMotion_ChangesTimeScale()
    {
        var gm = CreateGameManagerWithUI();
        var go = gm.gameObject;

        // Create a dummy player object required by StartGame.
        var player = new GameObject("player");
        typeof(GameManager).GetField("playerObject", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(gm, player);

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
        Object.DestroyImmediate(player);
    }

    /// <summary>
    /// Pausing the game should freeze <see cref="Time.timeScale"/> and prevent
    /// objects that rely on <see cref="GameManager.IsRunning"/> from moving.
    /// </summary>
    [Test]
    public void PauseGame_StopsObjectMovement()
    {
        // Create GameManager with required UI and a dummy player for StartGame.
        var gm = CreateGameManagerWithUI();
        var go = gm.gameObject;
        var player = new GameObject("player");
        typeof(GameManager).GetField("playerObject", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(gm, player);

        gm.StartGame();

        // Spawn a ZigZagEnemy which consults IsRunning() before moving.
        var enemyObj = new GameObject("enemy");
        var enemy = enemyObj.AddComponent<ZigZagEnemy>();
        enemy.OnEnable();

        // Run one update to establish a baseline position.
        enemy.Update();
        Vector3 posBeforePause = enemyObj.transform.position;

        // Pausing should set timeScale to zero and halt further movement.
        gm.PauseGame();
        enemy.Update();
        Vector3 posAfterPause = enemyObj.transform.position;

        Assert.AreEqual(posBeforePause, posAfterPause, "Enemy should not move while game is paused.");

        // Resume to restore time scale for subsequent tests and clean up.
        gm.ResumeGame();
        Object.DestroyImmediate(enemyObj);
        Object.DestroyImmediate(go);
        Object.DestroyImmediate(player);
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

        var gm = CreateGameManagerWithUI();
        var go = gm.gameObject;

        // Provide player reference so StartGame does not log errors.
        var player = new GameObject("player");
        typeof(GameManager).GetField("playerObject", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(gm, player);

        gm.StartGame();

        float normal = gm.GetSpeed();
        gm.HardcoreMode = true;
        float hardcore = gm.GetSpeed();

        Assert.AreEqual(normal * gm.hardcoreSpeedMultiplier, hardcore, 0.001f);

        Object.DestroyImmediate(go);
        Object.DestroyImmediate(player);
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
        var gm = CreateGameManagerWithUI();
        gm.HardcoreMode = true;
        Object.DestroyImmediate(gm.gameObject);
        Object.DestroyImmediate(saveObj);

        // Creating new instances should load the persisted setting
        var saveObj2 = new GameObject("save2");
        saveObj2.AddComponent<SaveGameManager>();
        var gm2 = CreateGameManagerWithUI();

        Assert.IsTrue(gm2.HardcoreMode);

        Object.DestroyImmediate(gm2.gameObject);
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
        var primary = CreateGameManagerWithUI();
        var primaryObj = primary.gameObject;

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

    /// <summary>
    /// Ensures <see cref="GameManager.StartGame"/> logs an error and skips spawning
    /// starting power-ups when no player object exists in the scene.
    /// </summary>
    [Test]
    public void StartGame_PlayerMissing_SkipsPowerUpSpawn()
    {
        // Create required singletons so upgrade data can be queried.
        var saveObj = new GameObject("save");
        saveObj.AddComponent<SaveGameManager>();
        var shopObj = new GameObject("shop");
        var shop = shopObj.AddComponent<ShopManager>();
        shop.availableUpgrades = new[]
        {
            new ShopManager.UpgradeData
            {
                type = UpgradeType.StartingPowerUp,
                cost = 1,
                effect = 1f
            }
        };
        // Populate upgrade levels so the starting power-up count is one.
        typeof(ShopManager).GetMethod("LoadState", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(shop, null);
        var dictField = typeof(ShopManager).GetField("upgradeLevels", BindingFlags.NonPublic | BindingFlags.Instance);
        var levels = (System.Collections.Generic.Dictionary<UpgradeType, int>)dictField.GetValue(shop);
        levels[UpgradeType.StartingPowerUp] = 1;
        dictField.SetValue(shop, levels);

        // GameManager with a simple power-up prefab to instantiate.
        var gm = CreateGameManagerWithUI();
        var go = gm.gameObject;
        gm.startingPowerUps = new[] { new GameObject("PowerUp") };

        // Expect an error about the missing player reference.
        LogAssert.Expect(LogType.Error, new Regex("Player object reference not set"));

        gm.StartGame();

        // The prefab should not have spawned because the player was absent.
        Assert.IsNull(GameObject.Find("PowerUp(Clone)"));

        Object.DestroyImmediate(go);
        Object.DestroyImmediate(shopObj);
        Object.DestroyImmediate(saveObj);
    }

    /// <summary>
    /// Calling <see cref="GameManager.GameOver"/> with a missing save system
    /// should log errors but still complete gracefully without throwing
    /// exceptions.
    /// </summary>
    [Test]
    public void GameOver_MissingSaveManager_LogsErrorAndUsesFallback()
    {
        var gm = CreateGameManagerWithUI();
        var go = gm.gameObject;

        // Provide a ShopManager to avoid unrelated error logs.
        var shopObj = new GameObject("shop");
        shopObj.AddComponent<ShopManager>();

        // Remove the SaveGameManager instance to simulate a missing dependency.
        if (SaveGameManager.Instance != null)
        {
            Object.DestroyImmediate(SaveGameManager.Instance.gameObject);
            var instField = typeof(SaveGameManager).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
            instField.SetValue(null, null);
        }

        // Expect error logs about the missing save manager.
        LogAssert.Expect(LogType.Error, new Regex("SaveGameManager missing"));
        LogAssert.Expect(LogType.Error, new Regex("SaveGameManager missing"));
        LogAssert.Expect(LogType.Error, new Regex("SaveGameManager missing"));

        gm.GameOver();

        LogAssert.NoUnexpectedReceived();

        Object.DestroyImmediate(go);
        Object.DestroyImmediate(shopObj);
    }

    /// <summary>
    /// <see cref="GameManager.StartGame"/> should use default values and log
    /// errors when the <see cref="ShopManager"/> dependency is missing.
    /// </summary>
    [Test]
    public void StartGame_MissingShopManager_UsesDefaultsAndLogsError()
    {
        var gm = CreateGameManagerWithUI();
        var go = gm.gameObject;

        // Supply required player reference so only ShopManager errors surface.
        var player = new GameObject("player");
        typeof(GameManager).GetField("playerObject", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(gm, player);

        // Expect two error logs: one for speed bonus and one for power-up count.
        LogAssert.Expect(LogType.Error, new Regex("ShopManager missing"));
        LogAssert.Expect(LogType.Error, new Regex("ShopManager missing"));

        gm.StartGame();

        // Without a shop, the starting speed should equal the base speed.
        Assert.AreEqual(gm.baseSpeed, gm.GetSpeed());

        LogAssert.NoUnexpectedReceived();

        Object.DestroyImmediate(go);
        Object.DestroyImmediate(player);
    }
}
