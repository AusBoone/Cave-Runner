using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System;

/// <summary>
/// Central controller for the endless runner. Tracks the player's
/// progress, handles pausing and game over logic and communicates with
/// the optional <see cref="SteamManager"/> for achievements and cloud
/// saves.
/// </summary>
public class GameManager : MonoBehaviour
{
    public float baseSpeed = 5f;              // initial world scroll speed
    public float speedIncrease = 0.1f;        // speed gain per second
    public Text scoreLabel;                   // UI label showing current distance
    public Text highScoreLabel;               // UI label showing best distance
    public Text coinLabel;                    // UI label showing collected coins

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
    /// Initializes the singleton instance and loads the saved high
    /// score from either PlayerPrefs or the Steam cloud.
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
        }
        else
        {
            Destroy(gameObject);
        }
        currentSpeed = baseSpeed;
        coins = 0;
        gravityFlipped = false;
        gravityFlipTimer = 0f;
        currentStage = 0;

        if (SteamManager.Instance != null)
        {
            int cloudScore = SteamManager.Instance.LoadHighScore();
            int localScore = PlayerPrefs.GetInt("HighScore", 0);
            if (cloudScore > localScore)
            {
                PlayerPrefs.SetInt("HighScore", cloudScore);
                PlayerPrefs.Save();
            }
        }

        UpdateHighScoreLabel();
        UpdateCoinLabel();
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

        // Reduce any active speed boost timer and reset the multiplier when finished
        if (speedBoostTimer > 0f)
        {
            speedBoostTimer -= Time.deltaTime;
            if (speedBoostTimer <= 0f)
            {
                speedMultiplier = 1f;
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

        // Update distance traveled applying any speed multipliers
        distance += currentSpeed * speedMultiplier * Time.deltaTime;

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
        return currentSpeed * speedMultiplier;
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

        // If gravity was flipped when the run ended, flip it back so menus and
        // future runs use the normal downward orientation.
        if (gravityFlipped)
        {
            Physics2D.gravity = new Vector2(Physics2D.gravity.x, -Physics2D.gravity.y);
            gravityFlipped = false;
            gravityFlipTimer = 0f;
        }
        int finalScore = Mathf.FloorToInt(distance);
        int highScore = PlayerPrefs.GetInt("HighScore", 0);

        // Persist a new high score locally and to Steam if available
        if (finalScore > highScore)
        {
            highScore = finalScore;
            PlayerPrefs.SetInt("HighScore", highScore);
            PlayerPrefs.Save();
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

            // Submit the score to the Steam leaderboard
            SteamManager.Instance.UploadScore(finalScore);
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
        currentSpeed = baseSpeed;
        coins = 0;
        speedMultiplier = 1f;
        speedBoostTimer = 0f;
        gravityFlipped = false;
        gravityFlipTimer = 0f;
        currentStage = 0;

        UpdateCoinLabel();
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
    /// Registers the UIManager so game events can trigger menu updates.
    /// </summary>
    public void SetUIManager(UIManager manager)
    {
        uiManager = manager;
    }

    /// <summary>
    /// Adds to the player's coin tally and updates the UI label.
    /// </summary>
    public void AddCoins(int amount)
    {
        coins += amount;
        UpdateCoinLabel();
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
    /// Refreshes the on-screen high score text from PlayerPrefs.
    /// </summary>
    private void UpdateHighScoreLabel()
    {
        if (highScoreLabel != null)
        {
            highScoreLabel.text = PlayerPrefs.GetInt("HighScore", 0).ToString();
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
