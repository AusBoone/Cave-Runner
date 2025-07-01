// SaveGameManager.cs
// -----------------------------------------------------------------------------
// Provides persistent storage of player progress using a JSON file located
// under Application.persistentDataPath. The design intentionally avoids
// PlayerPrefs for large or structured data so saves can be inspected and
// manually edited if required. On first run existing PlayerPrefs values are
// migrated to maintain backward compatibility. Saving writes to a temporary
// file first to reduce the chance of corruption if the application quits
// during a write operation.
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
        public int coins;
        public int highScore;
        public List<UpgradeEntry> upgrades = new List<UpgradeEntry>();
    }

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
            savePath = Path.Combine(Application.persistentDataPath, "savegame.json");
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
                    data.coins = loaded.coins;
                    data.highScore = loaded.highScore;
                    upgradeLevels.Clear();
                    foreach (var entry in loaded.upgrades)
                    {
                        if (Enum.TryParse(entry.type, out UpgradeType type))
                        {
                            upgradeLevels[type] = entry.level;
                        }
                    }
                    return;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Failed to parse save file, starting fresh: " + ex.Message);
            }
        }
        else
        {
            // No save file found; migrate values stored in PlayerPrefs if
            // present. This maintains compatibility with versions prior to the
            // JSON-based system.
            data.coins = PlayerPrefs.GetInt(CoinsKey, 0);
            data.highScore = PlayerPrefs.GetInt(HighScoreKey, 0);
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
        }
    }

    /// <summary>
    /// Converts the current state into JSON and writes it to disk. A temporary
    /// file is used so the existing save is not corrupted if the application
    /// closes mid-write.
    /// </summary>
    private void SaveDataToFile()
    {
        data.upgrades.Clear();
        foreach (var kvp in upgradeLevels)
        {
            data.upgrades.Add(new UpgradeEntry { type = kvp.Key.ToString(), level = kvp.Value });
        }

        string json = JsonUtility.ToJson(data);

        // Write to a temp file first, then replace the original. This guards
        // against partial writes leaving a corrupt save file.
        string tempPath = savePath + ".tmp";
        File.WriteAllText(tempPath, json);
        File.Copy(tempPath, savePath, true);
        File.Delete(tempPath);
    }
}
