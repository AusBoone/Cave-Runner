using NUnit.Framework;
using UnityEngine;
using System.Reflection;
using System.Collections.Generic;

/// <summary>
/// Tests verifying the persistent upgrade and coin logic of <see cref="ShopManager"/>.
/// </summary>
public class ShopManagerTests
{
    [Test]
    public void AddCoins_PersistsTotal()
    {
        PlayerPrefs.DeleteAll();
        var go = new GameObject("shop");
        var sm = go.AddComponent<ShopManager>();
        sm.availableUpgrades = new ShopManager.UpgradeData[0];

        // Reload state now that upgrades are assigned
        typeof(ShopManager).GetMethod("LoadState", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(sm, null);

        sm.AddCoins(5);
        Object.DestroyImmediate(go);

        var go2 = new GameObject("shop2");
        var sm2 = go2.AddComponent<ShopManager>();
        sm2.availableUpgrades = new ShopManager.UpgradeData[0];
        typeof(ShopManager).GetMethod("LoadState", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(sm2, null);

        Assert.AreEqual(5, sm2.Coins);
        Object.DestroyImmediate(go2);
    }

    [Test]
    public void PurchaseUpgrade_DeductsCoinsAndSavesLevel()
    {
        PlayerPrefs.DeleteAll();
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

        var go2 = new GameObject("shop2");
        var sm2 = go2.AddComponent<ShopManager>();
        sm2.availableUpgrades = new[] { data };
        typeof(ShopManager).GetMethod("LoadState", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(sm2, null);

        Assert.AreEqual(1f, sm2.GetUpgradeEffect(UpgradeType.MagnetDuration));
        Object.DestroyImmediate(go2);
    }

    [Test]
    public void MagnetPowerUp_UsesUpgradeEffect()
    {
        PlayerPrefs.DeleteAll();
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
    }
}
