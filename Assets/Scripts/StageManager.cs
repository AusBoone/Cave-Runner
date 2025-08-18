// StageManager.cs
// -----------------------------------------------------------------------------
// Handles stage progression events triggered by the GameManager. When a stage
// is unlocked it swaps the active background sprite, adjusts obstacle and
// hazard prefab lists and now applies stage-specific spawn probabilities and
// difficulty multipliers to spawners. This revision also supports stage
// modifiers such as custom gravity and speed multipliers for added variety.
// 2024 update: assets are now loaded through the Unity Addressables system
// asynchronously so stages can stream in without freezing the main thread.
// Music tracks are also swapped using AudioManager's cross-fading functionality
// when a new stage begins.
// 2026 fix: LoadStageRoutine now clears its coroutine reference when exiting
// early so callers don't hold on to a stale handle.
// 2027 fix: Stage gravity scaling now preserves the existing horizontal
// component so levels can apply sideways forces without them being reset.
// 2028 diagnostic: addressable load failures now log detailed error information
// to aid in debugging missing or misconfigured assets.
// 2029 update: stage music now streams via Addressables asynchronously.
// -----------------------------------------------------------------------------

using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using System.Collections;

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
            StopCoroutine(loadRoutine);
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

        // Load the background sprite asynchronously
        Sprite bgSprite = null;
        if (parallaxBackground != null && data.backgroundSprite != null &&
            data.backgroundSprite.RuntimeKeyIsValid())
        {
            AsyncOperationHandle<Sprite> bgHandle = data.backgroundSprite.LoadAssetAsync<Sprite>();
            loadedHandles.Add(bgHandle);
            yield return bgHandle;
            if (bgHandle.Status == AsyncOperationStatus.Succeeded)
            {
                // Background asset loaded successfully; cache it for assignment
                // after all asynchronous work completes.
                bgSprite = bgHandle.Result;
            }
            else
            {
                // Log detailed information about the failure so designers can
                // resolve misconfigured or missing addressable references. The
                // OperationException provides stack details when available while
                // Status offers a fallback enum value if no exception is set.
                Debug.LogError(bgHandle.OperationException != null
                    ? bgHandle.OperationException
                    : (object)bgHandle.Status);
            }
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
        // Preserve the existing horizontal gravity component while scaling only
        // the vertical axis according to the stage's modifier. Some projects
        // may use a non-zero X gravity for unique mechanics (e.g., sideways
        // wind) and wiping it out here would cause subtle physics bugs.
        Physics2D.gravity = new Vector2(defaultGravity.x, defaultGravity.y * data.gravityScale);

        // Choose a random music track for this stage and start a cross-fade.
        // The clip is loaded asynchronously so large files do not stall the
        // main thread. The coroutine yields until the Addressables request
        // completes, then invokes the callback with the resulting clip. The
        // asset handle is cached so ReleaseLoadedAssets can free memory when
        // switching stages.
        if (data.stageMusic != null && data.stageMusic.Length > 0)
        {
            int idx = Random.Range(0, data.stageMusic.Length);
            string clipKey = data.stageMusic[idx];
            yield return LoadStageMusic(clipKey, clip =>
            {
                // Only cross-fade once a valid clip has loaded and the global
                // AudioManager exists. The callback isolates audio-specific
                // behaviour from the loading logic so tests can provide a
                // minimal environment without audio sources.
                if (clip != null && AudioManager.Instance != null)
                {
                    AudioManager.Instance.CrossfadeTo(clip);
                }
            });
        }

        UIManager.Instance?.HideLoadingIndicator();
        loadRoutine = null;
    }

    // Utility coroutine to load a set of GameObject references via Addressables
    // and invoke a callback with the resulting array. Handles are tracked so
    // assets can be released when the stage changes.
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
                Debug.LogError(handle.OperationException != null
                    ? handle.OperationException
                    : (object)handle.Status);
            }
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
    /// <returns>Coroutine enumerator that yields until the clip finishes loading.</returns>
    protected virtual IEnumerator LoadStageMusic(string clipKey, System.Action<AudioClip> onLoaded)
    {
        // Abort early if no key is provided to avoid issuing unnecessary
        // Addressables requests during testing or in misconfigured stages.
        if (string.IsNullOrEmpty(clipKey))
        {
            onLoaded?.Invoke(null);
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
            Debug.LogError(handle.OperationException != null
                ? handle.OperationException
                : (object)handle.Status);
            onLoaded?.Invoke(null);
        }
    }
}
