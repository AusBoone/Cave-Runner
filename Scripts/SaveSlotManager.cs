using System;
using System.IO;
using UnityEngine;

// -----------------------------------------------------------------------------
// SaveSlotManager
// -----------------------------------------------------------------------------
// Static helper used by SaveGameManager to determine where save files are
// stored. Each slot corresponds to its own directory under the application's
// persistent data path. The active slot index is persisted using PlayerPrefs so
// profiles remain consistent across sessions.
// -----------------------------------------------------------------------------
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
    /// name. The directory is created if it does not already exist. Callers must
    /// provide only a simple file name; any path segments are rejected to avoid
    /// traversal attacks. If the slot directory cannot be created due to an
    /// <see cref="IOException"/> or <see cref="UnauthorizedAccessException"/>, the
    /// method logs the failure and falls back to <see
    /// cref="Application.persistentDataPath"/>.
    /// </summary>
    /// <param name="fileName">Name of the file to locate inside the current save slot.</param>
    /// <returns>Full path to the requested file within the slot or a fallback path on error.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="fileName"/> is null, empty, or contains path separators.</exception>
    public static string GetPath(string fileName)
    {
        // Validate input to ensure callers cannot escape the save directory or
        // create unexpected files. Path.GetFileName strips directories; if it
        // alters the provided string then a path segment was present.
        if (string.IsNullOrWhiteSpace(fileName) || Path.GetFileName(fileName) != fileName)
        {
            throw new ArgumentException("File name must be a non-empty simple name", nameof(fileName));
        }

        // Compute the directory for the active slot. Each slot isolates its
        // data under a unique folder so multiple profiles remain separated.
        string dir = Path.Combine(Application.persistentDataPath, $"slot_{CurrentSlot}");

        try
        {
            // Lazily create the directory when first accessed. Directory.Exists
            // returns false when a file occupies the path, which will trigger an
            // exception in CreateDirectory that we handle below.
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            // Join the sanitized file name to the slot directory path.
            return Path.Combine(dir, fileName);
        }
        catch (IOException ex)
        {
            // Log and fall back to the root persistent path so saving can still
            // proceed in a predictable location.
            LoggingHelper.LogError($"Failed to create save slot directory '{dir}': {ex.Message}");
            return Path.Combine(Application.persistentDataPath, fileName);
        }
        catch (UnauthorizedAccessException ex)
        {
            // Permissions issues are treated similarly to IO failures.
            LoggingHelper.LogError($"Failed to create save slot directory '{dir}': {ex.Message}");
            return Path.Combine(Application.persistentDataPath, fileName);
        }
    }
}

