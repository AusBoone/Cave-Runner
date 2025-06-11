using NUnit.Framework;
using UnityEngine;
using System.Reflection;

/// <summary>
/// Tests for the CoinMagnet component verifying activation
/// and deactivation logic.
/// </summary>
public class CoinMagnetTests
{
    [Test]
    public void ActivateMagnet_SetsTimerAndActive()
    {
        var player = new GameObject("player");
        var magnet = player.AddComponent<CoinMagnet>();
        magnet.ActivateMagnet(2f);

        var activeField = typeof(CoinMagnet).GetField("magnetActive", BindingFlags.NonPublic | BindingFlags.Instance);
        var timerField = typeof(CoinMagnet).GetField("magnetTimer", BindingFlags.NonPublic | BindingFlags.Instance);

        bool active = (bool)activeField.GetValue(magnet);
        float timer = (float)timerField.GetValue(magnet);

        Assert.IsTrue(active);
        Assert.AreEqual(2f, timer);

        Object.DestroyImmediate(player);
    }

    [Test]
    public void Update_DisablesWhenTimerExpires()
    {
        var player = new GameObject("player");
        var magnet = player.AddComponent<CoinMagnet>();

        var activeField = typeof(CoinMagnet).GetField("magnetActive", BindingFlags.NonPublic | BindingFlags.Instance);
        var timerField = typeof(CoinMagnet).GetField("magnetTimer", BindingFlags.NonPublic | BindingFlags.Instance);

        activeField.SetValue(magnet, true);
        timerField.SetValue(magnet, 0f);

        magnet.Update();

        bool active = (bool)activeField.GetValue(magnet);
        Assert.IsFalse(active);

        Object.DestroyImmediate(player);
    }
}
