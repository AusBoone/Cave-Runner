// StageManager.cs
// -----------------------------------------------------------------------------
// Handles stage progression events triggered by the GameManager. When a stage
// is unlocked it swaps the active background sprite, adjusts obstacle and
// hazard prefab lists and now applies stage-specific spawn probabilities and
// difficulty multipliers to spawners. This revision also supports stage
// modifiers such as custom gravity and speed multipliers for added variety.
// 2024 update: assets are now loaded through the Unity Addressables system
// asynchronously so stages can stream in without freezing the main thread.
// -----------------------------------------------------------------------------

using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

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
        [Tooltip("Address of the background sprite to load asynchronously.")]
        public AssetReferenceSprite backgroundSprite;

        [Tooltip("Ground obstacles available during this stage.")]
        public AssetReferenceGameObject[] groundObstacles;
        [Tooltip("Ceiling obstacles available during this stage.")]
        public AssetReferenceGameObject[] ceilingObstacles;
        [Tooltip("Moving platforms spawned in this stage.")]
        public AssetReferenceGameObject[] movingPlatforms;
        [Tooltip("Rotating hazards spawned in this stage.")]
        public AssetReferenceGameObject[] rotatingHazards;

        [Tooltip("Pit hazards for the HazardSpawner.")]
        public AssetReferenceGameObject[] pits;
        [Tooltip("Flying hazards such as bats for the HazardSpawner.")]
        public AssetReferenceGameObject[] bats;

        [Tooltip("Zig-zagging enemies available in this stage.")]
        public AssetReferenceGameObject[] zigZagEnemies;

        [Tooltip("Swooping enemies available in this stage.")]
        public AssetReferenceGameObject[] swoopingEnemies;

        [Tooltip("Shooter enemies available in this stage.")]
        public AssetReferenceGameObject[] shooterEnemies;

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
        if (loadRoutine != null)
        {
            StopCoroutine(loadRoutine);
        }
        loadRoutine = StartCoroutine(LoadStageRoutine(stageIndex));
    }

    // Keeps track of the running coroutine so tests can verify async behaviour
    private Coroutine loadRoutine;

    // Coroutine that performs asynchronous addressable loading and updates
    // spawners when complete.
    private IEnumerator LoadStageRoutine(int stageIndex)
    {
        if (stages == null || stageIndex < 0 || stageIndex >= stages.Length)
        {
            yield break; // invalid index or no data
        }

        StageDataSO asset = stages[stageIndex];
        if (asset == null)
        {
            yield break; // asset missing
        }
        StageData data = asset.stage;

        UIManager.Instance?.ShowLoadingIndicator();

        // Load the background sprite asynchronously
        Sprite bgSprite = null;
        if (parallaxBackground != null && data.backgroundSprite != null &&
            data.backgroundSprite.RuntimeKeyIsValid())
        {
            AsyncOperationHandle<Sprite> bgHandle = data.backgroundSprite.LoadAssetAsync<Sprite>();
            yield return bgHandle;
            if (bgHandle.Status == AsyncOperationStatus.Succeeded)
            {
                bgSprite = bgHandle.Result;
            }
            Addressables.Release(bgHandle);
        }

        if (parallaxBackground != null && bgSprite != null)
        {
            parallaxBackground.spriteName = bgSprite.name;
            var sr = parallaxBackground.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.sprite = bgSprite;
            }
        }

        // Load and assign obstacle prefabs
        if (obstacleSpawner != null)
        {
            yield return LoadPrefabs(data.groundObstacles, r => obstacleSpawner.groundObstacles = r);
            yield return LoadPrefabs(data.ceilingObstacles, r => obstacleSpawner.ceilingObstacles = r);
            yield return LoadPrefabs(data.movingPlatforms, r => obstacleSpawner.movingPlatforms = r);
            yield return LoadPrefabs(data.rotatingHazards, r => obstacleSpawner.rotatingHazards = r);
            obstacleSpawner.spawnMultiplier = data.obstacleSpawnMultiplier;
            obstacleSpawner.groundChance = data.groundObstacleChance;
            obstacleSpawner.ceilingChance = data.ceilingObstacleChance;
            obstacleSpawner.platformChance = data.movingPlatformChance;
            obstacleSpawner.rotatingChance = data.rotatingHazardChance;
        }

        // Load and assign hazard prefabs
        if (hazardSpawner != null)
        {
            yield return LoadPrefabs(data.pits, r => hazardSpawner.pitPrefabs = r);
            yield return LoadPrefabs(data.bats, r => hazardSpawner.batPrefabs = r);
            yield return LoadPrefabs(data.zigZagEnemies, r => hazardSpawner.zigZagPrefabs = r);
            yield return LoadPrefabs(data.swoopingEnemies, r => hazardSpawner.swoopPrefabs = r);
            yield return LoadPrefabs(data.shooterEnemies, r => hazardSpawner.shooterPrefabs = r);
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

        UIManager.Instance?.HideLoadingIndicator();
        loadRoutine = null;
    }

    // Utility coroutine to load a set of GameObject references via Addressables
    // and invoke a callback with the resulting array.
    private IEnumerator LoadPrefabs(AssetReferenceGameObject[] refs, System.Action<GameObject[]> setter)
    {
        if (refs == null || refs.Length == 0)
        {
            setter?.Invoke(new GameObject[0]);
            yield break;
        }

        var list = new System.Collections.Generic.List<GameObject>();
        foreach (var r in refs)
        {
            if (r == null || !r.RuntimeKeyIsValid())
                continue;
            AsyncOperationHandle<GameObject> handle = r.LoadAssetAsync();
            yield return handle;
            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                list.Add(handle.Result);
            }
            Addressables.Release(handle);
        }
        setter?.Invoke(list.ToArray());
    }
}
