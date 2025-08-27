using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Collections;
using System.Collections.Generic; // Supports List<T> for filtering prefabs
using TMPro; // TextMeshPro provides TMP_Text for UI labels

// For a high-level overview of how GameManager collaborates with other systems, see docs/ArchitectureOverview.md.

/// <summary>
/// Central controller for the endless runner. Tracks the player's
/// progress, handles pausing and game over logic and communicates with
/// the optional <see cref="SteamManager"/> for achievements and cloud
/// saves. This file also introduces a simple coin combo mechanic which
/// multiplies coin value when pickups occur rapidly. When the combo
/// increases, optional feedback such as camera shake, particles and a
/// pitched sound effect is played. Recent revisions add slow motion
/// support, stage-specific speed modifiers and the ability to start a run
/// with random power-ups. This revision also introduces a temporary coin
/// bonus effect that multiplies coin pickups when active. The effect now
/// stacks when additional power-ups are collected and exposes helper
/// methods so UI elements can display the remaining time. A new hardcore
/// mode option further increases game speed and spawn rates for an extra
/// challenge.
///
/// <remarks>
/// 2024 update: exposes <see cref="GetCoinComboMultiplier"/> so external
/// systems such as <see cref="AdaptiveDifficultyManager"/> can react to the
/// player's combo performance.
/// </remarks>
/// 
/// <remarks>
/// 2024 update: starting a run now triggers <see cref="AdaptiveDifficultyManager"/>
/// so obstacle and hazard spawners scale with player skill.
/// </remarks>
/// <remarks>
/// 2025 update: achievements are now unlocked for high coin combos,
/// boss defeats and clearing hardcore mode.
/// </remarks>
/// <remarks>
/// 2025 update: adds validation to <see cref="ActivateSpeedBoost"/> so callers
/// must provide positive durations and multipliers, preventing unintended
/// slowdowns or permanent boosts from invalid arguments.
/// </remarks>
/// <remarks>
/// 2028 fix: duplicates detected in <see cref="Awake"/> now return immediately
/// after calling <c>Destroy</c>. Skipping the remainder of initialization avoids
/// running setup on an object that is about to be destroyed, which previously
/// led to occasional null reference errors when supporting singletons were
/// missing.
/// </remarks>
/// <remarks>
/// 2030 update: registers for <c>Application.quitting</c> so
/// <see cref="InputManager.Shutdown"/> executes on exit. This explicit cleanup
/// releases native <c>InputAction</c> resources, preventing memory leaks in
/// player builds and during editor sessions.
/// </remarks>
/// <remarks>
/// 2036 update: introduces <see cref="maxComboMultiplier"/> to cap coin combo
/// rewards and align UI plus achievement logic with the new limit.
/// </remarks>
/// <remarks>
/// 2038 update: <see cref="StartGame"/> now caches the player GameObject and
/// verifies it exists before spawning starting power-ups. This validation avoids
/// costly repeated <c>FindGameObjectWithTag</c> calls and prevents spawns when the
/// player is missing, eliminating potential null reference errors.
/// </remarks>
/// <remarks>
/// 2039 update: removes runtime player searches in favor of a serialized
/// reference that must be assigned in the inspector. <see cref="StartGame"/>
/// now validates this reference and logs an error when it is absent to help
/// developers catch misconfigured scenes early.
/// </remarks>
/// <remarks>
/// 2026 update: redirects error logs through <see cref="LoggingHelper"/> so
/// build configurations can suppress verbose output while still surfacing
/// critical issues.
/// </remarks>
/// <remarks>
/// 2042 update: adds explicit dependency validation in <see cref="Awake"/> and
/// replaces direct property access in <see cref="GameOver"/> and
/// <see cref="StartGame"/> with null-safe handling to prevent runtime
/// exceptions when supporting managers or UI elements are missing.
/// </remarks>
/// <remarks>
/// 2043 update: UI text elements migrated from <c>UnityEngine.UI.Text</c> to
/// <see cref="TMP_Text"/> for higher quality rendering and to modernize the
/// UI stack.
/// </remarks>
/// <remarks>
/// 2045 update: exposes <see cref="PlayerTransform"/> so external systems can
/// access the player's <see cref="Transform"/> directly instead of performing
/// costly scene searches for a tagged object.
/// </remarks>
/// <remarks>
/// 2047 update: introduces <see cref="maxSpeed"/> to cap world speed and exposes
/// <see cref="MaxSpeed"/> for systems that scale difficulty based on the
/// game's top velocity.
/// </remarks>
/// <remarks>
/// 2050 update: pausing now sets <c>Time.timeScale</c> to <c>0</c> and resuming or
/// starting a run restores it to <c>1</c>. Centralizing time control here ensures
/// all gameplay and physics-based systems halt uniformly when the game is
/// paused and resume predictably.
/// </remarks>
/// <remarks>
/// 2051 fix: <see cref="StartGame"/> now filters <see cref="startingPowerUps"/>
/// for null entries before instantiation and logs a warning when invalid
/// prefabs are encountered. This prevents runtime exceptions from
/// misconfigured arrays and aids debugging during development.
/// </remarks>
/// <remarks>
/// 2056 fix: aborts <see cref="StartGame"/> when the serialized player
/// reference is missing. Exiting early prevents runs from starting and stops
/// power-up spawns in misconfigured scenes.
/// </remarks>
/// </summary>
public class GameManager : MonoBehaviour
{
    public float baseSpeed = 5f;              // initial world scroll speed
    public float speedIncrease = 0.1f;        // speed gain per second
    [SerializeField]
    private float maxSpeed = 20f;             // upper limit for world scroll speed
    public TMP_Text scoreLabel;               // UI label showing current distance
    public TMP_Text highScoreLabel;           // UI label showing best distance
    public TMP_Text coinLabel;                // UI label showing collected coins
    [Tooltip("Power-up prefabs that may spawn when a run begins if the player has the corresponding upgrade.")]
    public GameObject[] startingPowerUps;

