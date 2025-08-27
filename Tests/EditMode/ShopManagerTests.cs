using NUnit.Framework;
using UnityEngine;
using System.Reflection;
using System.Collections.Generic;

/// <summary>
/// Tests verifying the persistent upgrade and coin logic of <see cref="ShopManager"/>.
/// </summary>
public class ShopManagerTests
{
    // Local spy used to verify the number of times SaveGameManager writes data
    // during SaveState calls.
    private class SaveGameManagerSpy : SaveGameManager
    {
        public int Calls { get; private set; }
        protected override void SaveDataToFile()
        {
            Calls++;
            base.SaveDataToFile();
        }
    }
    [Test]
    public void AddCoins_PersistsTotal()
    {
        // Remove any existing save file before starting the test
        System.IO.File.Delete(System.IO.Path.Combine(
            Application.persistentDataPath, "savegame.json"));

        var saveObj = new GameObject("save");
        saveObj.AddComponent<SaveGameManager>();

        var go = new GameObject("shop");
        var sm = go.AddComponent<ShopManager>();
        sm.availableUpgrades = new ShopManager.UpgradeData[0];

        // Reload state now that upgrades are assigned
        typeof(ShopManager).GetMethod("LoadState", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(sm, null);

        sm.AddCoins(5);
        Object.DestroyImmediate(go);
        Object.DestroyImmediate(saveObj);

        var go2 = new GameObject("shop2");
        var saveObj2 = new GameObject("save2");
        saveObj2.AddComponent<SaveGameManager>();
        var sm2 = go2.AddComponent<ShopManager>();
        sm2.availableUpgrades = new ShopManager.UpgradeData[0];
        typeof(ShopManager).GetMethod("LoadState", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(sm2, null);

        Assert.AreEqual(5, sm2.Coins);
        Object.DestroyImmediate(go2);
        Object.DestroyImmediate(saveObj2);
    }

    [Test]
    public void PurchaseUpgrade_DeductsCoinsAndSavesLevel()
    {
        System.IO.File.Delete(System.IO.Path.Combine(
            Application.persistentDataPath, "savegame.json"));
        var saveObj = new GameObject("save");
        saveObj.AddComponent<SaveGameManager>();
        var data = new ShopManager.UpgradeData { type = UpgradeType.MagnetDuration, cost = 3, effect = 1f };

        var go = new GameObject("shop");
        var sm = go.AddComponent<ShopManager>();
        sm.availableUpgrades = new[] { data };
        typeof(ShopManager).GetMethod("LoadState", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(sm, null);

        sm.AddCoins(5);
        bool bought = sm.PurchaseUpgrade(UpgradeType.MagnetDuration);

        Assert.IsTrue(bought);
        Assert.AreEqual(2, sm.Coins);
        Assert.AreEqual(1f, sm.GetUpgradeEffect(UpgradeType.MagnetDuration));
        Object.DestroyImmediate(go);
        Object.DestroyImmediate(saveObj);

        var go2 = new GameObject("shop2");
        var saveObj2 = new GameObject("save2");
        saveObj2.AddComponent<SaveGameManager>();
        var sm2 = go2.AddComponent<ShopManager>();
        sm2.availableUpgrades = new[] { data };
        typeof(ShopManager).GetMethod("LoadState", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(sm2, null);

        Assert.AreEqual(1f, sm2.GetUpgradeEffect(UpgradeType.MagnetDuration));
        Object.DestroyImmediate(go2);
        Object.DestroyImmediate(saveObj2);
    }

    [Test]
    public void MagnetPowerUp_UsesUpgradeEffect()
    {
        System.IO.File.Delete(System.IO.Path.Combine(
            Application.persistentDataPath, "savegame.json"));
        var saveObj = new GameObject("save");
        saveObj.AddComponent<SaveGameManager>();
        var data = new ShopManager.UpgradeData { type = UpgradeType.MagnetDuration, cost = 1, effect = 1f };

        var shopObj = new GameObject("shop");
        var sm = shopObj.AddComponent<ShopManager>();
        sm.availableUpgrades = new[] { data };
        typeof(ShopManager).GetMethod("LoadState", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(sm, null);

        // Manually set upgrade level to simulate a previous purchase
        var dictField = typeof(ShopManager).GetField("upgradeLevels", BindingFlags.NonPublic | BindingFlags.Instance);
        var levels = (Dictionary<UpgradeType, int>)dictField.GetValue(sm);
        levels[UpgradeType.MagnetDuration] = 1;
        dictField.SetValue(sm, levels);

        var player = new GameObject("player");
        player.tag = "Player";
        var magnet = player.AddComponent<CoinMagnet>();
        var playerCol = player.AddComponent<BoxCollider2D>();

        var powerObj = new GameObject("power");
        var mp = powerObj.AddComponent<MagnetPowerUp>();
        var powerCol = powerObj.AddComponent<BoxCollider2D>();
        powerCol.isTrigger = true;
        mp.duration = 2f;

        mp.OnTriggerEnter2D(playerCol);

        var timerField = typeof(CoinMagnet).GetField("magnetTimer", BindingFlags.NonPublic | BindingFlags.Instance);
        float timer = (float)timerField.GetValue(magnet);

        Assert.AreEqual(3f, timer);

        Object.DestroyImmediate(powerObj);
        Object.DestroyImmediate(player);
        Object.DestroyImmediate(shopObj);
        Object.DestroyImmediate(saveObj);
    }

    [Test]
    public void SpeedBoostPowerUp_UsesUpgradeEffect()
    {
        System.IO.File.Delete(System.IO.Path.Combine(
            Application.persistentDataPath, "savegame.json"));
        var saveObj = new GameObject("save");
        saveObj.AddComponent<SaveGameManager>();
        var data = new ShopManager.UpgradeData { type = UpgradeType.SpeedBoostDuration, cost = 1, effect = 1f };

        var shopObj = new GameObject("shop");
        var sm = shopObj.AddComponent<ShopManager>();
        sm.availableUpgrades = new[] { data };
        typeof(ShopManager).GetMethod("LoadState", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(sm, null);

        var dictField = typeof(ShopManager).GetField("upgradeLevels", BindingFlags.NonPublic | BindingFlags.Instance);
        var levels = (Dictionary<UpgradeType, int>)dictField.GetValue(sm);
        levels[UpgradeType.SpeedBoostDuration] = 1;
        dictField.SetValue(sm, levels);

        var gmObj = new GameObject("gm");
        var gm = gmObj.AddComponent<GameManager>();

        var player = new GameObject("player");
        player.tag = "Player";
        var playerCol = player.AddComponent<BoxCollider2D>();

        var powerObj = new GameObject("power");
        var sb = powerObj.AddComponent<SpeedBoostPowerUp>();
        var powerCol = powerObj.AddComponent<BoxCollider2D>();
        powerCol.isTrigger = true;
        sb.duration = 2f;

        sb.OnTriggerEnter2D(playerCol);

        var timerField = typeof(GameManager).GetField("speedBoostTimer", BindingFlags.NonPublic | BindingFlags.Instance);
        float timer = (float)timerField.GetValue(gm);

        Assert.AreEqual(3f, timer);

        Object.DestroyImmediate(powerObj);
        Object.DestroyImmediate(player);
        Object.DestroyImmediate(gmObj);
        Object.DestroyImmediate(shopObj);
        Object.DestroyImmediate(saveObj);
    }

    [Test]
    public void ShieldPowerUp_UsesUpgradeEffect()
    {
        System.IO.File.Delete(System.IO.Path.Combine(
            Application.persistentDataPath, "savegame.json"));
        var saveObj = new GameObject("save");
        saveObj.AddComponent<SaveGameManager>();
        var data = new ShopManager.UpgradeData { type = UpgradeType.ShieldDuration, cost = 1, effect = 1f };

        var shopObj = new GameObject("shop");
        var sm = shopObj.AddComponent<ShopManager>();
        sm.availableUpgrades = new[] { data };
        typeof(ShopManager).GetMethod("LoadState", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(sm, null);

        var dictField = typeof(ShopManager).GetField("upgradeLevels", BindingFlags.NonPublic | BindingFlags.Instance);
        var levels = (Dictionary<UpgradeType, int>)dictField.GetValue(sm);
        levels[UpgradeType.ShieldDuration] = 1;
        dictField.SetValue(sm, levels);

        var player = new GameObject("player");
        player.tag = "Player";
        var shield = player.AddComponent<PlayerShield>();
        var playerCol = player.AddComponent<BoxCollider2D>();

        var powerObj = new GameObject("power");
        var sp = powerObj.AddComponent<ShieldPowerUp>();
        var powerCol = powerObj.AddComponent<BoxCollider2D>();
        powerCol.isTrigger = true;
        sp.duration = 2f;

        sp.OnTriggerEnter2D(playerCol);

        var timerField = typeof(PlayerShield).GetField("shieldTimer", BindingFlags.NonPublic | BindingFlags.Instance);
        float timer = (float)timerField.GetValue(shield);

        Assert.AreEqual(3f, timer);

        Object.DestroyImmediate(powerObj);
        Object.DestroyImmediate(player);
        Object.DestroyImmediate(shopObj);
        Object.DestroyImmediate(saveObj);
    }

    /// <summary>
    /// Saving multiple upgrades should result in only a single disk write from
    /// <see cref="SaveGameManager.UpdateUpgradeLevels"/> instead of one per
    /// upgrade entry.
    /// </summary>
    [Test]
    public void SaveState_BatchesUpgradeWrites()
    {
        System.IO.File.Delete(System.IO.Path.Combine(
            Application.persistentDataPath, "savegame.json"));

        var saveObj = new GameObject("save");
        var save = saveObj.AddComponent<SaveGameManagerSpy>();

        var go = new GameObject("shop");
        var sm = go.AddComponent<ShopManager>();
        sm.availableUpgrades = new[]
        {
            new ShopManager.UpgradeData { type = UpgradeType.MagnetDuration, cost = 1, effect = 0 },
            new ShopManager.UpgradeData { type = UpgradeType.SpeedBoostDuration, cost = 1, effect = 0 }
        };
        typeof(ShopManager).GetMethod("LoadState", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(sm, null);

        // Simulate purchase by directly modifying the upgrade dictionary
        var dictField = typeof(ShopManager).GetField("upgradeLevels", BindingFlags.NonPublic | BindingFlags.Instance);
        var levels = (Dictionary<UpgradeType, int>)dictField.GetValue(sm);
        levels[UpgradeType.MagnetDuration] = 1;
        levels[UpgradeType.SpeedBoostDuration] = 2;

        typeof(ShopManager).GetMethod("SaveState", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(sm, null);

        Assert.AreEqual(2, save.Calls); // one for coins, one for upgrades

        Object.DestroyImmediate(go);
        Object.DestroyImmediate(saveObj);
    }
}
