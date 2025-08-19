using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tests for <see cref="UIManager"/> verifying that leaderboard errors are
/// surfaced to the player through a clear message. The tests execute the
/// formatting logic directly to avoid dependency on Unity coroutines.
/// </summary>
public class UIManagerTests
{
    /// <summary>
    /// When the leaderboard callback reports failure the UI should present a
    /// friendly message instead of leaving the panel blank. This helps players
    /// understand that scores could not be retrieved rather than assuming there
    /// are none.
    /// </summary>
    [Test]
    public void DisplayScores_ShowsErrorMessageOnFailure()
    {
        // Prepare a minimal UI hierarchy containing the text element that will
        // display either scores or the error message.
        var uiObj = new GameObject("ui");
        var ui = uiObj.AddComponent<UIManager>();
        var textObj = new GameObject("txt");
        var text = textObj.AddComponent<Text>();
        ui.leaderboardText = text;

        // Simulate a failed leaderboard retrieval.
        ui.DisplayScores(null, false);

        // The text should now contain the human readable error string.
        Assert.AreEqual("Failed to load leaderboard.", text.text);

        Object.DestroyImmediate(textObj);
        Object.DestroyImmediate(uiObj);
    }
}