    // Runtime values tracked during a single run
    private float distance;                   // total distance traveled
    private float currentSpeed;               // current scroll speed before multipliers
    private int coins;                        // coins collected this run
    private bool isRunning;                   // true while gameplay is active
    private bool isPaused;                    // true when pause menu is shown
    private bool isGameOver;                  // set after the player dies
    private UIManager uiManager;              // reference to the UI controller
    private float speedBoostTimer;            // remaining time on active speed boost
    private float speedMultiplier = 1f;       // multiplier applied by speed boosts
    private float gravityFlipTimer;           // remaining time gravity is inverted
    private bool gravityFlipped;              // true while gravity is inverted
    private float coinComboTimer;             // countdown for maintaining a coin combo
    private int coinComboMultiplier = 1;      // current coin multiplier from the combo

    [SerializeField]
    private GameObject playerObject;          // cached reference to the player GameObject; assign via inspector

    /// <summary>
    /// Provides read-only access to the player's <see cref="Transform"/>. The
    /// reference is null when the player object has not been assigned in the
    /// inspector, allowing callers to safely handle missing dependencies.
    /// </summary>
    public Transform PlayerTransform
    {
        get
        {
            // Return the transform of the serialized player object when
            // available. The null-conditional prevents dereferencing a missing
            // player and keeps callers robust in unconfigured scenes.
            return playerObject != null ? playerObject.transform : null;
        }
    }

    /// <summary>
    /// Read-only accessor exposing the configured maximum speed so external
    /// systems like <see cref="AdaptiveDifficultyManager"/> can scale their
    /// behavior based on the game's top scroll velocity.
    /// </summary>
    public float MaxSpeed => maxSpeed;

    // Coin bonus power-up variables
    private float coinBonusTimer;             // remaining time coins are multiplied
    private float coinBonusMultiplier = 1f;   // active multiplier applied to coin pickups

    private float slowMotionTimer;            // time remaining on slow motion
    private float slowMotionScale = 1f;       // scale value applied during slow motion
    private float stageSpeedMultiplier = 1f;  // modifier set by StageManager

    // -----------------------------------------------------------
    // Hardcore mode fields
    // -----------------------------------------------------------
    [Header("Hardcore Mode")]
    [Tooltip("Enable increased speed and spawn rates with fewer power-ups.")]
    [SerializeField]
    private bool hardcoreMode;                // stored flag indicating mode state

    [Tooltip("Multiplier applied to world speed when hardcore mode is active.")]
    public float hardcoreSpeedMultiplier = 1.2f;
    [Tooltip("Multiplier applied to hazard and obstacle spawn rates in hardcore mode.")]
    public float hardcoreSpawnMultiplier = 1.5f;
    [Tooltip("Multiplier applied to power-up spawn interval when hardcore mode is active.")]
    public float hardcorePowerUpRateMultiplier = 0.5f;

    [Tooltip("Time allowed between coin pickups to continue the combo.")]
    public float comboDuration = 1.5f;        // seconds before the combo resets

    [Tooltip("Maximum value the coin combo multiplier can reach before it stops increasing.")]
    [SerializeField]
    private int maxComboMultiplier = 10;      // cap applied to combo multiplier for balance

    public TMP_Text comboLabel;               // UI label showing current combo multiplier

