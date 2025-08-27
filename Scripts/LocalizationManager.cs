// LocalizationManager.cs
// -----------------------------------------------------------------------------
// Simple runtime localization service for fetching translated strings. Language
// tables are loaded from JSON files stored under Resources/Localization. Each
// file contains an array of key/value pairs that map string identifiers to
// translations. The manager exposes methods to change the active language and
// retrieve strings by key. Missing keys return the key itself so callers can
// detect untranslated values.
// -----------------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Loads and provides access to translated strings at runtime. Translation files
/// live in <c>Resources/Localization</c> and are named by language code such as
/// "en" or "es". Each file must contain a JSON object with an <c>entries</c>
/// array. Example:
/// <code>
/// {
///   "entries": [ { "key": "greeting", "value": "Hello" } ]
/// }
/// </code>
/// </summary>
public static class LocalizationManager
{
    private const string ResourcesPath = "Localization"; // subfolder under Resources
    private static readonly Dictionary<string, string> table = new Dictionary<string, string>();
    private static string currentLanguage = "en";

    /// <summary>Raised when <see cref="SetLanguage"/> changes the active table.</summary>
    public static event System.Action OnLanguageChanged;

    /// <summary>Currently active language code.</summary>
    public static string CurrentLanguage => currentLanguage;

    /// <summary>
    /// List of available languages derived from JSON files placed under
    /// <c>Resources/Localization</c>.
    /// </summary>
    public static IEnumerable<string> AvailableLanguages
    {
        get
        {
            return Resources.LoadAll<TextAsset>(ResourcesPath)
                .Select(a => a.name)
                .ToArray();
        }
    }

    /// <summary>
    /// Changes the active language and reloads the corresponding table. If the
    /// file cannot be found the table becomes empty and missing keys will return
    /// their identifier.
    /// </summary>
    /// <param name="language">Language code such as "en" or "es".</param>
    public static void SetLanguage(string language)
    {
        currentLanguage = language;
        LoadTable(language);
        OnLanguageChanged?.Invoke();
    }

    /// <summary>
    /// Retrieves the translated string for the provided key. If no entry exists
    /// the key itself is returned.
    /// </summary>
    public static string Get(string key)
    {
        if (!table.ContainsKey(key))
        {
            return key;
        }
        return table[key];
    }

    // Loads the table for the specified language code from Resources.
    private static void LoadTable(string language)
    {
        table.Clear();
        TextAsset asset = Resources.Load<TextAsset>($"{ResourcesPath}/{language}");
        if (asset == null)
        {
            LoggingHelper.LogWarning($"Localization file not found for language '{language}'."); // Use helper so missing files respect verbose flag.
            return;
        }
        try
        {
            LocalizationFile wrapper = JsonUtility.FromJson<LocalizationFile>(asset.text);
            if (wrapper != null && wrapper.entries != null)
            {
                foreach (var entry in wrapper.entries)
                {
                    if (!string.IsNullOrEmpty(entry.key))
                    {
                        table[entry.key] = entry.value ?? string.Empty;
                    }
                }
            }
        }
        catch (System.Exception ex)
        {
            LoggingHelper.LogError("Failed to parse localization JSON: " + ex.Message); // Route errors through helper for consistency.
        }
    }

    // Helper types used to deserialize the JSON structure.
    [System.Serializable]
    private class LocalizationFile
    {
        public LocalizationEntry[] entries;
    }

    [System.Serializable]
    private class LocalizationEntry
    {
        public string key;
        public string value;
    }
}
