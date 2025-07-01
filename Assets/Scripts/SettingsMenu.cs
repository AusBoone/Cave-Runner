using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Provides UI hooks for changing key bindings and colorblind mode. When the
/// new Input System package is installed the menu can start interactive
/// rebinding operations so players can customise controls at runtime.
/// </summary>
public class SettingsMenu : MonoBehaviour
{
    public Text jumpKeyLabel;
    public Text slideKeyLabel;
    public Text pauseKeyLabel;
    public Toggle colorblindToggle;
    public Slider musicVolumeSlider;
    public Text musicVolumeLabel;
    public Slider effectsVolumeSlider;
    public Text effectsVolumeLabel;

    /// <summary>
    /// Populates UI elements with the current settings on start.
    /// </summary>
    void Start()
    {
        if (jumpKeyLabel != null) jumpKeyLabel.text = InputManager.JumpKey.ToString();
        if (slideKeyLabel != null) slideKeyLabel.text = InputManager.SlideKey.ToString();
        if (pauseKeyLabel != null) pauseKeyLabel.text = InputManager.PauseKey.ToString();
        if (colorblindToggle != null) colorblindToggle.isOn = ColorblindManager.Enabled;
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

    // Updates the on-screen music volume percentage if a label is assigned.
    private void UpdateMusicVolumeLabel(float value)
    {
        if (musicVolumeLabel != null)
        {
            musicVolumeLabel.text = Mathf.RoundToInt(value * 100f) + "%";
        }
    }

    // Updates the effects volume percentage label.
    private void UpdateEffectsVolumeLabel(float value)
    {
        if (effectsVolumeLabel != null)
        {
            effectsVolumeLabel.text = Mathf.RoundToInt(value * 100f) + "%";
        }
    }
}
