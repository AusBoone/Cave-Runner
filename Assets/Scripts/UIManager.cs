using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public GameObject startPanel;
    public GameObject gameOverPanel;
    public GameObject pausePanel;
    public Text finalScoreLabel;
    public Text highScoreLabel;
    public Text coinScoreLabel;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) && GameManager.Instance != null && GameManager.Instance.IsRunning())
        {
            if (GameManager.Instance.IsPaused())
            {
                Resume();
            }
            else
            {
                Pause();
            }
        }
    }

    void Start()
    {
        if (startPanel != null)
        {
            startPanel.SetActive(true);
        }
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }
        if (pausePanel != null)
        {
            pausePanel.SetActive(false);
        }
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetUIManager(this);
        }
    }

    public void Play()
    {
        if (startPanel != null)
        {
            startPanel.SetActive(false);
        }
        if (GameManager.Instance != null)
        {
            GameManager.Instance.StartGame();
        }
    }

    public void ShowGameOver(int score, int highScore, int coins)
    {
        if (finalScoreLabel != null)
        {
            finalScoreLabel.text = score.ToString();
        }
        if (highScoreLabel != null)
        {
            highScoreLabel.text = highScore.ToString();
        }
        if (coinScoreLabel != null)
        {
            coinScoreLabel.text = coins.ToString();
        }
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
        }
    }

    public void Pause()
    {
        if (pausePanel != null)
        {
            pausePanel.SetActive(true);
        }
        if (GameManager.Instance != null)
        {
            GameManager.Instance.PauseGame();
        }
        Time.timeScale = 0f;
    }

    public void Resume()
    {
        if (pausePanel != null)
        {
            pausePanel.SetActive(false);
        }
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ResumeGame();
        }
        Time.timeScale = 1f;
    }

    public void Restart()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
