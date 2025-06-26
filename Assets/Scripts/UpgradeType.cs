using System;

/// <summary>
/// Enumerates all upgrade categories recognized by <see cref="ShopManager"/>.
/// Values are used as keys when persisting upgrade levels to PlayerPrefs.
/// </summary>
public enum UpgradeType
{
    /// <summary>Extends the duration of the magnet power-up.</summary>
    MagnetDuration = 0
}
