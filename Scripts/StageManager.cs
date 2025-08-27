// StageManager.cs
// -----------------------------------------------------------------------------
// For system architecture context see docs/ArchitectureOverview.md.
// Handles stage progression events triggered by the GameManager. When a stage
// is unlocked it swaps the active background sprite, adjusts obstacle and
// hazard prefab lists and applies stage-specific spawn probabilities and
// difficulty multipliers to spawners.
// -----------------------------------------------------------------------------

using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using System.Collections;
using System.Collections.Generic;

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

        [Header("Audio")]
        [Tooltip("Addressable keys for music clips played during this stage.")]
        public string[] stageMusic;

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

    // List of addressable handles currently loaded so they can be released
    // when the stage changes. Helps prevent memory leaks from dangling assets.
    private readonly System.Collections.Generic.List<AsyncOperationHandle> loadedHandles =
        new System.Collections.Generic.List<AsyncOperationHandle>();

    // Stores the gravity value present when this component awakens so it can be
    // restored if the object is destroyed. Stage data may scale gravity for
    // variety and forgetting to revert it would affect other scenes and the
    // editor.
    private Vector2 defaultGravity;

    // Releases all loaded addressable assets and clears the handle list. Called
    // before loading a new stage and when this component is destroyed.
    private void ReleaseLoadedAssets()
    {
        foreach (var handle in loadedHandles)
        {
            if (handle.IsValid())
            {
                Addressables.Release(handle);
            }
        }
        loadedHandles.Clear();
    }

    void Awake()
    {
        // Record the starting gravity so it can be restored when this manager
        // is destroyed. Stage modifiers may adjust gravity and leaving the
        // scaled value active would affect future scenes and the editor.
        defaultGravity = Physics2D.gravity;

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
        ReleaseLoadedAssets();

        // Restore the default gravity so other scenes are not affected by the
        // stage's custom value.
        Physics2D.gravity = defaultGravity;
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
            // Halt any in-progress stage load to prevent overlapping coroutines
            // from updating spawners with outdated data.
            StopCoroutine(loadRoutine);
            // Clear the reference immediately so tests and callers can observe
            // that no asynchronous load is active before starting a new one.
            loadRoutine = null;
        }
        // Release assets from the previous stage before loading new ones.
        ReleaseLoadedAssets();
        loadRoutine = StartCoroutine(LoadStageRoutine(stageIndex));
    }

    // Keeps track of the running coroutine so tests can verify async behaviour
    private Coroutine loadRoutine;

    // Coroutine that performs asynchronous addressable loading and updates
    // spawners when complete.
    private IEnumerator LoadStageRoutine(int stageIndex)
    {
        // Immediately exit and clear the coroutine reference when provided an
        // invalid index or the stages array is missing. This prevents callers
        // from retaining a stale coroutine handle after an early bailout.
        if (stages == null || stageIndex < 0 || stageIndex >= stages.Length)
        {
            loadRoutine = null;
            UIManager.Instance?.HideLoadingIndicator();
            yield break;
        }

        StageDataSO asset = stages[stageIndex];
        if (asset == null)
        {
            loadRoutine = null;
            UIManager.Instance?.HideLoadingIndicator();
            yield break; // asset missing
        }
        StageData data = asset.stage;

        UIManager.Instance?.ShowLoadingIndicator();
        UIManager.Instance?.SetLoadingProgress(0f);

        // Determine how many assets will be loaded so progress can report a
        // normalized value. Only valid references contribute to the total to
        // avoid division by zero and to reflect actual work being performed.
        int totalAssets = 0;
        if (parallaxBackground != null && data.backgroundSprite != null && data.backgroundSprite.RuntimeKeyIsValid())
        {
            totalAssets++;
        }

        System.Func<AssetReferenceGameObject[], int> CountValid = (AssetReferenceGameObject[] arr) =>
        {
            if (arr == null)
            {
                return 0;
            }
            int count = 0;
            foreach (var r in arr)
            {
                if (r != null && r.RuntimeKeyIsValid())
                {
                    count++;
                }
            }
            return count;
        };

        if (obstacleSpawner != null)
        {
            totalAssets += CountValid(data.groundObstacles);
            totalAssets += CountValid(data.ceilingObstacles);
            totalAssets += CountValid(data.movingPlatforms);
            totalAssets += CountValid(data.rotatingHazards);
        }

        if (hazardSpawner != null)
        {
            totalAssets += CountValid(data.pits);
            totalAssets += CountValid(data.bats);
            totalAssets += CountValid(data.zigZagEnemies);
            totalAssets += CountValid(data.swoopingEnemies);
            totalAssets += CountValid(data.shooterEnemies);
        }

        string musicKey = null;
        if (data.stageMusic != null && data.stageMusic.Length > 0)
        {
            int idx = Random.Range(0, data.stageMusic.Length);
            musicKey = data.stageMusic[idx];
            if (!string.IsNullOrEmpty(musicKey))
            {
                totalAssets++; // music clip will be loaded as well
            }
        }

        if (totalAssets == 0)
        {
            totalAssets = 1; // prevent division by zero if nothing is queued
        }

        float loadedAssets = 0f;
        System.Action ReportProgress = () =>
        {
            loadedAssets++;
            UIManager.Instance?.SetLoadingProgress(loadedAssets / totalAssets);
        };

        // Assemble all asynchronous loading operations so they can run in
        // parallel. The results are cached locally and applied after every
        // operation has completed to ensure spawners are updated atomically.
        var operations = new List<IEnumerator>();

        Sprite bgSprite = null;
        if (parallaxBackground != null && data.backgroundSprite != null && data.backgroundSprite.RuntimeKeyIsValid())
        {
            operations.Add(LoadSprite(data.backgroundSprite, s => bgSprite = s, ReportProgress));
        }

        if (obstacleSpawner != null)
        {
            operations.Add(LoadPrefabs(data.groundObstacles, r => obstacleSpawner.groundObstacles = r, ReportProgress));
            operations.Add(LoadPrefabs(data.ceilingObstacles, r => obstacleSpawner.ceilingObstacles = r, ReportProgress));
            operations.Add(LoadPrefabs(data.movingPlatforms, r => obstacleSpawner.movingPlatforms = r, ReportProgress));
            operations.Add(LoadPrefabs(data.rotatingHazards, r => obstacleSpawner.rotatingHazards = r, ReportProgress));
        }

        if (hazardSpawner != null)
        {
            operations.Add(LoadPrefabs(data.pits, r => hazardSpawner.pitPrefabs = r, ReportProgress));
            operations.Add(LoadPrefabs(data.bats, r => hazardSpawner.batPrefabs = r, ReportProgress));
            operations.Add(LoadPrefabs(data.zigZagEnemies, r => hazardSpawner.zigZagPrefabs = r, ReportProgress));
            operations.Add(LoadPrefabs(data.swoopingEnemies, r => hazardSpawner.swoopPrefabs = r, ReportProgress));
            operations.Add(LoadPrefabs(data.shooterEnemies, r => hazardSpawner.shooterPrefabs = r, ReportProgress));
        }

        AudioClip stageClip = null;
        if (!string.IsNullOrEmpty(musicKey))
        {
            operations.Add(LoadStageMusic(musicKey, clip => stageClip = clip, ReportProgress));
        }

        yield return CoroutineUtilities.WhenAll(this, operations);

        // Assign the background sprite after loading completes so rendering
        // changes happen in one frame without displaying partial results.
        if (parallaxBackground != null && bgSprite != null)
        {
            parallaxBackground.spriteName = bgSprite.name;
            var sr = parallaxBackground.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.sprite = bgSprite;
            }
        }

        if (obstacleSpawner != null)
        {
            obstacleSpawner.spawnMultiplier = data.obstacleSpawnMultiplier;
            obstacleSpawner.groundChance = data.groundObstacleChance;
            obstacleSpawner.ceilingChance = data.ceilingObstacleChance;
            obstacleSpawner.platformChance = data.movingPlatformChance;
            obstacleSpawner.rotatingChance = data.rotatingHazardChance;
        }

        if (hazardSpawner != null)
        {
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
        Physics2D.gravity = new Vector2(defaultGravity.x, defaultGravity.y * data.gravityScale);

        if (stageClip != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.CrossfadeTo(stageClip);
        }

        UIManager.Instance?.SetLoadingProgress(1f);
        UIManager.Instance?.HideLoadingIndicator();
        loadRoutine = null;
    }

    /// <summary>
    /// Loads a set of prefab references via Addressables and supplies the
    /// resulting array to <paramref name="setter"/>. Each completed load
    /// advances <paramref name="progressCallback"/> so callers can aggregate
    /// overall progress. Handles are tracked for later release when stages
    /// change.
    /// </summary>
    /// <param name="refs">Prefabs to load asynchronously.</param>
    /// <param name="setter">Callback receiving the resolved prefab array.</param>
    /// <param name="progressCallback">Invoked after each prefab finishes loading.</param>
    private IEnumerator LoadPrefabs(AssetReferenceGameObject[] refs, System.Action<GameObject[]> setter, System.Action progressCallback)
    {
        if (refs == null || refs.Length == 0)
        {
            setter?.Invoke(new GameObject[0]);
            yield break;
        }

        var list = new List<GameObject>();
        foreach (var r in refs)
        {
            if (r == null || !r.RuntimeKeyIsValid())
                continue;
            AsyncOperationHandle<GameObject> handle = r.LoadAssetAsync();
            loadedHandles.Add(handle);
            yield return handle;
            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                // Prefab loaded correctly; store the resulting object so the
                // spawner can instantiate it later.
                list.Add(handle.Result);
            }
            else
            {
                // Log the operation exception if provided, otherwise log the
                // status enum so failures are visible during development and
                // automated tests. This makes diagnosis of missing assets
                // considerably easier.
                LoggingHelper.LogError(handle.OperationException != null
                    ? handle.OperationException.ToString()
                    : handle.Status.ToString()); // Use helper so errors always surface while supporting log gating.
            }
            progressCallback?.Invoke();
        }
        setter?.Invoke(list.ToArray());
    }

    /// <summary>
    /// Asynchronously loads an audio clip for stage music using the Unity
    /// Addressables system. Handles are cached to <see cref="loadedHandles"/> so
    /// <see cref="ReleaseLoadedAssets"/> can free the clip when a new stage is
    /// applied, preventing memory leaks from accumulating as the player
    /// progresses. Callers provide a callback to receive the clip once loading
    /// completes; null is supplied if the load fails or the key is invalid.
    /// </summary>
    /// <param name="clipKey">Addressable key for the desired music clip.</param>
    /// <param name="onLoaded">Invoked with the loaded clip or null.</param>
    /// <param name="progressCallback">Called once the load operation completes.</param>
    /// <returns>Coroutine enumerator that yields until the clip finishes loading.</returns>
    protected virtual IEnumerator LoadStageMusic(string clipKey, System.Action<AudioClip> onLoaded, System.Action progressCallback)
    {
        // Abort early if no key is provided to avoid issuing unnecessary
        // Addressables requests during testing or in misconfigured stages.
        if (string.IsNullOrEmpty(clipKey))
        {
            onLoaded?.Invoke(null);
            progressCallback?.Invoke();
            yield break;
        }

        AsyncOperationHandle<AudioClip> handle = Addressables.LoadAssetAsync<AudioClip>(clipKey);
        loadedHandles.Add(handle);
        yield return handle; // wait for the clip to load without blocking the frame

        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            onLoaded?.Invoke(handle.Result);
        }
        else
        {
            // Surface the exception or status so developers notice missing or
            // misconfigured audio assets during testing and development.
            LoggingHelper.LogError(handle.OperationException != null
                ? handle.OperationException.ToString()
                : handle.Status.ToString()); // Route through helper for consistent error reporting.
            onLoaded?.Invoke(null);
        }
        progressCallback?.Invoke();
    }

    /// <summary>
    /// Loads a single sprite via Addressables, assigning the result to
    /// <paramref name="setter"/>. The <paramref name="progressCallback"/> is
    /// invoked regardless of success so aggregate progress calculations remain
    /// accurate even when an asset fails to load.
    /// </summary>
    /// <param name="reference">Addressable sprite reference to load.</param>
    /// <param name="setter">Callback receiving the sprite or null on failure.</param>
    /// <param name="progressCallback">Invoked after the load finishes.</param>
    private IEnumerator LoadSprite(AssetReferenceSprite reference, System.Action<Sprite> setter, System.Action progressCallback)
    {
        if (reference == null || !reference.RuntimeKeyIsValid())
        {
            setter?.Invoke(null);
            progressCallback?.Invoke();
            yield break;
        }

        AsyncOperationHandle<Sprite> handle = reference.LoadAssetAsync<Sprite>();
        loadedHandles.Add(handle);
        yield return handle;
        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            setter?.Invoke(handle.Result);
        }
        else
        {
            LoggingHelper.LogError(handle.OperationException != null
                ? handle.OperationException.ToString()
                : handle.Status.ToString()); // Ensure all errors flow through LoggingHelper.
            setter?.Invoke(null);
        }
        progressCallback?.Invoke();
    }
}
