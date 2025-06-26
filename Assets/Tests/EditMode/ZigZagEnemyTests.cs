using NUnit.Framework;
using UnityEngine;
using System.Reflection;

/// <summary>
/// Tests for the ZigZagEnemy ensuring pooled instances reset internal
/// state and that movement stops when timeScale is zero.
/// </summary>
public class ZigZagEnemyTests
{
    [Test]
    public void OnEnable_ResetsStartPositionAndTimer()
    {
        var go = new GameObject("enemy");
        var enemy = go.AddComponent<ZigZagEnemy>();
        enemy.OnEnable();

        var timerField = typeof(ZigZagEnemy).GetField("timer", BindingFlags.NonPublic | BindingFlags.Instance);
        timerField.SetValue(enemy, 5f);
        go.transform.position = new Vector3(3f, 4f, 0f);
        enemy.OnEnable();

        var startField = typeof(ZigZagEnemy).GetField("startPos", BindingFlags.NonPublic | BindingFlags.Instance);
        Vector3 start = (Vector3)startField.GetValue(enemy);
        float timer = (float)timerField.GetValue(enemy);

        Assert.AreEqual(go.transform.position, start);
        Assert.AreEqual(0f, timer);
        Object.DestroyImmediate(go);
    }

    [Test]
    public void Update_HaltsWhenTimeScaleZero()
    {
        var go = new GameObject("enemy");
        var enemy = go.AddComponent<ZigZagEnemy>();
        enemy.OnEnable();

        enemy.Update();
        float yBefore = go.transform.position.y;
        Time.timeScale = 0f;
        enemy.Update();
        float yAfter = go.transform.position.y;
        Time.timeScale = 1f;

        Assert.AreEqual(yBefore, yAfter);
        Object.DestroyImmediate(go);
    }
}
