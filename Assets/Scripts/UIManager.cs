using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Manages the game's simple UI screens such as the start menu, pause menu
/// and game-over display. Interfaces with the <see cref="GameManager"/> to
/// start, pause and restart the game.
/// </summary>
public class UIManager : MonoBehaviour
{
    public GameObject startPanel;
    public GameObject gameOverPanel;
    public GameObject pausePanel;
    public Text finalScoreLabel;
    public Text highScoreLabel;
    public Text coinScoreLabel;

    /// <summary>
    /// Listens for the Escape key to toggle the pause menu while the game
    /// is running.
    /// </summary>
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

    /// <summary>
    /// Initializes the UI panels and registers this manager with the game
    /// manager.
    /// </summary>
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

    /// <summary>
    /// Called by the start button to begin a run.
    /// </summary>
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

    /// <summary>
    /// Displays the final results on the game-over screen.
    /// </summary>
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

    /// <summary>
    /// Shows the pause menu and pauses the game via <see cref="GameManager"/>.
    /// </summary>
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

    /// <summary>
    /// Hides the pause menu and resumes gameplay.
    /// </summary>
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

    /// <summary>
    /// Reloads the active scene to restart the game.
    /// </summary>
    public void Restart()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
