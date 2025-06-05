using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Provides UI hooks for changing key bindings and colorblind mode.
/// </summary>
public class SettingsMenu : MonoBehaviour
{
    public Text jumpKeyLabel;
    public Text slideKeyLabel;
    public Toggle colorblindToggle;

    void Start()
    {
        if (jumpKeyLabel != null) jumpKeyLabel.text = InputManager.JumpKey.ToString();
        if (slideKeyLabel != null) slideKeyLabel.text = InputManager.SlideKey.ToString();
        if (colorblindToggle != null) colorblindToggle.isOn = ColorblindManager.Enabled;
    }

    public void SetJumpKey(string keyName)
    {
        if (System.Enum.TryParse(keyName, out KeyCode key))
        {
            InputManager.SetJumpKey(key);
            if (jumpKeyLabel != null) jumpKeyLabel.text = key.ToString();
        }
    }

    public void SetSlideKey(string keyName)
    {
        if (System.Enum.TryParse(keyName, out KeyCode key))
        {
            InputManager.SetSlideKey(key);
            if (slideKeyLabel != null) slideKeyLabel.text = key.ToString();
        }
    }

    public void ToggleColorblind(bool value)
    {
        ColorblindManager.SetEnabled(value);
    }
}