    [Header("Combo Feedback")]
    [Tooltip("Particle effect played when the combo multiplier increases.")]
    public ParticleSystem comboParticles;
    [Tooltip("Audio clip played when the combo multiplier increases.")]
    public AudioClip comboSound;
    [Tooltip("Camera shake component used for combo effects.")]
    public CameraShake cameraShake;

    /// <summary>
    /// Distance milestones that unlock stages or achievements as the
    /// player progresses.
    /// </summary>
    public float[] stageGoals;

    private int currentStage;

    /// <summary>
    /// Event triggered whenever a new stage index is reached.
    /// </summary>
    public event System.Action<int> OnStageUnlocked;

    private const string AchDistance1000 = "ACH_DISTANCE_1000";
    private const string AchDistance5000 = "ACH_DISTANCE_5000";
    private const string AchCoins50 = "ACH_COINS_50";
    private const string AchCoins200 = "ACH_COINS_200";
    private const string AchCombo10 = "ACH_COMBO_10"; // unlocked when combo hits max
    private const string AchFirstBoss = "ACH_FIRST_BOSS";
    private const string AchHardcoreWin = "ACH_HARDCORE_WIN";

    public static GameManager Instance { get; private set; }

    public bool IsRunning()
    {
        return isRunning;
    }

    public bool IsPaused()
    {
        return isPaused;
    }

    public bool IsGameOver()
    {
        return isGameOver;
    }

    /// <summary>
    /// True when hardcore mode is enabled. Updating this property also persists
    /// the setting via <see cref="SaveGameManager"/> if available.
    /// </summary>
    public bool HardcoreMode
    {
        get => hardcoreMode;
        set
        {
            hardcoreMode = value;
            if (SaveGameManager.Instance != null)
            {
                SaveGameManager.Instance.HardcoreMode = value;
            }
        }
    }

    /// <summary>
    /// Initializes the singleton instance and loads the saved high
    /// score from either <see cref="SaveGameManager"/> or the Steam cloud.
    /// </summary>
    void Awake()
    {
        // Enforce the singleton pattern so only one manager controls the game
        // state. The first instance persists across scenes while subsequent
        // instances self-destruct.
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // keep this manager alive between scenes

            // Subscribe to the quitting event so we can dispose input actions
            // even if the GameManager is not explicitly destroyed before exit.
            // This release prevents native memory leaks from lingering
            // InputAction allocations in player builds and the editor.
            Application.quitting += OnApplicationQuitting;

            // Ensure required helper singletons exist before gameplay begins.
            if (AnalyticsManager.Instance == null)
            {
                new GameObject("AnalyticsManager").AddComponent<AnalyticsManager>();
            }
            if (SaveGameManager.Instance == null)
            {
                new GameObject("SaveGameManager").AddComponent<SaveGameManager>();
            }
        }
        else
        {
            // A GameManager already exists. Destroy this duplicate and exit
            // immediately so the remaining initialization does not run on a
            // soon-to-be destroyed object. Without this early return, later
            // code could access missing dependencies and trigger
            // NullReferenceException errors.
            Destroy(gameObject);
            return;
        }

        // Validate critical dependencies before proceeding. Missing references
        // indicate a misconfigured scene and could lead to null reference
        // exceptions later in initialization. Logging an explicit error helps
        // developers quickly diagnose the issue.
        bool missingDependencies = false;
        if (SaveGameManager.Instance == null)
        {
            LoggingHelper.LogError("Awake: SaveGameManager instance not found. Aborting initialization.");
            missingDependencies = true;
        }
        if (scoreLabel == null)
        {
            LoggingHelper.LogError("Awake: scoreLabel reference not set.");
            missingDependencies = true;
        }
        if (highScoreLabel == null)
        {
            LoggingHelper.LogError("Awake: highScoreLabel reference not set.");
            missingDependencies = true;
        }
        if (coinLabel == null)
        {
            LoggingHelper.LogError("Awake: coinLabel reference not set.");
            missingDependencies = true;
        }
        if (comboLabel == null)
        {
            LoggingHelper.LogError("Awake: comboLabel reference not set.");
            missingDependencies = true;
        }
        if (missingDependencies)
        {
            return; // Abort setup when essential components are missing
        }

        // Load the persisted hardcore mode preference after ensuring
        // SaveGameManager exists. Default to false when no save is present.
        hardcoreMode = SaveGameManager.Instance != null && SaveGameManager.Instance.HardcoreMode;
        currentSpeed = baseSpeed;
        coins = 0;
        gravityFlipped = false;
        gravityFlipTimer = 0f;
        currentStage = 0;
        coinComboTimer = 0f;
        coinComboMultiplier = 1;
        coinBonusTimer = 0f;
        coinBonusMultiplier = 1f;

