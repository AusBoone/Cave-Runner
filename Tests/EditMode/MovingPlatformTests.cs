using NUnit.Framework;
using UnityEngine;
using System.Reflection;

/// <summary>
/// Tests for the MovingPlatform component ensuring pooled instances reset
/// their starting position and that movement halts when the game is paused.
/// </summary>
public class MovingPlatformTests
{
    [Test]
    public void OnEnable_ResetsStartPositionAndTimer()
    {
        var go = new GameObject("platform");
        var mp = go.AddComponent<MovingPlatform>();

        // Move the object and trigger OnEnable as if it were spawned
        go.transform.position = new Vector3(2f, 3f, 0f);
        mp.OnEnable();

        // Change position and timer then simulate returning from a pool
        var timerField = typeof(MovingPlatform).GetField("timer", BindingFlags.NonPublic | BindingFlags.Instance);
        timerField.SetValue(mp, 5f);
        go.transform.position = new Vector3(5f, 6f, 0f);
        mp.OnEnable();

        var startPosField = typeof(MovingPlatform).GetField("startPos", BindingFlags.NonPublic | BindingFlags.Instance);
        Vector3 startPos = (Vector3)startPosField.GetValue(mp);
        float timer = (float)timerField.GetValue(mp);

        Assert.AreEqual(go.transform.position, startPos);
        Assert.AreEqual(0f, timer);

        Object.DestroyImmediate(go);
    }

    [Test]
    public void Update_HaltsWhenTimeScaleZero()
    {
        var go = new GameObject("platform");
        var mp = go.AddComponent<MovingPlatform>();
        mp.OnEnable();

        // Pre-set timer to simulate elapsed time so the platform would move
        var timerField = typeof(MovingPlatform).GetField("timer", BindingFlags.NonPublic | BindingFlags.Instance);
        timerField.SetValue(mp, 1f);
        mp.Update();
        float yBeforePause = go.transform.position.y;

        Time.timeScale = 0f;
        mp.Update();
        float yAfterPause = go.transform.position.y;
        Time.timeScale = 1f;

        Assert.AreEqual(yBeforePause, yAfterPause);
        Object.DestroyImmediate(go);
    }
}
