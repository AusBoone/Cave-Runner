using UnityEngine;

/// <summary>
/// Manages customizable key bindings saved in PlayerPrefs.
/// </summary>
public static class InputManager
{
    private const string JumpPref = "JumpKey";
    private const string SlidePref = "SlideKey";

    public static KeyCode JumpKey { get; private set; }
    public static KeyCode SlideKey { get; private set; }

    static InputManager()
    {
        // Load keys from prefs or use defaults
        JumpKey = LoadKey(JumpPref, KeyCode.Space);
        SlideKey = LoadKey(SlidePref, KeyCode.LeftControl);
    }

    private static KeyCode LoadKey(string pref, KeyCode defaultKey)
    {
        string saved = PlayerPrefs.GetString(pref, defaultKey.ToString());
        if (System.Enum.TryParse(saved, out KeyCode key))
        {
            return key;
        }
        return defaultKey;
    }

    public static void SetJumpKey(KeyCode key)
    {
        JumpKey = key;
        PlayerPrefs.SetString(JumpPref, key.ToString());
    }

    public static void SetSlideKey(KeyCode key)
    {
        SlideKey = key;
        PlayerPrefs.SetString(SlidePref, key.ToString());
    }
}
