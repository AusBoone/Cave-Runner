// SettingsMenuTests.cs
// -----------------------------------------------------------------------------
// Comprehensive suite verifying SettingsMenu's hooks for user-configurable
// options. Coverage includes:
//   - Language dropdown population using TextMeshPro's TMP_Dropdown.
//   - Rumble toggle forwarding to InputManager.
//   - Hardcore mode toggle propagating to SaveGameManager and GameManager.
//   - Music and effects volume sliders applying values through AudioManager.
// These tests guard against regressions as UI controls evolve.
// -----------------------------------------------------------------------------
using NUnit.Framework;
using UnityEngine;
using TMPro; // Use TextMeshPro for UI elements in tests
using System.Collections.Generic;
using System.Reflection; // For setting singleton instances in stubs

/// <summary>
/// Tests for <see cref="SettingsMenu"/> covering language dropdown population
/// and selection behaviour.
/// </summary>
public class SettingsMenuTests
{
    /// <summary>
    /// Resets global singletons and PlayerPrefs so each test runs in a clean
    /// environment. This prevents state leakage between tests.
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        PlayerPrefs.DeleteAll();

        // Destroy existing SaveGameManager instance if present and clear the
        // backing field so new test instances can take its place.
        if (SaveGameManager.Instance != null)
        {
            Object.DestroyImmediate(SaveGameManager.Instance.gameObject);
            typeof(SaveGameManager)
                .GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic)
                .SetValue(null, null);
        }

        // Repeat for GameManager and AudioManager to avoid cross-test pollution.
        if (GameManager.Instance != null)
        {
            Object.DestroyImmediate(GameManager.Instance.gameObject);
            typeof(GameManager)
                .GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic)
                .SetValue(null, null);
        }
        if (AudioManager.Instance != null)
        {
            Object.DestroyImmediate(AudioManager.Instance.gameObject);
            typeof(AudioManager)
                .GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic)
                .SetValue(null, null);
        }

        // Ensure rumble starts disabled so tests observe intentional changes.
        InputManager.SetRumbleEnabled(false);
    }

    // ------------------------------------------------------------------
    // Helper stub components
    // ------------------------------------------------------------------

    /// <summary>
    /// Lightweight SaveGameManager used solely for exposing the singleton
    /// instance without triggering disk IO or asynchronous loading.
    /// </summary>
    private class StubSaveGameManager : SaveGameManager
    {
        new void Awake()
        {
            typeof(SaveGameManager)
                .GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic)
                .SetValue(null, this);
        }

        void OnDestroy()
        {
            typeof(SaveGameManager)
                .GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic)
                .SetValue(null, null);
        }
    }

    /// <summary>
    /// Minimal GameManager that solely registers itself as the singleton
    /// instance. Production initialization is skipped to keep tests focused on
    /// property propagation.
    /// </summary>
    private class StubGameManager : GameManager
    {
        new void Awake()
        {
            typeof(GameManager)
                .GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic)
                .SetValue(null, this);
        }

        void OnDestroy()
        {
            typeof(GameManager)
                .GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic)
                .SetValue(null, null);
        }
    }

    [Test]
    public void Start_PopulatesLanguageDropdown()
    {
        // Set an initial language so Start selects a predictable option.
        LocalizationManager.SetLanguage("es");

        // Prepare a TMP_Dropdown to receive the populated language options.
        var dropdownObj = new GameObject("dropdown");
        var dropdown = dropdownObj.AddComponent<TMP_Dropdown>();

        // Attach the SettingsMenu and assign the dropdown field.
        var menuObj = new GameObject("menu");
        var menu = menuObj.AddComponent<SettingsMenu>();
        menu.languageDropdown = dropdown;

        // Act: run Start to populate options based on LocalizationManager.
        menu.Start();

        // Build a list from the available languages for comparison.
        List<string> expected = new List<string>(LocalizationManager.AvailableLanguages);

        // Ensure the dropdown mirrors the language list exactly.
        Assert.AreEqual(expected.Count, dropdown.options.Count, "Dropdown should contain an entry for each available language");

        // The selected value should match the current language set above.
        Assert.AreEqual("es", dropdown.options[dropdown.value].text, "Dropdown should select the active language");

        // Clean up temporary objects to avoid polluting the scene.
        Object.DestroyImmediate(menuObj);
        Object.DestroyImmediate(dropdownObj);
    }

    /// <summary>
    /// Toggling the rumble option should update InputManager's static
    /// RumbleEnabled flag so future haptic requests reflect the new setting.
    /// </summary>
    [Test]
    public void ToggleRumble_UpdatesInputManager()
    {
        var menuObj = new GameObject("menu");
        var menu = menuObj.AddComponent<SettingsMenu>();

        // Act: enable rumble via the settings menu.
        menu.ToggleRumble(true);

        // Verify the static flag mirrors the toggle value.
        Assert.IsTrue(InputManager.RumbleEnabled, "Rumble toggle should enable rumble in InputManager");

        Object.DestroyImmediate(menuObj);
    }

    /// <summary>
    /// Enabling hardcore mode should persist the change through
    /// SaveGameManager and also update GameManager so gameplay immediately
    /// reflects the tougher ruleset.
    /// </summary>
    [Test]
    public void ToggleHardcore_UpdatesManagers()
    {
        // Supply lightweight singleton instances for the managers accessed by
        // SettingsMenu.
        var saveObj = new GameObject("save");
        saveObj.AddComponent<StubSaveGameManager>();
        var gmObj = new GameObject("gm");
        gmObj.AddComponent<StubGameManager>();

        var menuObj = new GameObject("menu");
        var menu = menuObj.AddComponent<SettingsMenu>();

        // Act: enable hardcore mode through the menu.
        menu.ToggleHardcore(true);

        // Both manager singletons should now report the new setting.
        Assert.IsTrue(SaveGameManager.Instance.HardcoreMode, "SaveGameManager should store hardcore mode state");
        Assert.IsTrue(GameManager.Instance.HardcoreMode, "GameManager should reflect hardcore mode immediately");

        Object.DestroyImmediate(menuObj);
        Object.DestroyImmediate(gmObj);
        Object.DestroyImmediate(saveObj);
    }

    /// <summary>
    /// Adjusting the volume sliders should forward clamped values to
    /// AudioManager and persist them via SaveGameManager.
    /// </summary>
    [Test]
    public void ChangeVolume_SlidersUpdateAudioManager()
    {
        // Provide stubbed managers required by SettingsMenu.
        var saveObj = new GameObject("save");
        saveObj.AddComponent<StubSaveGameManager>();
        var audioObj = new GameObject("audio");
        var am = audioObj.AddComponent<AudioManager>();
        am.musicSource = audioObj.AddComponent<AudioSource>();
        am.musicSourceSecondary = audioObj.AddComponent<AudioSource>();
        am.effectsSource = audioObj.AddComponent<AudioSource>();

        var menuObj = new GameObject("menu");
        var menu = menuObj.AddComponent<SettingsMenu>();

        // Act: update both music and effects volumes through the menu.
        menu.ChangeMusicVolume(0.2f);
        menu.ChangeEffectsVolume(0.4f);

        // AudioManager's audio sources should reflect the new levels.
        Assert.AreEqual(0.2f, am.musicSource.volume, 1e-4f, "Music volume should be applied to primary source");
        Assert.AreEqual(0.2f, am.musicSourceSecondary.volume, 1e-4f, "Music volume should be applied to secondary source");
        Assert.AreEqual(0.4f, am.effectsSource.volume, 1e-4f, "Effects volume should be applied to effects source");

        // SaveGameManager should persist the chosen volumes for future sessions.
        Assert.AreEqual(0.2f, SaveGameManager.Instance.MusicVolume, 1e-4f, "Music volume should be persisted");
        Assert.AreEqual(0.4f, SaveGameManager.Instance.EffectsVolume, 1e-4f, "Effects volume should be persisted");

        Object.DestroyImmediate(menuObj);
        Object.DestroyImmediate(audioObj);
        Object.DestroyImmediate(saveObj);
    }
}