        if (SteamManager.Instance != null)
        {
            int cloudScore = SteamManager.Instance.LoadHighScore();
            int localScore = SaveGameManager.Instance.HighScore;
            if (cloudScore > localScore)
            {
                SaveGameManager.Instance.HighScore = cloudScore;
            }
        }

        UpdateHighScoreLabel();
        UpdateCoinLabel();
        UpdateMultiplierLabel();

        // After the run has been reset, allow the adaptive difficulty system to
        // update spawn multipliers according to the player's recent performance.
        if (AdaptiveDifficultyManager.Instance != null)
        {
            AdaptiveDifficultyManager.Instance.AdjustDifficulty();
        }
    }

    /// <summary>
    /// Advances the game state every frame while running. Updates the
    /// player's distance and applies speed boosts over time.
    /// </summary>
    void Update()
    {
        if (!isRunning) return; // skip updates when the game is not active

        // Increase the base scroll speed over time so difficulty ramps up
        currentSpeed += speedIncrease * Time.deltaTime;
        // Prevent runaway acceleration by clamping to the configured maximum
        currentSpeed = Mathf.Min(currentSpeed, maxSpeed);

        // Count down slow motion timer using unscaled time so it expires
        // regardless of the current time scale.
        if (slowMotionTimer > 0f)
        {
            slowMotionTimer -= Time.unscaledDeltaTime;
            if (slowMotionTimer <= 0f)
            {
                Time.timeScale = 1f;
                slowMotionScale = 1f;
            }
        }

        // Reduce any active speed boost timer and reset the multiplier when finished
        if (speedBoostTimer > 0f)
        {
            speedBoostTimer -= Time.deltaTime;
            if (speedBoostTimer <= 0f)
            {
                speedMultiplier = 1f;
            }
        }

        // Count down the combo timer so the multiplier resets if time runs out
        if (coinComboTimer > 0f)
        {
            coinComboTimer -= Time.deltaTime;
            if (coinComboTimer <= 0f)
            {
                coinComboMultiplier = 1;
                UpdateMultiplierLabel();
            }
        }

        // Count down coin bonus timer and reset when expired
        if (coinBonusTimer > 0f)
        {
            coinBonusTimer -= Time.deltaTime;
            if (coinBonusTimer <= 0f)
            {
                coinBonusMultiplier = 1f;
            }
        }

        // Revert gravity when the flip duration expires
        if (gravityFlipped)
        {
            gravityFlipTimer -= Time.deltaTime;
            if (gravityFlipTimer <= 0f)
            {
                gravityFlipped = false;
                Physics2D.gravity = new Vector2(Physics2D.gravity.x, -Physics2D.gravity.y);
            }
        }

        // Update distance traveled applying all active multipliers. Hardcore
        // mode further increases speed using <see cref="hardcoreSpeedMultiplier"/>.
        float hcMult = hardcoreMode ? hardcoreSpeedMultiplier : 1f;
        distance += currentSpeed * speedMultiplier * stageSpeedMultiplier * hcMult * Time.deltaTime;

        // Notify listeners when distance milestones are crossed
        if (stageGoals != null && currentStage < stageGoals.Length && distance >= stageGoals[currentStage])
        {
            currentStage++;
            OnStageUnlocked?.Invoke(currentStage);
        }

        // Refresh the on-screen score text
        if (scoreLabel != null)
        {
            scoreLabel.text = Mathf.FloorToInt(distance).ToString();
        }
    }

    /// <summary>
    /// Current horizontal movement speed of the world. Returns zero if the
    /// game is not actively running.
    /// </summary>
    public float GetSpeed()
    {
        if (!isRunning)
        {
            return 0f;
        }
        float hcMult = hardcoreMode ? hardcoreSpeedMultiplier : 1f;
        return currentSpeed * speedMultiplier * stageSpeedMultiplier * hcMult;
    }

    /// <summary>
    /// Stops gameplay and displays the final results. High scores are
    /// saved locally and to Steam if available. Achievements are also
    /// checked here.
    /// </summary>
    public void GameOver()
    {
        // Stop all update logic and mark the run as finished
        isRunning = false;
        isPaused = false;
        isGameOver = true;
        Time.timeScale = 1f; // ensure normal time for menus

        // If gravity was flipped when the run ended, flip it back so menus and
        // future runs use the normal downward orientation.
        if (gravityFlipped)
        {
            Physics2D.gravity = new Vector2(Physics2D.gravity.x, -Physics2D.gravity.y);
            gravityFlipped = false;
            gravityFlipTimer = 0f;
        }
        coinBonusMultiplier = 1f;
        coinBonusTimer = 0f;

        int finalScore = Mathf.FloorToInt(distance);

        // Use null-conditional access so a missing save system does not crash
        // the game. Fallback to zero when no high score is available.
        var save = SaveGameManager.Instance;
        int highScore = save?.HighScore ?? 0;
        if (save == null)
        {
            LoggingHelper.LogError("GameOver: SaveGameManager missing; using default high score of 0.");
        }

        // Persist a new high score locally and to Steam if available
        if (finalScore > highScore)
        {
            highScore = finalScore;
            if (save != null)
            {
                // Persist the new high score when a save system exists.
                save.HighScore = highScore;
            }
            else
            {
                LoggingHelper.LogError("GameOver: SaveGameManager missing; high score not saved.");
            }
            if (SteamManager.Instance != null)
            {
                SteamManager.Instance.SaveHighScore(highScore);
            }
        }
        if (SteamManager.Instance != null)
        {
            // Grant distance and coin achievements if thresholds were reached
            if (finalScore >= 1000)
            {
                SteamManager.Instance.UnlockAchievement(AchDistance1000);
            }
            if (finalScore >= 5000)
            {
                SteamManager.Instance.UnlockAchievement(AchDistance5000);
            }
            if (coins >= 50)
            {
                SteamManager.Instance.UnlockAchievement(AchCoins50);
            }
            if (coins >= 200)
            {
                SteamManager.Instance.UnlockAchievement(AchCoins200);
            }

            // Completing a long run in hardcore mode grants an additional
            // achievement. The distance threshold mirrors the standard
            // marathon achievement so players must truly master the mode.
            if (hardcoreMode && finalScore >= 5000)
            {
                SteamManager.Instance.UnlockAchievement(AchHardcoreWin);
            }

            // Submit the score to the Steam leaderboard
            SteamManager.Instance.UploadScore(finalScore);
        }
        else if (LeaderboardClient.Instance != null)
        {
            // Non-Steam builds post to the HTTP leaderboard instead
            StartCoroutine(LeaderboardClient.Instance.UploadScore(finalScore));
        }

        // Persist run coins so they can be spent in the shop
        var shop = ShopManager.Instance;
        if (shop != null)
        {
            shop.AddCoins(coins);
        }
        else
        {
            LoggingHelper.LogError("GameOver: ShopManager missing; coins not persisted.");
        }
        // Update the UI with the final results
        if (uiManager != null)
        {
            uiManager.ShowGameOver(finalScore, highScore, coins);
        }
        // Record analytics data for this run
        if (AnalyticsManager.Instance != null)
        {
            AnalyticsManager.Instance.LogRun(distance, coins, true);
        }
        // Update the high score label only when the save system exists.
        if (SaveGameManager.Instance != null)
        {
            UpdateHighScoreLabel();
        }
        else
        {
            LoggingHelper.LogError("GameOver: SaveGameManager missing; high score label not updated.");
        }
    }

    /// <summary>
    /// Resets all runtime variables and begins a new run.
    /// Validates that the serialized player reference is assigned before
    /// attempting any player-dependent logic such as spawning power-ups.
    /// </summary>
    /// <remarks>
    /// 2038 update: verifies the player exists before spawning starting
    /// power-ups and caches the reference to avoid redundant searches.
    /// </remarks>
    /// <remarks>
    /// 2039 update: requires the player reference to be set in the inspector
    /// and emits an error if it is missing, removing runtime tag lookups.
    /// </remarks>
    public void StartGame()
    {
        // Validate the serialized player reference before any initialization.
        // Without a player object there is no run to manage, so we log an error
        // and exit to keep the game in its idle state. This early return also
        // prevents power-up spawns that depend on the player's position.
        if (playerObject == null)
        {
            LoggingHelper.LogError("StartGame: Player object reference not set. Aborting run.");
            return; // Skip initialization entirely when misconfigured.
        }

        // Always restore gravity before a new run in case the previous game
        // ended while flipped.
        if (gravityFlipped)
        {
            Physics2D.gravity = new Vector2(Physics2D.gravity.x, -Physics2D.gravity.y);
        }
        // Reset all state so the run begins fresh
        isRunning = true;
        isPaused = false;
        isGameOver = false;
        distance = 0f;

        // Query the shop for upgrades using null-conditional access. When the
        // shop is missing, default values are used and an error is logged so
        // developers understand why bonuses were skipped.
        var shop = ShopManager.Instance;
        float bonus = shop?.GetUpgradeEffect(UpgradeType.BaseSpeedBonus) ?? 0f;
        if (shop == null)
        {
            LoggingHelper.LogError("StartGame: ShopManager missing; using base speed without bonuses.");
        }
        currentSpeed = baseSpeed + bonus;
        coins = 0;
        speedMultiplier = 1f;
        speedBoostTimer = 0f;
        gravityFlipped = false;
        gravityFlipTimer = 0f;
        currentStage = 0;
        coinComboTimer = 0f;
        coinComboMultiplier = 1;
        coinBonusTimer = 0f;
        coinBonusMultiplier = 1f;

        stageSpeedMultiplier = 1f;
        slowMotionTimer = 0f;
        // Ensure global time runs at normal speed in case a previous session
        // paused the game or slow motion was active.
        Time.timeScale = 1f;

        // Validate required references and spawn starting power-ups if configured.
        int startingCount = Mathf.RoundToInt(shop?.GetUpgradeEffect(UpgradeType.StartingPowerUp) ?? 0f);
        if (shop == null)
        {
            LoggingHelper.LogError("StartGame: ShopManager missing; starting power-ups unavailable.");
        }

        if (startingCount > 0 && startingPowerUps != null && startingPowerUps.Length > 0)
        {
            // Filter out any null entries before selecting prefabs. Randomly
            // choosing a null would cause Instantiate to throw, so we build a
            // list of only valid prefabs first.
            var validPrefabs = new List<GameObject>();
            foreach (GameObject prefab in startingPowerUps)
            {
                if (prefab != null)
                {
                    validPrefabs.Add(prefab);
                }
            }

            // Warn developers when configuration errors are detected so they
            // can fix missing references in the inspector.
            if (validPrefabs.Count < startingPowerUps.Length)
            {
                LoggingHelper.LogWarning("StartGame: startingPowerUps contains null entries; skipping invalid prefabs.");
            }

            if (validPrefabs.Count > 0)
            {
                // Spawn the configured power-up prefabs adjacent to the player
                // using only the validated list to avoid null reference errors.
                Vector3 spawnPosition = playerObject.transform.position;
                for (int i = 0; i < startingCount; i++)
                {
                    GameObject prefab = validPrefabs[UnityEngine.Random.Range(0, validPrefabs.Count)];
                    Instantiate(prefab, spawnPosition + Vector3.right * (i + 1), Quaternion.identity);
                }
            }
            else
            {
                // If every entry was invalid, nothing can be spawned. Logging a
                // warning here provides clarity during debugging sessions.
                LoggingHelper.LogWarning("StartGame: no valid starting power-up prefabs available; none spawned.");
            }
        }

        UpdateCoinLabel();
        UpdateMultiplierLabel();
    }

    /// <summary>
    /// Halts updates without resetting gameplay data.
    /// </summary>
    public void PauseGame()
    {
        if (!isRunning || isPaused) return;

        // Flag gameplay as inactive and freeze time so all Update and physics
        // calculations stop. Using Time.timeScale ensures any running
        // coroutines or animations tied to scaled time also pause.
        isRunning = false;
        isPaused = true;
        Time.timeScale = 0f;
    }

    /// <summary>
    /// Continues gameplay after being paused.
    /// </summary>
    public void ResumeGame()
    {
        if (!isPaused) return;

        // Reactivate gameplay and restore normal time progression so movement
        // and physics resume. This mirrors the behavior in StartGame so all
        // entry points use consistent time scaling.
        isRunning = true;
        isPaused = false;
        Time.timeScale = 1f;
    }

    /// <summary>
    /// Total distance the player has traveled during this run.
    /// </summary>
    public float GetDistance()
    {
        return distance;
    }

    /// <summary>
    /// Temporarily multiplies the game speed for a set <paramref name="duration"/>.
    /// The <paramref name="duration"/> must be greater than <c>0</c> seconds and
    /// <paramref name="multiplier"/> must be greater than <c>0</c> to avoid
    /// unintended slowdowns or permanent boosts.
    /// </summary>
    /// <param name="duration">How long in seconds the boost should last. Must be &gt; 0.</param>
    /// <param name="multiplier">Factor applied to base speed. Must be &gt; 0.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="duration"/> is not positive.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="multiplier"/> is not positive.</exception>
    public void ActivateSpeedBoost(float duration, float multiplier)
    {
        // Validate input to prevent zero or negative values that could stall
        // or permanently freeze game speed.
        if (duration <= 0f)
        {
            // ArgumentException clarifies that duration must exceed zero seconds.
            throw new ArgumentException("duration must be positive", nameof(duration));
        }

        // Ensure multiplier remains positive so speed is always scaled by a
        // sensible factor; values less than or equal to zero would negate or
        // reverse movement.
        if (multiplier <= 0f)
        {
            // ArgumentOutOfRangeException communicates an invalid range.
            throw new ArgumentOutOfRangeException(nameof(multiplier), "multiplier must be positive");
        }

        // Only after validation do we apply the multiplier and timer to keep
        // internal state consistent.
        speedMultiplier = multiplier;
        speedBoostTimer = duration;
    }

    /// <summary>
    /// Temporarily increases the value of collected coins.
    /// </summary>
    /// <param name="duration">Seconds the bonus remains active.</param>
    /// <param name="multiplier">Multiplier applied to each coin pickup.</param>
    public void ActivateCoinBonus(float duration, float multiplier)
    {
        if (duration <= 0f)
            throw new ArgumentException("duration must be positive", nameof(duration));
        if (multiplier < 1f)
            throw new ArgumentOutOfRangeException(nameof(multiplier), "multiplier must be >= 1");

        // Extend any active bonus so multiple pickups stack
        coinBonusTimer += duration;
        // Use whichever multiplier is higher to encourage combining power-ups
        coinBonusMultiplier = Mathf.Max(coinBonusMultiplier, multiplier);
    }

    /// <summary>
    /// Remaining seconds on the current coin bonus effect. Returns zero when
    /// no bonus is active.
    /// </summary>
    public float GetCoinBonusTimeRemaining()
    {
        return Mathf.Max(0f, coinBonusTimer);
    }

    /// <summary>
    /// Current multiplier applied to coin pickups from the coin bonus.
    /// </summary>
    public float GetCoinBonusMultiplier()
    {
        return coinBonusMultiplier;
    }

    /// <summary>
    /// Inverts global gravity for a limited time.
    /// </summary>
    public void ActivateGravityFlip(float duration)
    {
        if (!gravityFlipped)
        {
            Physics2D.gravity = new Vector2(Physics2D.gravity.x, -Physics2D.gravity.y);
            gravityFlipped = true;
        }
        gravityFlipTimer = duration;
    }

    /// <summary>
    /// Slows down time for a limited duration. Scale must be between 0 and 1.
    /// </summary>
    public void ActivateSlowMotion(float duration, float scale)
    {
        if (duration <= 0f)
            throw new ArgumentException("duration must be positive", nameof(duration));
        if (scale <= 0f || scale > 1f)
            throw new ArgumentOutOfRangeException(nameof(scale), "scale must be between 0 and 1");

        Time.timeScale = scale;
        slowMotionScale = scale;
        slowMotionTimer = duration;
    }

    /// <summary>
    /// Sets a stage-specific speed multiplier applied on top of all other multipliers.
    /// </summary>
    public void SetStageSpeedMultiplier(float multiplier)
    {
        stageSpeedMultiplier = Mathf.Max(0.01f, multiplier);
    }

    /// <summary>
    /// Invoked whenever the coin combo multiplier increases. Plays
    /// optional feedback such as screen shake, particle effects and
    /// a sound that scales with the multiplier.
    /// </summary>
    protected virtual void OnComboIncreased()
    {
        // Trigger particle burst if assigned
        if (comboParticles != null)
        {
            comboParticles.Play();
        }

        // Shake the camera with intensity proportional to the multiplier
        if (cameraShake != null)
        {
            float magnitude = 0.1f * coinComboMultiplier;
            cameraShake.Shake(0.15f, magnitude);
        }

        // Play the combo sound with pitch based on the multiplier
        if (AudioManager.Instance != null && comboSound != null)
        {
            float pitch = 1f + 0.1f * (coinComboMultiplier - 1);
            AudioManager.Instance.PlaySound(comboSound, pitch);
        }

        // Unlock the combo achievement once the multiplier reaches the
        // defined threshold. The SteamManager ignores duplicate unlocks
        // so this check can run every increase without additional state.
        // Using the configurable max keeps the logic in sync with any
        // designer-defined cap.
        if (SteamManager.Instance != null && coinComboMultiplier >= maxComboMultiplier)
        {
            SteamManager.Instance.UnlockAchievement(AchCombo10);
        }

    }

    /// <summary>
    /// Registers the UIManager so game events can trigger menu updates.
    /// </summary>
    public void SetUIManager(UIManager manager)
    {
        uiManager = manager;
    }

    /// <summary>
    /// Called by boss entities when defeated so the appropriate Steam
    /// achievement can be granted.
    /// </summary>
    public void NotifyBossDefeated()
    {
        if (SteamManager.Instance != null)
        {
            SteamManager.Instance.UnlockAchievement(AchFirstBoss);
        }
    }

    /// <summary>
    /// Adds to the player's coin tally and updates the UI label. Coin value may
    /// increase through both the combo system and any purchased upgrades. The
    /// combo multiplier is capped at <see cref="maxComboMultiplier"/> to prevent
    /// unbounded rewards.
    /// </summary>
    public void AddCoins(int amount)
    {
        // Ensure callers provide a positive coin value
        if (amount <= 0)
        {
            throw new ArgumentException("Coin amount must be positive", nameof(amount));
        }

        // Determine whether this pickup continues an existing combo
        if (coinComboTimer > 0f)
        {
            int previous = coinComboMultiplier; // store prior value to detect actual changes

            // Increment then clamp the combo multiplier so feedback only triggers
            // when the multiplier truly increases and never exceeds the configured max.
            coinComboMultiplier = Mathf.Min(previous + 1, Mathf.Max(1, maxComboMultiplier));

            // Only run combo-increase feedback when the multiplier rises. This
            // prevents duplicate particles or sound when already at the cap.
            if (coinComboMultiplier > previous)
            {
                OnComboIncreased();
            }
        }
        else
        {
            coinComboMultiplier = 1; // reset combo when timer expires
        }

        coinComboTimer = comboDuration; // reset the combo window

        // Incorporate any purchased coin multiplier upgrade. Each upgrade level
        // adds a fixed bonus value to every coin pickup.
        float bonus = 0f;
        if (ShopManager.Instance != null)
        {
            bonus = ShopManager.Instance.GetUpgradeEffect(UpgradeType.CoinMultiplier);
        }

        // Final value after all bonuses are applied including temporary coin bonus.
        coins += Mathf.RoundToInt((amount + bonus) * coinComboMultiplier * coinBonusMultiplier);
        UpdateCoinLabel();
        UpdateMultiplierLabel();
    }

    /// <summary>
    /// Returns the player's collected coin count for this run.
    /// </summary>
    public int GetCoins()
    {
        return coins;
    }

    /// <summary>
    /// Returns the current unlocked stage index.
    /// </summary>
    public int GetCurrentStage()
    {
        return currentStage;
    }

    /// <summary>
    /// Current coin combo multiplier capped by <see cref="maxComboMultiplier"/>.
    /// Returns one when no combo is active so callers can easily scale
    /// rewards based on the combo state.
    /// </summary>
    public int GetCoinComboMultiplier()
    {
        // Clamp the reported value so external systems cannot exceed the
        // configured maximum even if the internal field is set directly via
        // reflection (as some tests do).
        return Mathf.Min(coinComboMultiplier, Mathf.Max(1, maxComboMultiplier));
    }

    /// <summary>
    /// Refreshes the on-screen high score text from <see cref="SaveGameManager"/>.
    /// </summary>
    private void UpdateHighScoreLabel()
    {
        if (highScoreLabel != null)
        {
            highScoreLabel.text = SaveGameManager.Instance.HighScore.ToString();
        }
    }

    /// <summary>
    /// Refreshes the on-screen coin total text.
    /// </summary>
    private void UpdateCoinLabel()
    {
        if (coinLabel != null)
        {
            coinLabel.text = coins.ToString();
        }
    }

    /// <summary>
    /// Refreshes the on-screen combo multiplier text. Because the combo
    /// value is clamped, the label never displays a multiplier above
    /// <see cref="maxComboMultiplier"/>.
    /// </summary>
    private void UpdateMultiplierLabel()
    {
        if (comboLabel != null)
        {
            comboLabel.text = "x" + coinComboMultiplier;
            // Inform the UI manager so it can animate the label
            uiManager?.AnimateComboLabel(comboLabel);
        }
    }

    /// <summary>
    /// Clears the static instance when the manager is destroyed and ensures the
    /// input system releases its unmanaged resources. This method runs both when
    /// exiting play mode and when duplicate managers self-destruct.
    /// </summary>
    void OnDestroy()
    {
        if (Instance == this)
        {
            // Unsubscribe from the application quitting event to avoid dangling
            // delegates if the manager is rebuilt. Calling Shutdown here frees
            // native InputAction buffers so they do not leak after the game
            // exits or the manager is torn down.
            Application.quitting -= OnApplicationQuitting;
            InputManager.Shutdown();
            Instance = null;
        }
    }

    /// <summary>
    /// Invoked when the application is closing. Ensures <see cref="InputManager"/>
    /// disposes its <see cref="UnityEngine.InputSystem.InputAction"/> instances,
    /// releasing native memory and preventing leaks on shutdown.
    /// </summary>
    private static void OnApplicationQuitting()
    {
        InputManager.Shutdown();
    }
}
