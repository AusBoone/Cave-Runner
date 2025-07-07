using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Manages the currently selected save slot. Each slot stores game data in a
/// separate folder under <see cref="Application.persistentDataPath"/> so multiple
/// player profiles can exist on the same machine. The selected slot index is
/// persisted via <see cref="PlayerPrefs"/>.
/// </summary>
public static class SaveSlotManager
{
    /// <summary>Maximum number of available save slots.</summary>
    public const int MaxSlots = 3;

    /// <summary>Preference key storing the active slot index.</summary>
    private const string SlotPref = "CurrentSaveSlot";

    /// <summary>Index of the slot currently in use.</summary>
    public static int CurrentSlot { get; private set; }

    static SaveSlotManager()
    {
        CurrentSlot = Mathf.Clamp(PlayerPrefs.GetInt(SlotPref, 0), 0, MaxSlots - 1);
    }

    /// <summary>
    /// Changes the active slot and persists the choice. Throws when an invalid
    /// index is provided.
    /// </summary>
    /// <param name="slot">Value between 0 and <see cref="MaxSlots"/> - 1.</param>
    public static void SetSlot(int slot)
    {
        if (slot < 0 || slot >= MaxSlots)
        {
            throw new ArgumentOutOfRangeException(nameof(slot), "Invalid save slot index");
        }
        CurrentSlot = slot;
        PlayerPrefs.SetInt(SlotPref, slot);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Returns a path inside the current slot's directory for the given file
    /// name. The directory is created if it does not already exist.
    /// </summary>
    public static string GetPath(string fileName)
    {
        string dir = Path.Combine(Application.persistentDataPath, $"slot_{CurrentSlot}");
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        return Path.Combine(dir, fileName);
    }
}

