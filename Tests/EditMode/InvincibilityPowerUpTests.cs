using NUnit.Framework;
using UnityEngine;
using System.Reflection;

/// <summary>
/// Tests covering <see cref="InvincibilityPowerUp"/> behaviour.
/// </summary>
public class InvincibilityPowerUpTests
{
    [Test]
    public void OnTriggerEnter_DurationIncludesUpgrade()
    {
        System.IO.File.Delete(System.IO.Path.Combine(
            Application.persistentDataPath, "savegame.json"));
        var saveObj = new GameObject("save");
        saveObj.AddComponent<SaveGameManager>();

        var data = new ShopManager.UpgradeData { type = UpgradeType.InvincibilityDuration, cost = 1, effect = 1f };
        var shopObj = new GameObject("shop");
        var sm = shopObj.AddComponent<ShopManager>();
        sm.availableUpgrades = new[] { data };
        typeof(ShopManager).GetMethod("LoadState", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(sm, null);
        var dictField = typeof(ShopManager).GetField("upgradeLevels", BindingFlags.NonPublic | BindingFlags.Instance);
        var levels = (System.Collections.Generic.Dictionary<UpgradeType, int>)dictField.GetValue(sm);
        levels[UpgradeType.InvincibilityDuration] = 2;
        dictField.SetValue(sm, levels);

        var player = new GameObject("player");
        player.tag = "Player";
        var shield = player.AddComponent<PlayerShield>();
        var playerCol = player.AddComponent<BoxCollider2D>();

        var powerObj = new GameObject("power");
        var inv = powerObj.AddComponent<InvincibilityPowerUp>();
        var powerCol = powerObj.AddComponent<BoxCollider2D>();
        powerCol.isTrigger = true;
        inv.duration = 1f;

        inv.OnTriggerEnter2D(playerCol);

        var timerField = typeof(PlayerShield).GetField("shieldTimer", BindingFlags.NonPublic | BindingFlags.Instance);
        float timer = (float)timerField.GetValue(shield);
        // Total time = base (1s) + 2 upgrade seconds = 3s
        Assert.AreEqual(3f, timer);

        Object.DestroyImmediate(powerObj);
        Object.DestroyImmediate(player);
        Object.DestroyImmediate(shopObj);
        Object.DestroyImmediate(saveObj);
    }

    [Test]
    public void ActivateShield_TimerCountsDown()
    {
        var player = new GameObject("player");
        var shield = player.AddComponent<PlayerShield>();

        shield.ActivateShield(1f);
        typeof(PlayerShield).GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(shield, null);
        typeof(PlayerShield).GetField("shieldTimer", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(shield, 0.5f);
        typeof(PlayerShield).GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(shield, null);

        var timerField = typeof(PlayerShield).GetField("shieldTimer", BindingFlags.NonPublic | BindingFlags.Instance);
        float timer = (float)timerField.GetValue(shield);
        Assert.Less(timer, 0.5f);

        Object.DestroyImmediate(player);
    }
}
