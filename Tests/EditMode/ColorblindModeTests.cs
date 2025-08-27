using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools; // Provides LogAssert for log verification

/// <summary>
/// Unit tests covering <see cref="ColorblindMode"/> behaviour. These tests
/// confirm that the component applies the correct colours based on the global
/// <see cref="ColorblindManager"/> state, responds to mode changes and
/// unsubscribes from events when destroyed.
/// </summary>
public class ColorblindModeTests
{
    /// <summary>
    /// Helper method to build a GameObject with a SpriteRenderer and a
    /// ColorblindMode component for testing.
    /// </summary>
    private static (GameObject obj, SpriteRenderer renderer, ColorblindMode mode) CreateModeObject()
    {
        var go = new GameObject("colorblind");
        var renderer = go.AddComponent<SpriteRenderer>();
        var mode = go.AddComponent<ColorblindMode>();
        mode.targets = new[] { renderer };
        return (go, renderer, mode);
    }

    /// <summary>
    /// Verifies that the Start method tints target renderers using the current
    /// ColorblindManager setting.
    /// </summary>
    [Test]
    public void Start_AppliesInitialColor()
    {
        // Ensure the manager starts disabled so normalColor should be applied.
        ColorblindManager.SetEnabled(false);

        var (go, renderer, mode) = CreateModeObject();

        // Manually invoke Start via reflection because Unity does not call it in tests automatically.
        typeof(ColorblindMode).GetMethod("Start", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .Invoke(mode, null);

        Assert.AreEqual(mode.normalColor, renderer.material.color,
            "Renderer should use normalColor when colorblind mode is disabled at startup");

        Object.DestroyImmediate(go);
    }

    /// <summary>
    /// Ensures colour updates occur when the global colorblind setting changes.
    /// </summary>
    [Test]
    public void OnModeChanged_UpdatesRendererColors()
    {
        ColorblindManager.SetEnabled(false);
        var (go, renderer, mode) = CreateModeObject();
        typeof(ColorblindMode).GetMethod("Start", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .Invoke(mode, null);

        // Trigger a mode change to colorblind-enabled.
        ColorblindManager.SetEnabled(true);
        Assert.AreEqual(mode.colorblindColor, renderer.material.color,
            "Renderer should switch to colorblindColor after enabling mode");

        Object.DestroyImmediate(go);
    }

    /// <summary>
    /// Confirms the component unsubscribes from ColorblindManager events on
    /// destruction to avoid stale callbacks.
    /// </summary>
    [Test]
    public void OnDestroy_UnsubscribesFromManager()
    {
        ColorblindManager.SetEnabled(false);
        var (go, renderer, mode) = CreateModeObject();
        typeof(ColorblindMode).GetMethod("Start", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .Invoke(mode, null);

        // Destroy only the ColorblindMode component and then toggle the manager.
        // If the component unsubscribed correctly, the toggle should not
        // generate log messages or modify the renderer colour.
        Object.DestroyImmediate(mode);
        ColorblindManager.SetEnabled(true);
        LogAssert.NoUnexpectedReceived();

        // Renderer colour should remain at the initial normalColor value
        // because the component is no longer listening for changes.
        Assert.AreEqual(mode.normalColor, renderer.material.color,
            "Renderer colour should not change after component destruction");

        Object.DestroyImmediate(go);
    }
}

