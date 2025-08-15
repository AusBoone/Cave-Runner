// SaveGameManager.cs
// -----------------------------------------------------------------------------
// Provides persistent storage of player progress using a JSON file located
// under Application.persistentDataPath. The design intentionally avoids
// PlayerPrefs for large or structured data so saves can be inspected and
// manually edited if required. On first run existing PlayerPrefs values are
// migrated to maintain backward compatibility. Saving writes to a temporary
// file first to reduce the chance of corruption if the application quits
// during a write operation.
//
// 2025 bug fix: loading no longer throws when the "upgrades" array is missing
// from a legacy save file. The loader now checks for null before iterating so
// older saves continue to load correctly.
// 2026 update: SaveDataToFile now catches IO exceptions and cleans up temporary
// files to prevent crashes when the disk is unwritable.
// 2027 update: corrupt or unreadable saves trigger a reset to default values,
// ensuring players are not stuck with invalid data.
// -----------------------------------------------------------------------------

using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton responsible for loading and saving player progress such as coins,
/// purchased upgrades and the high score. Data is serialized to JSON so it can
/// be easily inspected and modified between versions.
/// </summary>
public class SaveGameManager : MonoBehaviour
{
    public static SaveGameManager Instance { get; private set; }

    /// <summary>Serializable key/value pair representing an upgrade level.</summary>
    [Serializable]
    private struct UpgradeEntry
    {
        public string type;    // name of the UpgradeType enum value
        public int level;      // purchased level count
    }

    /// <summary>Container for all persistent data saved to disk.</summary>
    [Serializable]
    private class SaveData
    {
        // Bump <see cref="CurrentVersion"/> when fields are added so older
        // saves can be upgraded in <see cref="LoadData"/>.
        public int version;
        public int coins;
        public int highScore;
        public float musicVolume = 1f;   // range 0-1
        public float effectsVolume = 1f; // range 0-1
        public string language = "en";   // language code
        public bool tutorialCompleted;   // has the intro tutorial been shown
        public bool jumpTipShown;        // has the jump tip been displayed
        public bool slideTipShown;       // has the slide tip been displayed
        public bool hardcoreMode;        // optional hardcore mode toggle
        public List<UpgradeEntry> upgrades = new List<UpgradeEntry>();
    }

    // Version value written to disk alongside <see cref="SaveData"/>.
    private const int CurrentVersion = 4;

    private const string CoinsKey = "ShopCoins";       // legacy PlayerPrefs key
    private const string UpgradePrefix = "UpgradeLevel_"; // legacy PlayerPrefs prefix
    private const string HighScoreKey = "HighScore";   // legacy PlayerPrefs key

    private SaveData data = new SaveData();
    private readonly Dictionary<UpgradeType, int> upgradeLevels = new Dictionary<UpgradeType, int>();
    private string savePath;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            // Use the SaveSlotManager so each profile writes to its own file
            savePath = SaveSlotManager.GetPath("savegame.json");
            LoadData();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>Current coin balance.</summary>
    public int Coins
    {
        get => data.coins;
        set
        {
            data.coins = Mathf.Max(0, value);
            SaveDataToFile();
        }
    }

    /// <summary>Stored best distance traveled.</summary>
    public int HighScore
    {
        get => data.highScore;
        set
        {
            data.highScore = Mathf.Max(0, value);
            SaveDataToFile();
        }
    }

    /// <summary>Stored music volume in the range 0–1.</summary>
    public float MusicVolume
    {
        get => data.musicVolume;
        set
        {
            data.musicVolume = Mathf.Clamp01(value);
            SaveDataToFile();
        }
    }

    /// <summary>Stored effects volume in the range 0–1.</summary>
    public float EffectsVolume
    {
        get => data.effectsVolume;
        set
        {
            data.effectsVolume = Mathf.Clamp01(value);
            SaveDataToFile();
        }
    }

    /// <summary>Currently selected language code.</summary>
    public string Language
    {
        get => data.language;
        set
        {
            if (!string.IsNullOrEmpty(value))
            {
                data.language = value;
                SaveDataToFile();
                LocalizationManager.SetLanguage(value);
            }
        }
    }

