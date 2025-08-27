using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Central shop system that persists the player's coin total and purchased
/// upgrades between sessions using <see cref="SaveGameManager"/>. Each upgrade
/// increases a gameplay value such as power-up duration. Supported upgrades
/// include extending magnet, speed boost, and shield times, modifying the base
/// scroll speed, granting starting power-ups and adding a coin value bonus.
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
        // For duration upgrades this is the number of seconds added per level.
        // For coin upgrades this is the extra value granted per pickup.
        public float effect;
    }

    [Tooltip("Upgrades players can buy in the shop.")]
    public UpgradeData[] availableUpgrades;

    // Tracks how many times each upgrade has been purchased.
    private readonly Dictionary<UpgradeType, int> upgradeLevels = new Dictionary<UpgradeType, int>();

    // Persistence is now handled by SaveGameManager so the previous PlayerPrefs
    // keys are only kept for migration in SaveGameManager.

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
        LoggingHelper.LogWarning("Upgrade not configured: " + type); // Helper enforces global log gating.
        return new UpgradeData { type = type, cost = 0, effect = 0f };
    }

    // Restores coin and upgrade values from SaveGameManager.
    private void LoadState()
    {
        var save = SaveGameManager.Instance;
        if (save == null) return;

        Coins = save.Coins;
        foreach (var up in availableUpgrades)
        {
            int level = save.GetUpgradeLevel(up.type);
            upgradeLevels[up.type] = level;
        }
    }

    // Writes the current coin amount and upgrade levels to SaveGameManager.
    private void SaveState()
    {
        var save = SaveGameManager.Instance;
        if (save == null) return;

        save.Coins = Coins;

        // Batch update all upgrade levels to minimise file writes. The
        // individual setter triggers a disk write each call which scales
        // poorly with many upgrades.
        save.UpdateUpgradeLevels(new Dictionary<UpgradeType, int>(upgradeLevels));
    }
}
