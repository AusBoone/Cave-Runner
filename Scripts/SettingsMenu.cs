// SettingsMenu.cs
// -----------------------------------------------------------------------------
// Handles runtime configuration options such as key bindings, audio levels and
// accessibility toggles. Text fields now use TextMeshPro's TMP_Text for improved
// clarity across resolutions. 2024 UI refresh: the language selector switched
// from Unity's legacy Dropdown to TextMeshPro's TMP_Dropdown so option labels
// remain sharp and support rich text styling.
// -----------------------------------------------------------------------------
using UnityEngine;
using UnityEngine.UI; // Still required for Slider and Toggle components
using TMPro;          // Provides TMP_Text and TMP_Dropdown components
using System.Collections.Generic; // Used to build dropdown option lists

/// <summary>
/// Provides UI hooks for changing key bindings and colorblind mode. When the
/// new Input System package is installed the menu can start interactive
/// rebinding operations so players can customise controls at runtime.
/// </summary>
public class SettingsMenu : MonoBehaviour
{
    public TMP_Text jumpKeyLabel;
    public TMP_Text slideKeyLabel;
    public TMP_Text pauseKeyLabel;
    public Toggle colorblindToggle;
    public Slider musicVolumeSlider;
    public TMP_Text musicVolumeLabel;
    public Slider effectsVolumeSlider;
    public TMP_Text effectsVolumeLabel;
    public TMP_Dropdown languageDropdown; // TextMeshPro dropdown for language selection
    public Toggle rumbleToggle;
    public Toggle hardcoreToggle; // toggle for hardcore mode

    [Tooltip("Tutorial manager used to show help panels.")]
    public TutorialManager tutorialManager;

    /// <summary>
    /// Populates UI elements with the current settings on start.
    /// </summary>
    void Start()
    {
        if (jumpKeyLabel != null) jumpKeyLabel.text = InputManager.JumpKey.ToString();
        if (slideKeyLabel != null) slideKeyLabel.text = InputManager.SlideKey.ToString();
        if (pauseKeyLabel != null) pauseKeyLabel.text = InputManager.PauseKey.ToString();
        if (colorblindToggle != null) colorblindToggle.isOn = ColorblindManager.Enabled;
        if (rumbleToggle != null) rumbleToggle.isOn = InputManager.RumbleEnabled;
        if (hardcoreToggle != null && SaveGameManager.Instance != null)
            hardcoreToggle.isOn = SaveGameManager.Instance.HardcoreMode;
        if (musicVolumeSlider != null && SaveGameManager.Instance != null)
        {
            musicVolumeSlider.value = SaveGameManager.Instance.MusicVolume;
            UpdateMusicVolumeLabel(musicVolumeSlider.value);
        }
        if (effectsVolumeSlider != null && SaveGameManager.Instance != null)
        {
            effectsVolumeSlider.value = SaveGameManager.Instance.EffectsVolume;
            UpdateEffectsVolumeLabel(effectsVolumeSlider.value);
        }
        if (languageDropdown != null)
        {
            // Rebuild dropdown options from the languages exposed by LocalizationManager.
            languageDropdown.ClearOptions();

            // TMP_Dropdown uses its own OptionData type so we create a matching list.
            var options = new List<TMP_Dropdown.OptionData>();
            foreach (string lang in LocalizationManager.AvailableLanguages)
            {
                // Store each language code as both the option's label and value.
                options.Add(new TMP_Dropdown.OptionData(lang));
            }

            languageDropdown.options = options;

            // Select the language currently active in the game or fallback to the first entry.
            string current = SaveGameManager.Instance != null ?
                SaveGameManager.Instance.Language : LocalizationManager.CurrentLanguage;
            int index = options.FindIndex(o => o.text == current);
            languageDropdown.value = index >= 0 ? index : 0;
        }
    }

    /// <summary>
    /// Updates the jump key binding based on a UI selection.
    /// </summary>
    public void SetJumpKey(string keyName)
    {
        if (System.Enum.TryParse(keyName, out KeyCode key))
        {
            InputManager.SetJumpKey(key);
            if (jumpKeyLabel != null) jumpKeyLabel.text = key.ToString();
        }
    }

    /// <summary>
    /// Updates the pause key binding based on a UI selection.
    /// </summary>
    public void SetPauseKey(string keyName)
    {
        if (System.Enum.TryParse(keyName, out KeyCode key))
        {
            InputManager.SetPauseKey(key);
            if (pauseKeyLabel != null) pauseKeyLabel.text = key.ToString();
        }
    }

