using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public float baseSpeed = 5f;
    public float speedIncrease = 0.1f;
    public Text scoreLabel;
    public Text highScoreLabel;

    private float distance;
    private float currentSpeed;
    private bool isRunning;
    private bool isPaused;
    private bool isGameOver;
    private UIManager uiManager;

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
        UpdateHighScoreLabel();
    }

    void Update()
    {
        if (!isRunning) return;

        currentSpeed += speedIncrease * Time.deltaTime;
        distance += currentSpeed * Time.deltaTime;
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
        return currentSpeed;
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
        }
        if (uiManager != null)
        {
            uiManager.ShowGameOver(finalScore, highScore);
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

    public void SetUIManager(UIManager manager)
    {
        uiManager = manager;
    }

    private void UpdateHighScoreLabel()
    {
        if (highScoreLabel != null)
        {
            highScoreLabel.text = PlayerPrefs.GetInt("HighScore", 0).ToString();
        }
    }
}
