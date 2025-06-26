// StageManager.cs
// -----------------------------------------------------------------------------
// Handles stage progression events triggered by the GameManager. When a stage
// is unlocked it swaps the active background sprite and adjusts obstacle and
// hazard prefab lists to increase difficulty.
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
    }

    [Tooltip("Background component whose spriteName will change per stage.")]
    public ParallaxBackground parallaxBackground;
    [Tooltip("Obstacle spawner to update when a new stage begins.")]
    public ObstacleSpawner obstacleSpawner;
    [Tooltip("Hazard spawner to update when a new stage begins.")]
    public HazardSpawner hazardSpawner;

    [Tooltip("Ordered list of data for each stage.")]
    public StageData[] stages;

    void Awake()
    {
        // Subscribe to stage unlock notifications from the GameManager.
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnStageUnlocked += ApplyStage;
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

        StageData data = stages[stageIndex];

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

        // Update obstacle prefabs so newly spawned objects reflect the stage.
        if (obstacleSpawner != null)
        {
            obstacleSpawner.groundObstacles = data.groundObstacles;
            obstacleSpawner.ceilingObstacles = data.ceilingObstacles;
            obstacleSpawner.movingPlatforms = data.movingPlatforms;
            obstacleSpawner.rotatingHazards = data.rotatingHazards;
        }

        // Update hazard prefabs for pits and flying enemies.
        if (hazardSpawner != null)
        {
            hazardSpawner.pitPrefabs = data.pits;
            hazardSpawner.batPrefabs = data.bats;
        }
    }
}