    /// <summary>True once the introductory tutorial has been completed.</summary>
    public bool TutorialCompleted
    {
        get => data.tutorialCompleted;
        set
        {
            data.tutorialCompleted = value;
            SaveDataToFile();
        }
    }

    /// <summary>Whether the jump hint has already been shown.</summary>
    public bool JumpTipShown
    {
        get => data.jumpTipShown;
        set
        {
            data.jumpTipShown = value;
            SaveDataToFile();
        }
    }

    /// <summary>Whether the slide hint has already been shown.</summary>
    public bool SlideTipShown
    {
        get => data.slideTipShown;
        set
        {
            data.slideTipShown = value;
            SaveDataToFile();
        }
    }

    /// <summary>Whether hardcore mode is enabled.</summary>
    public bool HardcoreMode
    {
        get => data.hardcoreMode;
        set
        {
            data.hardcoreMode = value;
            SaveDataToFile();
        }
    }

    /// <summary>Returns the purchased level count for an upgrade.</summary>
    public int GetUpgradeLevel(UpgradeType type)
    {
        upgradeLevels.TryGetValue(type, out int level);
        return level;
    }

    /// <summary>Sets the level count for an upgrade and persists it.</summary>
    public void SetUpgradeLevel(UpgradeType type, int level)
    {
        upgradeLevels[type] = Mathf.Max(0, level);
        SaveDataToFile();
    }

    /// <summary>
    /// Updates multiple upgrade levels in a single operation. The provided
    /// dictionary may contain any subset of <see cref="UpgradeType"/> values.
    /// A single file write occurs after all levels are updated.
    /// </summary>
    /// <param name="levels">Mapping of upgrade types to their desired levels.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="levels"/> is null.</exception>
    public void UpdateUpgradeLevels(Dictionary<UpgradeType, int> levels)
    {
        if (levels == null)
            throw new ArgumentNullException(nameof(levels));

        foreach (var kvp in levels)
        {
            upgradeLevels[kvp.Key] = Mathf.Max(0, kvp.Value);
        }

        SaveDataToFile();
    }

