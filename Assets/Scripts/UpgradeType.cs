using System;

/// <summary>
/// Enumerates all upgrade categories recognized by <see cref="ShopManager"/>.
/// Values are used as keys when persisting upgrade levels to PlayerPrefs.
/// The enum started with only <see cref="MagnetDuration"/> but has been
/// extended to cover additional power-ups and coin bonuses.
/// </summary>
public enum UpgradeType
{
    /// <summary>Extends the duration of the magnet power-up.</summary>
    MagnetDuration = 0,

    /// <summary>Additional seconds applied to <see cref="SpeedBoostPowerUp"/>.</summary>
    SpeedBoostDuration = 1,

    /// <summary>Additional seconds applied to <see cref="ShieldPowerUp"/>.</summary>
    ShieldDuration = 2,

    /// <summary>Extra coins awarded for each pickup.</summary>
    CoinMultiplier = 3
}
