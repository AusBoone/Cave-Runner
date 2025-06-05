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
    public float baseSpeed = 5f;
    public float speedIncrease = 0.1f;
    public Text scoreLabel;
    public Text highScoreLabel;
    public Text coinLabel;

    private float distance;
    private float currentSpeed;
    private int coins;
    private bool isRunning;
    private bool isPaused;
    private bool isGameOver;
    private UIManager uiManager;
    private float speedBoostTimer;
    private float speedMultiplier = 1f;
    private float gravityFlipTimer;
    private bool gravityFlipped;

    public float[] stageGoals;
    private int currentStage;
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
        if (!isRunning) return;

        currentSpeed += speedIncrease * Time.deltaTime;
        if (speedBoostTimer > 0f)
        {
            speedBoostTimer -= Time.deltaTime;
            if (speedBoostTimer <= 0f)
            {
                speedMultiplier = 1f;
            }
        }
        if (gravityFlipped)
        {
            gravityFlipTimer -= Time.deltaTime;
            if (gravityFlipTimer <= 0f)
            {
                gravityFlipped = false;
                Physics2D.gravity = new Vector2(Physics2D.gravity.x, -Physics2D.gravity.y);
            }
        }
        distance += currentSpeed * speedMultiplier * Time.deltaTime;
        if (stageGoals != null && currentStage < stageGoals.Length && distance >= stageGoals[currentStage])
        {
            currentStage++;
            OnStageUnlocked?.Invoke(currentStage);
        }
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
        isRunning = false;
        isPaused = false;
        isGameOver = true;
        int finalScore = Mathf.FloorToInt(distance);
        int highScore = PlayerPrefs.GetInt("HighScore", 0);
        if (finalScore > highScore)
        {
            highScore = finalScore;
            PlayerPrefs.SetInt("HighScore", highScore);
            if (SteamManager.Instance != null)
            {
                SteamManager.Instance.SaveHighScore(highScore);
            }
        }
        if (SteamManager.Instance != null)
        {
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

            SteamManager.Instance.UploadScore(finalScore);
        }
        if (uiManager != null)
        {
            uiManager.ShowGameOver(finalScore, highScore, coins);
        }
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
}
