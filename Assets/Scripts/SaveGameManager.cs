// SaveGameManager.cs
// -----------------------------------------------------------------------------
// Provides persistent storage of player progress in a JSON file located under
// Application.persistentDataPath. This replaces the previous PlayerPrefs based
// system used by GameManager and ShopManager. On first run the manager migrates
// any existing PlayerPrefs values into the new save file.
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
            }
        }
        else
        {
            // No save file yet; migrate values stored in PlayerPrefs if present.
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
            SaveDataToFile();
        }
    }

    /// <summary>
    /// Writes the current data object to disk. The upgrade dictionary is first
    /// converted to a list of serializable entries.
    /// </summary>
    private void SaveDataToFile()
    {
        data.upgrades.Clear();
        foreach (var kvp in upgradeLevels)
        {
            data.upgrades.Add(new UpgradeEntry { type = kvp.Key.ToString(), level = kvp.Value });
        }
        string json = JsonUtility.ToJson(data);
        File.WriteAllText(savePath, json);
    }
}
