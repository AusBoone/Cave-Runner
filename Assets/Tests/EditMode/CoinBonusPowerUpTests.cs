using NUnit.Framework;
using UnityEngine;
using System.Reflection;

/// <summary>
/// Tests for the CoinBonusPowerUp component ensuring the coin bonus duration
/// includes upgrade effects.
/// </summary>
public class CoinBonusPowerUpTests
{
    [Test]
    public void CoinBonusPowerUp_UsesUpgradeEffect()
    {
        System.IO.File.Delete(System.IO.Path.Combine(
            Application.persistentDataPath, "savegame.json"));
        var saveObj = new GameObject("save");
        saveObj.AddComponent<SaveGameManager>();
        var data = new ShopManager.UpgradeData { type = UpgradeType.CoinBonusDuration, cost = 1, effect = 1f };

        var shopObj = new GameObject("shop");
        var sm = shopObj.AddComponent<ShopManager>();
        sm.availableUpgrades = new[] { data };
        typeof(ShopManager).GetMethod("LoadState", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(sm, null);

        var dictField = typeof(ShopManager).GetField("upgradeLevels", BindingFlags.NonPublic | BindingFlags.Instance);
        var levels = (System.Collections.Generic.Dictionary<UpgradeType, int>)dictField.GetValue(sm);
        levels[UpgradeType.CoinBonusDuration] = 1;
        dictField.SetValue(sm, levels);

        var gmObj = new GameObject("gm");
        var gm = gmObj.AddComponent<GameManager>();

        var player = new GameObject("player");
        player.tag = "Player";
        var playerCol = player.AddComponent<BoxCollider2D>();

        var powerObj = new GameObject("power");
        var cb = powerObj.AddComponent<CoinBonusPowerUp>();
        var powerCol = powerObj.AddComponent<BoxCollider2D>();
        powerCol.isTrigger = true;
        cb.duration = 2f;
        cb.multiplier = 2f;

        cb.OnTriggerEnter2D(playerCol);

        var timerField = typeof(GameManager).GetField("coinBonusTimer", BindingFlags.NonPublic | BindingFlags.Instance);
        float timer = (float)timerField.GetValue(gm);

        Assert.AreEqual(3f, timer);

        Object.DestroyImmediate(powerObj);
        Object.DestroyImmediate(player);
        Object.DestroyImmediate(gmObj);
        Object.DestroyImmediate(shopObj);
        Object.DestroyImmediate(saveObj);
    }
}
