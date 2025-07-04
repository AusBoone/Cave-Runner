// StageManager.cs
// -----------------------------------------------------------------------------
// Handles stage progression events triggered by the GameManager. When a stage
// is unlocked it swaps the active background sprite, adjusts obstacle and
// hazard prefab lists and now applies stage-specific spawn probabilities and
// difficulty multipliers to spawners. This revision also supports stage
// modifiers such as custom gravity and speed multipliers for added variety.
// -----------------------------------------------------------------------------

using UnityEngine;

/// <summary>
/// Coordinates stage-related visuals and hazards as the player travels further.
/// Attach this component to a persistent object and assign background,
/// obstacle and hazard references in the inspector.
/// </summary>
public class StageManager : MonoBehaviour
{
    [System.Serializable]
    public class StageData
    {
        [Tooltip("Sprite loaded from Resources/Art to use as the background.")]
        public string backgroundSprite;

        [Tooltip("Ground obstacles available during this stage.")]
        public GameObject[] groundObstacles;
        [Tooltip("Ceiling obstacles available during this stage.")]
        public GameObject[] ceilingObstacles;
        [Tooltip("Moving platforms spawned in this stage.")]
        public GameObject[] movingPlatforms;
        [Tooltip("Rotating hazards spawned in this stage.")]
        public GameObject[] rotatingHazards;

        [Tooltip("Pit hazards for the HazardSpawner.")]
        public GameObject[] pits;
        [Tooltip("Flying hazards such as bats for the HazardSpawner.")]
        public GameObject[] bats;

        [Tooltip("Zig-zagging enemies available in this stage.")]
        public GameObject[] zigZagEnemies;

        [Tooltip("Swooping enemies available in this stage.")]
        public GameObject[] swoopingEnemies;

        [Tooltip("Shooter enemies available in this stage.")]
        public GameObject[] shooterEnemies;

        [Header("Spawn Rate Multipliers")]
        [Tooltip("Multiplier applied to obstacle spawn rate during this stage.")]
        public float obstacleSpawnMultiplier = 1f;
        [Tooltip("Multiplier applied to hazard spawn rate during this stage.")]
        public float hazardSpawnMultiplier = 1f;

        [Header("Obstacle Spawn Probabilities")]
        [Tooltip("Relative chance to spawn ground obstacles.")]
        public float groundObstacleChance = 1f;
        [Tooltip("Relative chance to spawn ceiling obstacles.")]
        public float ceilingObstacleChance = 1f;
        [Tooltip("Relative chance to spawn moving platforms.")]
        public float movingPlatformChance = 1f;
        [Tooltip("Relative chance to spawn rotating hazards.")]
        public float rotatingHazardChance = 1f;

        [Header("Hazard Spawn Probabilities")]
        [Tooltip("Relative chance to spawn pits.")]
        public float pitChance = 1f;
        [Tooltip("Relative chance to spawn bats.")]
        public float batChance = 1f;
        [Tooltip("Relative chance to spawn zig-zag enemies.")]
        public float zigZagChance = 1f;
        [Tooltip("Relative chance to spawn swooping enemies.")]
        public float swoopChance = 1f;
        [Tooltip("Relative chance to spawn shooter enemies.")]
        public float shooterChance = 1f;

        [Header("Stage Modifiers")]
        [Tooltip("Multiplier applied to the base game speed during this stage.")]
        public float speedMultiplier = 1f;
        [Tooltip("Gravity scale applied while this stage is active.")]
        public float gravityScale = 1f;
    }

    [Tooltip("Background component whose spriteName will change per stage.")]
    public ParallaxBackground parallaxBackground;
    [Tooltip("Obstacle spawner to update when a new stage begins.")]
    public ObstacleSpawner obstacleSpawner;
    [Tooltip("Hazard spawner to update when a new stage begins.")]
    public HazardSpawner hazardSpawner;

    [Tooltip("Ordered list of data for each stage.")]
    public StageDataSO[] stages;

    void Awake()
    {
        // Subscribe to stage unlock notifications from the GameManager.
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnStageUnlocked += ApplyStage;
        }

        // Provide spawner references to the adaptive difficulty system so
        // it can scale spawn rates at runtime.
        if (AdaptiveDifficultyManager.Instance != null)
        {
            AdaptiveDifficultyManager.Instance.RegisterSpawners(obstacleSpawner, hazardSpawner);
        }
    }

    void Start()
    {
        // Ensure the initial stage settings are applied at startup.
        int initialStage = GameManager.Instance != null ? GameManager.Instance.GetCurrentStage() : 0;
        ApplyStage(initialStage);
    }

    void OnDestroy()
    {
        // Unsubscribe when destroyed to avoid dangling delegates.
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnStageUnlocked -= ApplyStage;
        }
    }

    /// <summary>
    /// Updates background and spawner prefabs for the specified stage index.
    /// Called when GameManager unlocks a new stage.
    /// </summary>
    /// <param name="stageIndex">Index of the stage to apply.</param>
    public void ApplyStage(int stageIndex)
    {
        if (stages == null || stageIndex < 0 || stageIndex >= stages.Length)
        {
            return; // invalid index or no data
        }

        StageDataSO asset = stages[stageIndex];
        if (asset == null)
        {
            return; // asset missing
        }
        StageData data = asset.stage;

        // Swap the scrolling background sprite if a name was provided.
        if (parallaxBackground != null && !string.IsNullOrEmpty(data.backgroundSprite))
        {
            parallaxBackground.spriteName = data.backgroundSprite;
            var sr = parallaxBackground.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                Sprite loaded = Resources.Load<Sprite>("Art/" + data.backgroundSprite);
                if (loaded != null)
                {
                    sr.sprite = loaded;
                }
            }
        }

        // Update obstacle prefabs and apply spawn settings for this stage.
        if (obstacleSpawner != null)
        {
            obstacleSpawner.groundObstacles = data.groundObstacles;
            obstacleSpawner.ceilingObstacles = data.ceilingObstacles;
            obstacleSpawner.movingPlatforms = data.movingPlatforms;
            obstacleSpawner.rotatingHazards = data.rotatingHazards;
            obstacleSpawner.spawnMultiplier = data.obstacleSpawnMultiplier;
            obstacleSpawner.groundChance = data.groundObstacleChance;
            obstacleSpawner.ceilingChance = data.ceilingObstacleChance;
            obstacleSpawner.platformChance = data.movingPlatformChance;
            obstacleSpawner.rotatingChance = data.rotatingHazardChance;
        }

        // Update hazard prefabs and spawn parameters for this stage.
        if (hazardSpawner != null)
        {
            hazardSpawner.pitPrefabs = data.pits;
            hazardSpawner.batPrefabs = data.bats;
            hazardSpawner.zigZagPrefabs = data.zigZagEnemies;
            hazardSpawner.swoopPrefabs = data.swoopingEnemies;
            hazardSpawner.shooterPrefabs = data.shooterEnemies;
            hazardSpawner.spawnMultiplier = data.hazardSpawnMultiplier;
            hazardSpawner.pitChance = data.pitChance;
            hazardSpawner.batChance = data.batChance;
            hazardSpawner.zigZagChance = data.zigZagChance;
            hazardSpawner.swoopChance = data.swoopChance;
            hazardSpawner.shooterChance = data.shooterChance;
        }

        // Apply stage-specific speed multiplier and gravity settings.
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetStageSpeedMultiplier(data.speedMultiplier);
        }
        Physics2D.gravity = new Vector2(0f, -9.81f * data.gravityScale);
    }
}
