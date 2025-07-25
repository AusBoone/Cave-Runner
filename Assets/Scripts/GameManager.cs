using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System;
using System.Collections;

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
/// </summary>
public class GameManager : MonoBehaviour
{
    public float baseSpeed = 5f;              // initial world scroll speed
    public float speedIncrease = 0.1f;        // speed gain per second
    public Text scoreLabel;                   // UI label showing current distance
    public Text highScoreLabel;               // UI label showing best distance
    public Text coinLabel;                    // UI label showing collected coins
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
    public Text comboLabel;                   // UI label showing current combo multiplier

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
    private const string AchCombo10 = "ACH_COMBO_10";
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
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
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
            Destroy(gameObject);
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
        int highScore = SaveGameManager.Instance.HighScore;

        // Persist a new high score locally and to Steam if available
        if (finalScore > highScore)
        {
            highScore = finalScore;
            SaveGameManager.Instance.HighScore = highScore;
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
        if (ShopManager.Instance != null)
        {
            ShopManager.Instance.AddCoins(coins);
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
        UpdateHighScoreLabel();
    }

    /// <summary>
    /// Resets all runtime variables and begins a new run.
    /// </summary>
    public void StartGame()
    {
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
        float bonus = 0f;
        if (ShopManager.Instance != null)
        {
            bonus = ShopManager.Instance.GetUpgradeEffect(UpgradeType.BaseSpeedBonus);
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
        Time.timeScale = 1f;

        // Spawn starting power-ups if the player purchased the upgrade.
        int startingCount = 0;
        if (ShopManager.Instance != null)
        {
            startingCount = Mathf.RoundToInt(ShopManager.Instance.GetUpgradeEffect(UpgradeType.StartingPowerUp));
        }
        if (startingCount > 0 && startingPowerUps != null && startingPowerUps.Length > 0)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            Vector3 pos = player != null ? player.transform.position : Vector3.zero;
            for (int i = 0; i < startingCount; i++)
            {
                GameObject prefab = startingPowerUps[UnityEngine.Random.Range(0, startingPowerUps.Length)];
                Instantiate(prefab, pos + Vector3.right * (i + 1), Quaternion.identity);
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
        isRunning = false;
        isPaused = true;
    }

    /// <summary>
    /// Continues gameplay after being paused.
    /// </summary>
    public void ResumeGame()
    {
        if (!isPaused) return;
        isRunning = true;
        isPaused = false;
    }

    /// <summary>
    /// Total distance the player has traveled during this run.
    /// </summary>
    public float GetDistance()
    {
        return distance;
    }

    /// <summary>
    /// Temporarily multiplies the game speed for a set duration.
    /// </summary>
    public void ActivateSpeedBoost(float duration, float multiplier)
    {
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
        if (SteamManager.Instance != null && coinComboMultiplier >= 10)
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
    /// increase through both the combo system and any purchased upgrades.
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
            coinComboMultiplier++;
            OnComboIncreased();
        }
        else
        {
            coinComboMultiplier = 1;
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
    /// Current coin combo multiplier. Returns one when no combo is
    /// active so callers can easily scale rewards based on the combo
    /// state.
    /// </summary>
    public int GetCoinComboMultiplier()
    {
        return coinComboMultiplier;
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
    /// Refreshes the on-screen combo multiplier text.
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
    /// Clears the static instance when the manager is destroyed.
    /// </summary>
    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
