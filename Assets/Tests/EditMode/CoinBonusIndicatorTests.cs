using NUnit.Framework;
using UnityEngine;
using TMPro; // Use TextMeshPro for UI elements in tests

/// <summary>
/// Tests for the CoinBonusIndicator component ensuring it shows and hides
/// based on the GameManager's coin bonus state.
/// </summary>
public class CoinBonusIndicatorTests
{
    [Test]
    public void Update_ShowsAndHidesLabel()
    {
        var gmObj = new GameObject("gm");
        var gm = gmObj.AddComponent<GameManager>();
        gm.StartGame();

        var uiObj = new GameObject("ui");
        var text = uiObj.AddComponent<TextMeshProUGUI>();
        var ind = uiObj.AddComponent<CoinBonusIndicator>();
        ind.timerLabel = text;

        // Initially inactive
        ind.Update();
        Assert.IsFalse(text.gameObject.activeSelf);

        gm.ActivateCoinBonus(1f, 2f);
        ind.Update();
        Assert.IsTrue(text.gameObject.activeSelf);

        // Expire the bonus
        typeof(GameManager).GetField("coinBonusTimer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(gm, 0f);
        ind.Update();
        Assert.IsFalse(text.gameObject.activeSelf);

        Object.DestroyImmediate(uiObj);
        Object.DestroyImmediate(gmObj);
    }
}
