using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

// Handles game state and communicates with SteamManager for achievements and cloud saves.

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

    private const string AchDistance1000 = "ACH_DISTANCE_1000";
    private const string AchCoins50 = "ACH_COINS_50";

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

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
        currentSpeed = baseSpeed;
        coins = 0;

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
        distance += currentSpeed * speedMultiplier * Time.deltaTime;
        if (scoreLabel != null)
        {
            scoreLabel.text = Mathf.FloorToInt(distance).ToString();
        }
    }

    public float GetSpeed()
    {
        if (!isRunning)
        {
            return 0f;
        }
        return currentSpeed * speedMultiplier;
    }

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
            if (coins >= 50)
            {
                SteamManager.Instance.UnlockAchievement(AchCoins50);
            }
        }
        if (uiManager != null)
        {
            uiManager.ShowGameOver(finalScore, highScore, coins);
        }
        UpdateHighScoreLabel();
    }

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
        UpdateCoinLabel();
    }

    public void PauseGame()
    {
        if (!isRunning || isPaused) return;
        isRunning = false;
        isPaused = true;
    }

    public void ResumeGame()
    {
        if (!isPaused) return;
        isRunning = true;
        isPaused = false;
    }

    public float GetDistance()
    {
        return distance;
    }

    public void ActivateSpeedBoost(float duration, float multiplier)
    {
        speedMultiplier = multiplier;
        speedBoostTimer = duration;
    }

    public void SetUIManager(UIManager manager)
    {
        uiManager = manager;
    }

    public void AddCoins(int amount)
    {
        coins += amount;
        UpdateCoinLabel();
    }

    public int GetCoins()
    {
        return coins;
    }

    private void UpdateHighScoreLabel()
    {
        if (highScoreLabel != null)
        {
            highScoreLabel.text = PlayerPrefs.GetInt("HighScore", 0).ToString();
        }
    }

    private void UpdateCoinLabel()
    {
        if (coinLabel != null)
        {
            coinLabel.text = coins.ToString();
        }
    }
}
