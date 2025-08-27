using NUnit.Framework;
using UnityEngine;
using TMPro; // Using TextMeshPro components in tests
using System.Reflection;

/// <summary>
/// Tests for the coin combo system in <see cref="GameManager"/> ensuring the
/// multiplier cap and validation behave as expected.
/// </summary>
public class ComboMultiplierTests
{
    /// <summary>
    /// Subclass of GameManager that counts combo increase events so tests can
    /// verify feedback only occurs while the multiplier climbs.
    /// </summary>
    private class TestGameManager : GameManager
    {
        public int comboEvents;
        protected override void OnComboIncreased()
        {
            comboEvents++;
        }
    }

    [Test]
    public void AddCoins_MultiplierClampedAndUIUpdated()
    {
        var gmObj = new GameObject("gm");
        var gm = gmObj.AddComponent<TestGameManager>();

        // Provide a label so UpdateMultiplierLabel can display the current value.
        var labelObj = new GameObject("label");
        // TextMeshProUGUI simulates the in-game combo label for testing.
        gm.comboLabel = labelObj.AddComponent<TextMeshProUGUI>();

        // Read the configured cap via reflection to keep the test flexible.
        int max = (int)typeof(GameManager).GetField("maxComboMultiplier", BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(gm);

        // Collect more coins than the cap allows; multiplier should stop increasing.
        for (int i = 0; i < max + 5; i++)
        {
            gm.AddCoins(1);
        }

        Assert.AreEqual(max, gm.GetCoinComboMultiplier(),
            "Combo multiplier should never exceed the configured maximum");
        Assert.AreEqual("x" + max, gm.comboLabel.text,
            "UI label should display the capped multiplier value");
        Assert.AreEqual(max - 1, gm.comboEvents,
            "Feedback should only trigger while the multiplier increases");

        Object.DestroyImmediate(labelObj);
        Object.DestroyImmediate(gmObj);
    }

    [Test]
    public void AddCoins_NonPositiveAmount_Throws()
    {
        var gmObj = new GameObject("gm");
        var gm = gmObj.AddComponent<GameManager>();

        Assert.Throws<System.ArgumentException>(() => gm.AddCoins(0),
            "Zero coins should be rejected to avoid silent logic errors");
        Assert.Throws<System.ArgumentException>(() => gm.AddCoins(-1),
            "Negative coin values must also be rejected");

        Object.DestroyImmediate(gmObj);
    }
}
