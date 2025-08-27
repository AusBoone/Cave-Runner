// Unity edit mode unit tests verifying colorblind settings.
// Run via the Unity Test Runner.

using NUnit.Framework;
using UnityEngine;
using System.Reflection;

/// <summary>
/// Edit mode tests for <see cref="ColorblindManager"/> and
/// <see cref="ColorblindMode"/> components.
///
/// The tests verify that enabling/disabling colorblind mode
/// correctly persists the preference via <see cref="PlayerPrefs"/>
/// and that registered <see cref="ColorblindMode"/> components
/// receive change notifications.
///
/// Each test cleans PlayerPrefs to avoid cross test contamination.
/// </summary>
public class ColorblindManagerTests
{
    private const string PrefKey = "ColorblindMode"; // matches constant in ColorblindManager

    [SetUp]
    public void ClearPrefs()
    {
        // Ensure a known starting state for each test by clearing preferences
        PlayerPrefs.DeleteAll();
        ColorblindManager.SetEnabled(false); // reset static state
    }

    [Test]
    public void SetEnabled_PersistsPreference()
    {
        // Enabling the mode should write a value of 1 to PlayerPrefs
        ColorblindManager.SetEnabled(true);
        Assert.AreEqual(1, PlayerPrefs.GetInt(PrefKey),
            "Enabling colorblind mode should store 1 in PlayerPrefs");

        // Disabling the mode should store 0
        ColorblindManager.SetEnabled(false);
        Assert.AreEqual(0, PlayerPrefs.GetInt(PrefKey),
            "Disabling colorblind mode should store 0 in PlayerPrefs");
    }

    [Test]
    public void SetEnabled_NotifiesRegisteredComponents()
    {
        // Create an object with a renderer to track color changes
        var obj = new GameObject("cb");
        var renderer = obj.AddComponent<SpriteRenderer>();
        var mode = obj.AddComponent<ColorblindMode>();
        mode.targets = new Renderer[] { renderer };
        mode.normalColor = Color.red;
        mode.colorblindColor = Color.green;

        // Manually invoke Start to register with ColorblindManager
        typeof(ColorblindMode)
            .GetMethod("Start", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(mode, null);

        // Initial color should match the normal color
        Assert.AreEqual(mode.normalColor, renderer.material.color);

        // Trigger the change event
        ColorblindManager.SetEnabled(true);
        Assert.AreEqual(mode.colorblindColor, renderer.material.color,
            "Renderer color should update when colorblind mode toggles");

        Object.DestroyImmediate(obj);
    }
}
