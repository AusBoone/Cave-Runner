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
    CoinMultiplier = 3,

    /// <summary>Additional base scroll speed applied at the start of each run.</summary>
    BaseSpeedBonus = 4,

    /// <summary>Number of random power-ups granted when starting a new run.</summary>
    StartingPowerUp = 5,

    /// <summary>Extends the duration of the coin bonus power-up.</summary>
    CoinBonusDuration = 6,

    /// <summary>Additional seconds the <see cref="DoubleJumpPowerUp"/> remains active.</summary>
    DoubleJumpDuration = 7,

    /// <summary>Additional seconds applied to <see cref="InvincibilityPowerUp"/>.</summary>
    InvincibilityDuration = 8
}
