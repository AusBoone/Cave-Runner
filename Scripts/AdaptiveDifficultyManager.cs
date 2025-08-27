using System; // Provides ArgumentOutOfRangeException for defensive validation
using UnityEngine;

/// <summary>
/// Adjusts obstacle and hazard spawn rates based on the player's recent
/// performance. The manager queries <see cref="AnalyticsManager"/> for the
/// average distance of the last few runs and scales spawner multipliers so
/// difficulty increases for skilled players and eases off when runs end early.
/// A 2024 revision also factors in the highest coin combo achieved in the last
/// session via <see cref="GameManager"/> allowing skilled play to nudge the
/// difficulty upward.
/// </summary>
public class AdaptiveDifficultyManager : MonoBehaviour
{
    public static AdaptiveDifficultyManager Instance { get; private set; }

    [Tooltip("Desired average distance a player should reach before difficulty ramps up.")]
    public float targetDistance = 500f;

    [Tooltip("Number of recent runs used when calculating average distance.")]
    public int sampleSize = 5;

    [Tooltip("Increment applied when increasing difficulty.")]
    public float increaseStep = 0.1f;

    [Tooltip("Increment applied when decreasing difficulty.")]
    public float decreaseStep = 0.1f;

    [Tooltip("Lower bound for spawn multipliers.")]
    public float minMultiplier = 0.5f;

    [Tooltip("Upper bound for spawn multipliers.")]
    public float maxMultiplier = 2f;

    private float currentMultiplier = 1f;

    [Tooltip("Bonus applied for each additional combo multiplier level. For example, a value of 0.05 increases a x3 combo by 10%.")]
    [SerializeField]
    private float comboBonusStep = 0.05f;

    private ObstacleSpawner obstacleSpawner;
    private HazardSpawner hazardSpawner;

    /// <summary>
    /// Unity lifecycle hook invoked when the component is first loaded.
    /// Performs defensive validation on serialized configuration fields to
    /// prevent ambiguous runtime behaviour. Any invalid value results in an
    /// <see cref="ArgumentOutOfRangeException"/> so misconfigurations are
    /// surfaced immediately during development or automated tests.
    /// </summary>
    void Awake()
    {
        // Validate that the designer-provided distance goal is positive.
        if (targetDistance <= 0f)
            throw new ArgumentOutOfRangeException(nameof(targetDistance),
                "Target distance must be greater than zero.");

        // Ensure at least one run is sampled when calculating averages.
        if (sampleSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(sampleSize),
                "Sample size must be greater than zero.");

        // Steps control how quickly difficulty adjusts and must be positive to
        // avoid inversions in scaling logic.
        if (increaseStep <= 0f)
            throw new ArgumentOutOfRangeException(nameof(increaseStep),
                "Increase step must be greater than zero.");
        if (decreaseStep <= 0f)
            throw new ArgumentOutOfRangeException(nameof(decreaseStep),
                "Decrease step must be greater than zero.");

        // Multiplier bounds represent allowable spawn scales and therefore must
        // also be positive and consistent with each other.
        if (minMultiplier <= 0f)
            throw new ArgumentOutOfRangeException(nameof(minMultiplier),
                "Minimum multiplier must be greater than zero.");
        if (maxMultiplier <= 0f)
            throw new ArgumentOutOfRangeException(nameof(maxMultiplier),
                "Maximum multiplier must be greater than zero.");
        if (minMultiplier > maxMultiplier)
            throw new ArgumentOutOfRangeException(nameof(minMultiplier),
                "Minimum multiplier cannot exceed maximum multiplier.");

        // Only one instance should persist for the lifetime of the application.
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            // Duplicate managers are not needed; discard extras.
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Registers the spawners whose spawn multipliers should be adjusted.
    /// Call this once after the spawners have been created.
    /// </summary>
    public void RegisterSpawners(ObstacleSpawner obstacle, HazardSpawner hazard)
    {
        obstacleSpawner = obstacle;
        hazardSpawner = hazard;
    }

    /// <summary>
    /// Computes a new difficulty multiplier from recent run data and applies
    /// it to the registered spawners. When no analytics data is available the
    /// multipliers remain unchanged.
    /// </summary>
    public void AdjustDifficulty()
    {
        if (AnalyticsManager.Instance == null)
            return;

        float avg = AnalyticsManager.Instance.GetAverageDistance(sampleSize);
        if (avg <= 0f)
            return;

        if (avg > targetDistance * 1.2f)
        {
            currentMultiplier = Mathf.Min(maxMultiplier, currentMultiplier + increaseStep);
        }
        else if (avg < targetDistance * 0.8f)
        {
            currentMultiplier = Mathf.Max(minMultiplier, currentMultiplier - decreaseStep);
        }

        // Factor in the player's coin combo performance from the last run. A
        // high combo implies the player consistently collects coins, so the
        // difficulty can scale up slightly faster. The bonus is clamped using
        // the configured min/max bounds to avoid runaway values.
        int combo = 1;
        if (GameManager.Instance != null)
        {
            combo = Mathf.Max(1, GameManager.Instance.GetCoinComboMultiplier());
        }
        // Apply the designer-configurable bonus for each extra combo level. A larger
        // step value means combos ramp difficulty more aggressively.
        float comboMult = 1f + (combo - 1) * comboBonusStep;
        currentMultiplier = Mathf.Clamp(currentMultiplier * comboMult, minMultiplier, maxMultiplier);

        if (obstacleSpawner != null)
            obstacleSpawner.spawnMultiplier = currentMultiplier;
        if (hazardSpawner != null)
            hazardSpawner.spawnMultiplier = currentMultiplier;
    }

    /// <summary>
    /// Returns the multiplier most recently applied by the manager.
    /// </summary>
    public float GetCurrentMultiplier()
    {
        return currentMultiplier;
    }
}
