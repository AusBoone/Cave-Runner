using NUnit.Framework;
using UnityEngine;
using System.Reflection;

/// <summary>
/// Tests ensuring <see cref="DoubleJumpPowerUp"/> correctly grants an extra air jump
/// and respects shop upgrade duration bonuses.
/// </summary>
public class DoubleJumpPowerUpTests
{
    [Test]
    public void OnTriggerEnter_ActivatesDoubleJumpWithUpgrade()
    {
        // Setup save and shop so upgrade values can be applied
        System.IO.File.Delete(System.IO.Path.Combine(
            Application.persistentDataPath, "savegame.json"));
        var saveObj = new GameObject("save");
        saveObj.AddComponent<SaveGameManager>();

        var data = new ShopManager.UpgradeData { type = UpgradeType.DoubleJumpDuration, cost = 1, effect = 1f };
        var shopObj = new GameObject("shop");
        var sm = shopObj.AddComponent<ShopManager>();
        sm.availableUpgrades = new[] { data };
        typeof(ShopManager).GetMethod("LoadState", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(sm, null);
        var dictField = typeof(ShopManager).GetField("upgradeLevels", BindingFlags.NonPublic | BindingFlags.Instance);
        var levels = (System.Collections.Generic.Dictionary<UpgradeType, int>)dictField.GetValue(sm);
        levels[UpgradeType.DoubleJumpDuration] = 1;
        dictField.SetValue(sm, levels);

        // Player with controller component
        var player = new GameObject("player");
        player.tag = "Player";
        var pc = player.AddComponent<PlayerController>();
        player.AddComponent<CapsuleCollider2D>();

        var powerObj = new GameObject("power");
        var dj = powerObj.AddComponent<DoubleJumpPowerUp>();
        var powerCol = powerObj.AddComponent<BoxCollider2D>();
        powerCol.isTrigger = true;
        dj.duration = 2f;

        dj.OnTriggerEnter2D(player.GetComponent<CapsuleCollider2D>());

        FieldInfo timerField = typeof(PlayerController).GetField("doubleJumpTimer", BindingFlags.NonPublic | BindingFlags.Instance);
        float timer = (float)timerField.GetValue(pc);

        // Base duration (2s) plus 1s from the upgrade
        Assert.AreEqual(3f, timer);

        Object.DestroyImmediate(powerObj);
        Object.DestroyImmediate(player);
        Object.DestroyImmediate(shopObj);
        Object.DestroyImmediate(saveObj);
    }

    [Test]
    public void ActivateDoubleJump_DecrementsOverTime()
    {
        var player = new GameObject("player");
        var pc = player.AddComponent<PlayerController>();
        player.AddComponent<CapsuleCollider2D>();

        pc.ActivateDoubleJump(1f);

        // Call Update twice to simulate time passing
        pc.Update();
        typeof(PlayerController).GetField("doubleJumpTimer", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(pc, 0.5f);
        pc.Update();

        FieldInfo timerField = typeof(PlayerController).GetField("doubleJumpTimer", BindingFlags.NonPublic | BindingFlags.Instance);
        float timer = (float)timerField.GetValue(pc);
        Assert.Less(timer, 0.5f);

        Object.DestroyImmediate(player);
    }
}