    /// <summary>
    /// Updates the slide key binding based on a UI selection.
    /// </summary>
    public void SetSlideKey(string keyName)
    {
        if (System.Enum.TryParse(keyName, out KeyCode key))
        {
            InputManager.SetSlideKey(key);
            if (slideKeyLabel != null) slideKeyLabel.text = key.ToString();
        }
    }
#if ENABLE_INPUT_SYSTEM
    /// <summary>
    /// Begins rebinding for the jump action using the new Input System.
    /// </summary>
    public void RebindJump()
    {
        InputManager.StartRebindJump(this, jumpKeyLabel);
    }

    /// <summary>
    /// Begins rebinding for the slide action.
    /// </summary>
    public void RebindSlide()
    {
        InputManager.StartRebindSlide(this, slideKeyLabel);
    }

    /// <summary>
    /// Begins rebinding for the pause action.
    /// </summary>
    public void RebindPause()
    {
        InputManager.StartRebindPause(this, pauseKeyLabel);
    }
#endif

    /// <summary>
    /// Enables or disables colorblind mode from the toggle.
    /// </summary>
    public void ToggleColorblind(bool value)
    {
        ColorblindManager.SetEnabled(value);
    }

    /// <summary>
    /// Enables or disables controller rumble via the settings toggle.
    /// </summary>
    public void ToggleRumble(bool value)
    {
        InputManager.SetRumbleEnabled(value);
    }

    /// <summary>
    /// Toggle hardcore mode via settings. Saves the value and informs the
    /// active <see cref="GameManager"/> if present.
    /// </summary>
    public void ToggleHardcore(bool value)
    {
        if (SaveGameManager.Instance != null)
            SaveGameManager.Instance.HardcoreMode = value;

        if (GameManager.Instance != null)
            GameManager.Instance.HardcoreMode = value;
    }

    /// <summary>
    /// Callback for the music volume slider. Persists the value and applies
    /// it through <see cref="AudioManager"/>.
    /// </summary>
    public void ChangeMusicVolume(float value)
    {
        if (SaveGameManager.Instance != null)
        {
            SaveGameManager.Instance.MusicVolume = value;
        }
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetMusicVolume(value);
        }
        UpdateMusicVolumeLabel(value);
    }

    /// <summary>
    /// Callback for the effects volume slider.
    /// </summary>
    public void ChangeEffectsVolume(float value)
    {
        if (SaveGameManager.Instance != null)
        {
            SaveGameManager.Instance.EffectsVolume = value;
        }
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetEffectsVolume(value);
        }
        UpdateEffectsVolumeLabel(value);
    }

    /// <summary>
    /// Callback for the language TMP_Dropdown. Persists the selected language and
    /// reloads translations via <see cref="LocalizationManager"/>.
    /// </summary>
    /// <param name="index">Index provided by the TMP_Dropdown selection.</param>
    public void ChangeLanguage(int index)
    {
        // Guard against invalid indices or an unassigned dropdown to prevent
        // out-of-range errors from corrupt UI events.
        if (languageDropdown == null || index < 0 || index >= languageDropdown.options.Count)
            return;

        string lang = languageDropdown.options[index].text;
        LocalizationManager.SetLanguage(lang);
        if (SaveGameManager.Instance != null)
        {
            SaveGameManager.Instance.Language = lang;
        }
    }

    // Updates the on-screen music volume percentage if a label is assigned.
    private void UpdateMusicVolumeLabel(float value)
    {
        if (musicVolumeLabel != null)
        {
            string fmt = LocalizationManager.Get("percentage_format");
            musicVolumeLabel.text = string.Format(fmt, Mathf.RoundToInt(value * 100f));
        }
    }

    // Updates the effects volume percentage label.
    private void UpdateEffectsVolumeLabel(float value)
    {
        if (effectsVolumeLabel != null)
        {
            string fmt = LocalizationManager.Get("percentage_format");
            effectsVolumeLabel.text = string.Format(fmt, Mathf.RoundToInt(value * 100f));
        }
    }

    /// <summary>
    /// Shows the tutorial sequence again as a help reference.
    /// </summary>
    public void ShowHelp()
    {
        if (tutorialManager != null)
        {
            tutorialManager.gameObject.SetActive(true);
            tutorialManager.BeginTutorial();
        }
    }
}
