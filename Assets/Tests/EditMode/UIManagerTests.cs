using NUnit.Framework;
using UnityEngine;
using TMPro; // TextMeshPro components used for UI labels
using System.Collections.Generic;
using System.IO;
using UnityEngine.TestTools; // Provides LogAssert for log verification

/// <summary>
/// Tests for <see cref="UIManager"/> covering both leaderboard error handling
/// and startup validation of critical references. The tests execute logic
/// directly without relying on Unity coroutines so they remain fast and
/// deterministic in edit mode.
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
        var text = textObj.AddComponent<TextMeshProUGUI>();
        ui.leaderboardText = text;

        // Simulate a failed leaderboard retrieval due to a network issue.
        ui.DisplayScores(null, false, LeaderboardClient.ErrorCode.NetworkError);

        // The text should now contain the human readable error string.
        Assert.AreEqual("Network unreachable.", text.text);

        Object.DestroyImmediate(textObj);
        Object.DestroyImmediate(uiObj);
    }

    /// <summary>
    /// Verifies that <see cref="UIManager.Awake"/> automatically caches the
    /// scene's <see cref="ParallaxBackground"/> when the serialized field is
    /// left unassigned. This avoids repeated FindObjectOfType lookups at
    /// runtime.
    /// </summary>
    [Test]
    public void Awake_CachesParallaxBackgroundWhenMissing()
    {
        // Create a background object that the manager should locate.
        var bgObj = new GameObject("bg");
        var bg = bgObj.AddComponent<ParallaxBackground>();

        // Create the UI manager with a null background reference.
        var uiObj = new GameObject("ui");
        var ui = uiObj.AddComponent<UIManager>();

        // Invoke the private Awake method so the caching logic runs.
        typeof(UIManager).GetMethod("Awake", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .Invoke(ui, null);

        Assert.AreSame(bg, ui.ParallaxBackground, "Awake should cache the background reference exactly once.");

        Object.DestroyImmediate(uiObj);
        Object.DestroyImmediate(bgObj);
    }

    /// <summary>
    /// Verifies that the manager emits an explicit error when a critical panel
    /// such as <see cref="UIManager.startPanel"/> is left unassigned. Other
    /// fields are populated to isolate the test to a single missing reference.
    /// </summary>
    [Test]
    public void Awake_LogsError_WhenStartPanelMissing()
    {
        var uiObj = new GameObject("ui");
        var ui = uiObj.AddComponent<UIManager>();

        // Populate other required fields so only startPanel is missing.
        ui.gameOverPanel = new GameObject();
        ui.pausePanel = new GameObject();
        ui.finalScoreLabel = new GameObject().AddComponent<TextMeshProUGUI>();
        ui.highScoreLabel = new GameObject().AddComponent<TextMeshProUGUI>();
        ui.coinScoreLabel = new GameObject().AddComponent<TextMeshProUGUI>();

        LogAssert.Expect(LogType.Error, "startPanel reference is missing; related UI features will be disabled to prevent errors.");
        typeof(UIManager).GetMethod("Awake", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .Invoke(ui, null);

        Object.DestroyImmediate(uiObj);
    }

    /// <summary>
    /// Ensures that label references are validated. When <see cref="UIManager.coinScoreLabel"/>
    /// is omitted, Awake should log an error so developers know why the coin
    /// display remains inactive.
    /// </summary>
    [Test]
    public void Awake_LogsError_WhenCoinScoreLabelMissing()
    {
        var uiObj = new GameObject("ui");
        var ui = uiObj.AddComponent<UIManager>();

        // Assign all panels and other labels except coinScoreLabel.
        ui.startPanel = new GameObject();
        ui.gameOverPanel = new GameObject();
        ui.pausePanel = new GameObject();
        ui.finalScoreLabel = new GameObject().AddComponent<TextMeshProUGUI>();
        ui.highScoreLabel = new GameObject().AddComponent<TextMeshProUGUI>();

        LogAssert.Expect(LogType.Error, "coinScoreLabel reference is missing; related UI features will be disabled to prevent errors.");
        typeof(UIManager).GetMethod("Awake", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .Invoke(ui, null);

        Object.DestroyImmediate(uiObj);
    }

    /// <summary>
    /// Ensures that <see cref="UIManager.ApplyFirstWorkshopItem"/> uses the
    /// cached background reference and succeeds when one is present. The test
    /// writes a temporary PNG to disk to mimic a workshop pack.
    /// </summary>
#if UNITY_STANDALONE
    [Test]
    public void ApplyFirstWorkshopItem_UsesCachedBackground()
    {
        // Set up a background with a SpriteRenderer to receive the replacement sprite.
        var bgObj = new GameObject("bg");
        bgObj.AddComponent<SpriteRenderer>();
        var bg = bgObj.AddComponent<ParallaxBackground>();

        // Create the UI manager and assign the background via reflection to
        // simulate inspector wiring.
        var uiObj = new GameObject("ui");
        var ui = uiObj.AddComponent<UIManager>();
        typeof(UIManager).GetField("parallaxBackground", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .SetValue(ui, bg);

        // Create a temporary directory containing a PNG so the method's fallback
        // path executes.
        string tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        string pngPath = Path.Combine(tempDir, "bg.png");
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        File.WriteAllBytes(pngPath, tex.EncodeToPNG());

        // Inject the directory into the manager's downloaded pack list.
        typeof(UIManager).GetField("downloadedPacks", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .SetValue(ui, new List<string> { tempDir });

        // The method should apply the sprite without throwing and update the renderer.
        Assert.DoesNotThrow(() => ui.ApplyFirstWorkshopItem());
        Assert.IsNotNull(bgObj.GetComponent<SpriteRenderer>().sprite, "Workshop application should set the background sprite.");

        // Clean up objects and temporary files.
        Object.DestroyImmediate(uiObj);
        Object.DestroyImmediate(bgObj);
        Directory.Delete(tempDir, true);
    }
#endif
}

