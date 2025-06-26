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

    /// <summary>
    /// Populates UI elements with the current settings on start.
    /// </summary>
    void Start()
    {
        if (jumpKeyLabel != null) jumpKeyLabel.text = InputManager.JumpKey.ToString();
        if (slideKeyLabel != null) slideKeyLabel.text = InputManager.SlideKey.ToString();
        if (pauseKeyLabel != null) pauseKeyLabel.text = InputManager.PauseKey.ToString();
        if (colorblindToggle != null) colorblindToggle.isOn = ColorblindManager.Enabled;
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
}
