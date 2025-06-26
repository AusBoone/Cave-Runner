using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Central shop system that persists the player's coin total and purchased
/// upgrades between sessions using PlayerPrefs. Each upgrade increases a
/// gameplay value such as power-up duration.
/// </summary>
public class ShopManager : MonoBehaviour
{
    public static ShopManager Instance { get; private set; }

    /// <summary>
    /// Defines a purchasable upgrade entry exposed in the inspector.
    /// </summary>
    [Serializable]
    public struct UpgradeData
    {
        public UpgradeType type;  // unique identifier for the upgrade
        public int cost;          // cost in coins per purchase
        public float effect;      // effect added per upgrade level
    }

    [Tooltip("Upgrades players can buy in the shop.")]
    public UpgradeData[] availableUpgrades;

    // Tracks how many times each upgrade has been purchased.
    private readonly Dictionary<UpgradeType, int> upgradeLevels = new Dictionary<UpgradeType, int>();

    private const string CoinsKey = "ShopCoins";
    private const string UpgradePrefix = "UpgradeLevel_";

    /// <summary>Current coin balance saved across sessions.</summary>
    public int Coins { get; private set; }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadState();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Adds coins to the persistent balance and saves immediately.
    /// </summary>
    public void AddCoins(int amount)
    {
        if (amount < 0)
        {
            throw new ArgumentException("amount must be non-negative", nameof(amount));
        }
        Coins += amount;
        SaveState();
    }

    /// <summary>
    /// Attempts to purchase an upgrade. Returns true when successful.
    /// </summary>
    public bool PurchaseUpgrade(UpgradeType type)
    {
        UpgradeData data = GetData(type);
        if (Coins < data.cost)
        {
            return false; // not enough coins
        }

        Coins -= data.cost;
        if (upgradeLevels.ContainsKey(type))
        {
            upgradeLevels[type]++;
        }
        else
        {
            upgradeLevels[type] = 1;
        }
        SaveState();
        return true;
    }

    /// <summary>
    /// Returns the cumulative effect value for the provided upgrade type.
    /// </summary>
    public float GetUpgradeEffect(UpgradeType type)
    {
        UpgradeData data = GetData(type);
        if (upgradeLevels.TryGetValue(type, out int level))
        {
            return data.effect * level;
        }
        return 0f;
    }

    // Fetches upgrade data from the configured list. If not found, returns a
    // default struct so callers do not crash.
    private UpgradeData GetData(UpgradeType type)
    {
        foreach (var up in availableUpgrades)
        {
            if (up.type == type)
            {
                return up;
            }
        }
        Debug.LogWarning("Upgrade not configured: " + type);
        return new UpgradeData { type = type, cost = 0, effect = 0f };
    }

    // Restores coin and upgrade values from PlayerPrefs.
    private void LoadState()
    {
        Coins = PlayerPrefs.GetInt(CoinsKey, 0);
        foreach (var up in availableUpgrades)
        {
            int level = PlayerPrefs.GetInt(UpgradePrefix + up.type, 0);
            upgradeLevels[up.type] = level;
        }
    }

    // Writes the current coin amount and upgrade levels to PlayerPrefs.
    private void SaveState()
    {
        PlayerPrefs.SetInt(CoinsKey, Coins);
        foreach (var kvp in upgradeLevels)
        {
            PlayerPrefs.SetInt(UpgradePrefix + kvp.Key, kvp.Value);
        }
        PlayerPrefs.Save();
    }
}
