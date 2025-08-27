using NUnit.Framework;
using UnityEngine;
using System.Reflection;

/// <summary>
/// Tests for the ShooterEnemy verifying the firing timer resets when the
/// object is disabled and re-enabled from a pool.
/// </summary>
public class ShooterEnemyTests
{
    [Test]
    public void OnDisable_ResetsShootTimer()
    {
        var go = new GameObject("enemy");
        var enemy = go.AddComponent<ShooterEnemy>();
        enemy.shootInterval = 1f;
        enemy.projectilePrefab = new GameObject("proj");
        enemy.Awake();
        enemy.OnEnable();

        var timerField = typeof(ShooterEnemy).GetField("shootTimer", BindingFlags.NonPublic | BindingFlags.Instance);
        timerField.SetValue(enemy, 0.2f);

        enemy.OnDisable();
        float timer = (float)timerField.GetValue(enemy);
        Assert.AreEqual(1f, timer);

        Object.DestroyImmediate(enemy.projectilePrefab);
        Object.DestroyImmediate(go);
    }
}
