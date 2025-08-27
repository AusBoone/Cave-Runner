using NUnit.Framework;
using UnityEngine;
using System.Reflection;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Unit tests verifying that <see cref="ShieldPowerUp"/> correctly grants the
/// player a shield, stacks duration with shop upgrades and returns itself to
/// an <see cref="ObjectPool"/> when collected. These behaviours ensure the
/// power-up interacts reliably with other systems during gameplay.
/// </summary>
public class ShieldPowerUpTests
{
    [Test]
    public void OnTriggerEnter_ActivatesPlayerShield()
    {
        // Setup a player with the shield component so OnTriggerEnter can enable it
        var player = new GameObject("player");
        player.tag = "Player";
        var shield = player.AddComponent<PlayerShield>();
        var playerCol = player.AddComponent<BoxCollider2D>();

        // Power-up instance that will trigger the shield
        var powerObj = new GameObject("power");
        var sp = powerObj.AddComponent<ShieldPowerUp>();
        var powerCol = powerObj.AddComponent<BoxCollider2D>();
        powerCol.isTrigger = true;
        sp.duration = 2f;

        // Simulate the collision
        sp.OnTriggerEnter2D(playerCol);

        // Access private timer field to confirm activation
        var timerField = typeof(PlayerShield).GetField("shieldTimer", BindingFlags.NonPublic | BindingFlags.Instance);
        float timer = (float)timerField.GetValue(shield);

        // The timer should equal the configured duration so the player becomes invulnerable.
        Assert.AreEqual(2f, timer);

        Object.DestroyImmediate(powerObj);
        Object.DestroyImmediate(player);
    }

    [Test]
    public void OnTriggerEnter_DurationStacksWithShopUpgrade()
    {
        // Ensure a clean save file so upgrade values are predictable
        System.IO.File.Delete(System.IO.Path.Combine(
            Application.persistentDataPath, "savegame.json"));
        var saveObj = new GameObject("save");
        saveObj.AddComponent<SaveGameManager>();
        var data = new ShopManager.UpgradeData { type = UpgradeType.ShieldDuration, cost = 1, effect = 1f };

        // Setup the shop with a purchased upgrade level of 2
        var shopObj = new GameObject("shop");
        var sm = shopObj.AddComponent<ShopManager>();
        sm.availableUpgrades = new[] { data };
        typeof(ShopManager).GetMethod("LoadState", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(sm, null);
        var dictField = typeof(ShopManager).GetField("upgradeLevels", BindingFlags.NonPublic | BindingFlags.Instance);
        var levels = (System.Collections.Generic.Dictionary<UpgradeType, int>)dictField.GetValue(sm);
        levels[UpgradeType.ShieldDuration] = 2; // simulate two upgrades purchased
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

        // Base duration (2s) should be increased by 2 upgrade seconds for a total of 4s.
        Assert.AreEqual(4f, timer);

        Object.DestroyImmediate(powerObj);
        Object.DestroyImmediate(player);
        Object.DestroyImmediate(shopObj);
        Object.DestroyImmediate(saveObj);
    }

    [Test]
    public void OnTriggerEnter_ReturnsObjectToPool()
    {
        // Create a pool capable of spawning shield power-ups
        var poolObj = new GameObject("pool");
        var pool = poolObj.AddComponent<ObjectPool>();
        pool.prefab = new GameObject("prefab");
        pool.prefab.AddComponent<ShieldPowerUp>();
        pool.prefab.AddComponent<BoxCollider2D>().isTrigger = true;

        // Spawn an instance from the pool
        var instance = pool.GetObject(Vector3.zero, Quaternion.identity);
        var sp = instance.GetComponent<ShieldPowerUp>();

        var player = new GameObject("player");
        player.tag = "Player";
        player.AddComponent<PlayerShield>();
        var playerCol = player.AddComponent<BoxCollider2D>();

        sp.OnTriggerEnter2D(playerCol);

        // After collection the object should be inactive and parented back to the pool
        Assert.IsFalse(instance.activeSelf, "Collected power-up should be disabled so it can be reused");
        Assert.AreEqual(pool.transform, instance.transform.parent, "Returned instance should rejoin its pool for recycling");

        // Getting another object should supply the same instance, proving it was queued
        var reused = pool.GetObject(Vector3.one, Quaternion.identity);
        Assert.AreSame(instance, reused, "Pooling avoids allocations by reusing the same GameObject");

        Object.DestroyImmediate(pool.prefab);
        Object.DestroyImmediate(poolObj);
        Object.DestroyImmediate(player);
    }

#if ENABLE_INPUT_SYSTEM
    [Test]
    public void OnTriggerEnter_TriggersRumble()
    {
        var gamepad = InputSystem.AddDevice<Gamepad>();

        var player = new GameObject("player");
        player.tag = "Player";
        player.AddComponent<PlayerShield>();
        var playerCol = player.AddComponent<BoxCollider2D>();

        var powerObj = new GameObject("power");
        var sp = powerObj.AddComponent<ShieldPowerUp>();
        var powerCol = powerObj.AddComponent<BoxCollider2D>();
        powerCol.isTrigger = true;

        InputManager.SetRumbleEnabled(true);
        sp.OnTriggerEnter2D(playerCol);

        FieldInfo field = typeof(InputManager).GetField("rumbleRoutine", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(field.GetValue(null), "Collecting the power-up should start a rumble coroutine");

        Object.DestroyImmediate(powerObj);
        Object.DestroyImmediate(player);
        InputSystem.RemoveDevice(gamepad);
    }
#endif
}