    /// <summary>
    /// Reads existing save data from disk or migrates old PlayerPrefs data when
    /// the save file does not yet exist.
    /// </summary>
    private void LoadData()
    {
        if (File.Exists(savePath))
        {
            try
            {
                string json = File.ReadAllText(savePath);
                SaveData loaded = JsonUtility.FromJson<SaveData>(json);
                if (loaded != null)
                {
                    // Handle missing fields by checking the save version.
                    if (loaded.version < 1)
                    {
                        loaded.musicVolume = 1f;
                        loaded.effectsVolume = 1f;
                    }
                    if (loaded.version < 2)
                    {
                        loaded.language = "en";
                    }
                    if (loaded.version < 3)
                    {
                        loaded.tutorialCompleted = false;
                        loaded.jumpTipShown = false;
                        loaded.slideTipShown = false;
                    }
                    if (loaded.version < 4)
                    {
                        loaded.hardcoreMode = false;
                    }

                    data.coins = loaded.coins;
                    data.highScore = loaded.highScore;
                    data.musicVolume = Mathf.Clamp01(loaded.musicVolume);
                    data.effectsVolume = Mathf.Clamp01(loaded.effectsVolume);
                    data.language = loaded.language ?? "en";
                    data.tutorialCompleted = loaded.tutorialCompleted;
                    data.jumpTipShown = loaded.jumpTipShown;
                    data.slideTipShown = loaded.slideTipShown;
                    data.hardcoreMode = loaded.hardcoreMode;
                    LocalizationManager.SetLanguage(data.language);
                    
                    upgradeLevels.Clear();
                    if (loaded.upgrades != null)
                    {
                        foreach (var entry in loaded.upgrades)
                        {
                            if (Enum.TryParse(entry.type, out UpgradeType type))
                            {
                                upgradeLevels[type] = entry.level;
                            }
                        }
                    }
                    return;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Failed to parse save file, starting fresh: " + ex.Message);
            }
            // Parsing failed or produced null; fall back to a clean slate.
            ResetToDefaultSave();
        }
        else
        {
            // No save file found; migrate values stored in PlayerPrefs if
            // present. This maintains compatibility with versions prior to the
            // JSON-based system.
            data.coins = PlayerPrefs.GetInt(CoinsKey, 0);
            data.highScore = PlayerPrefs.GetInt(HighScoreKey, 0);
            data.musicVolume = 1f;
            data.effectsVolume = 1f;
            data.language = "en";
            data.tutorialCompleted = PlayerPrefs.GetInt("TutorialSeen", 0) == 1;
            data.jumpTipShown = false;
            data.slideTipShown = false;
            data.hardcoreMode = false;
            foreach (UpgradeType type in Enum.GetValues(typeof(UpgradeType)))
            {
                int level = PlayerPrefs.GetInt(UpgradePrefix + type, 0);
                if (level > 0)
                {
                    upgradeLevels[type] = level;
                }
            }
            // Persist the newly created data so future loads skip migration.
            SaveDataToFile();
            LocalizationManager.SetLanguage(data.language);
        }
    }

    /// <summary>
    /// Resets all persisted values to their factory defaults, reapplies the
    /// default language and immediately writes the clean state to disk. Used when
    /// a save file exists but cannot be parsed.
    /// </summary>
    private void ResetToDefaultSave()
    {
        // Replace existing data with a fresh instance containing default field
        // values defined in <see cref="SaveData"/>.
        data = new SaveData();
        // Any previously cached upgrade levels are cleared so no stale values
        // remain after recovery from a corrupt file.
        upgradeLevels.Clear();
        // Apply the default language before persisting so both runtime state and
        // the save file reflect the reset value.
        LocalizationManager.SetLanguage(data.language);
        // Persist defaults so future loads start from a valid JSON file.
        SaveDataToFile();
    }

    /// <summary>
    /// Converts the current state into JSON and writes it to disk. A temporary
    /// file is used so the existing save is not corrupted if the application
    /// closes mid-write.
    /// </summary>
    /// <summary>
    /// Writes the current <see cref="SaveData"/> instance to disk. Marked as
    /// protected so tests can override the method and record how many times a
    /// save occurs without duplicating the serialization logic.
    /// </summary>
    protected virtual void SaveDataToFile()
    {
        data.version = CurrentVersion;
        data.musicVolume = Mathf.Clamp01(data.musicVolume);
        data.effectsVolume = Mathf.Clamp01(data.effectsVolume);
        if (string.IsNullOrEmpty(data.language))
        {
            data.language = "en";
        }
        data.upgrades.Clear();
        foreach (var kvp in upgradeLevels)
        {
            data.upgrades.Add(new UpgradeEntry { type = kvp.Key.ToString(), level = kvp.Value });
        }

        string json = JsonUtility.ToJson(data);

        // Write to a temp file first, then replace the original. This guards
        // against partial writes leaving a corrupt save file.
        string tempPath = savePath + ".tmp";
        try
        {
            // Writing to a temporary file first reduces the chance of leaving a
            // partially written save behind if the application closes or an
            // exception occurs mid-write.
            File.WriteAllText(tempPath, json);
            File.Copy(tempPath, savePath, true);
        }
        catch (IOException ex)
        {
            Debug.LogWarning($"Failed to write save file: {ex.Message}");
        }
        finally
        {
            // Ensure any temp file is removed regardless of success so future
            // attempts are not blocked.
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    /// <summary>
    /// Switches to a different save slot at runtime. The current data is
    /// written to disk before swapping directories and reloading from the new
    /// slot. Throws when an invalid index is provided.
    /// </summary>
    /// <param name="slot">Index of the slot to activate.</param>
    public void ChangeSlot(int slot)
    {
        if (slot < 0 || slot >= SaveSlotManager.MaxSlots)
            throw new ArgumentOutOfRangeException(nameof(slot), "Invalid save slot index");

        if (slot == SaveSlotManager.CurrentSlot)
            return; // already using this slot

        // Persist current state before redirecting file paths.
        SaveDataToFile();
        SaveSlotManager.SetSlot(slot);
        savePath = SaveSlotManager.GetPath("savegame.json");
        LoadData();
    }
}
