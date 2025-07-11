/*
 * ColorblindManager.cs
 * -----------------------------------------------------------------------------
 * Static helper tracking whether the user has enabled colorblind mode. The
 * value is stored in PlayerPrefs so it persists between sessions. Other
 * components subscribe to <see cref="OnModeChanged"/> to adjust their visuals
 * immediately when the preference toggles.
 * -----------------------------------------------------------------------------
 */
using UnityEngine;

/// <summary>
/// Global colorblind mode settings persisted with PlayerPrefs.
/// </summary>
public static class ColorblindManager
{
    /// <summary>Key used to store the preference in <see cref="PlayerPrefs"/>.</summary>
    private const string Pref = "ColorblindMode";

    /// <summary>
    /// Current colorblind mode state. Updated via <see cref="SetEnabled"/> and
    /// cached to avoid repeated preference lookups.
    /// </summary>
    public static bool Enabled { get; private set; }

    /// <summary>
    /// Fired whenever <see cref="Enabled"/> changes. The boolean parameter
    /// represents the new state.
    /// </summary>
    public static event System.Action<bool> OnModeChanged;

    /// <summary>
    /// Reads the persisted preference when the class is first accessed.
    /// </summary>
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
