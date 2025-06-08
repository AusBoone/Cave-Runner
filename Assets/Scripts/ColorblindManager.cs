using UnityEngine;

/// <summary>
/// Global colorblind mode settings persisted with PlayerPrefs.
/// </summary>
public static class ColorblindManager
{
    private const string Pref = "ColorblindMode";

    public static bool Enabled { get; private set; }

    public static event System.Action<bool> OnModeChanged;

    static ColorblindManager()
    {
        Enabled = PlayerPrefs.GetInt(Pref, 0) == 1;
    }

    /// <summary>
    /// Toggles colorblind mode and persists the preference.
    /// </summary>
    public static void SetEnabled(bool enabled)
    {
        Enabled = enabled;
        PlayerPrefs.SetInt(Pref, enabled ? 1 : 0);
        PlayerPrefs.Save();
        OnModeChanged?.Invoke(enabled);
    }
}
