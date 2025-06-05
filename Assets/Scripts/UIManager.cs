using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_STANDALONE
using Steamworks;
#endif
using System.Collections.Generic;

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
    public GameObject leaderboardPanel;
    public Text leaderboardText;
    public GameObject workshopPanel;
    public Text workshopListText;

#if UNITY_STANDALONE
    private List<string> downloadedPacks = new List<string>();
#endif

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
        if (leaderboardPanel != null)
        {
            leaderboardPanel.SetActive(false);
        }
        if (workshopPanel != null)
        {
            workshopPanel.SetActive(false);
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

    /// <summary>
    /// Displays the leaderboard panel and populates it with scores from Steam.
    /// </summary>
    public void ShowLeaderboard()
    {
        if (leaderboardPanel != null)
        {
            leaderboardPanel.SetActive(true);
        }
#if UNITY_STANDALONE
        if (SteamManager.Instance != null)
        {
            SteamManager.Instance.FindOrCreateLeaderboard("HIGHSCORES", success =>
            {
                if (success)
                {
                    SteamManager.Instance.DownloadTopScores(entries =>
                    {
                        if (leaderboardText != null && entries != null)
                        {
                            System.Text.StringBuilder sb = new System.Text.StringBuilder();
                            for (int i = 0; i < entries.Length; i++)
                            {
                                string name = SteamFriends.GetFriendPersonaName(entries[i].m_steamIDUser);
                                sb.AppendLine((i + 1) + ". " + name + " - " + entries[i].m_nScore);
                            }
                            leaderboardText.text = sb.ToString();
                        }
                    });
                }
            });
        }
#endif
    }

    /// <summary>
    /// Hides the leaderboard panel.
    /// </summary>
    public void HideLeaderboard()
    {
        if (leaderboardPanel != null)
        {
            leaderboardPanel.SetActive(false);
        }
    }

    /// <summary>
    /// Displays the workshop panel and lists subscribed items.
    /// </summary>
    public void ShowWorkshop()
    {
        if (workshopPanel != null)
        {
            workshopPanel.SetActive(true);
        }
#if UNITY_STANDALONE
        if (WorkshopManager.Instance != null)
        {
            WorkshopManager.Instance.DownloadSubscribedItems(paths =>
            {
                downloadedPacks = paths;
                if (workshopListText != null)
                {
                    System.Text.StringBuilder sb = new System.Text.StringBuilder();
                    for (int i = 0; i < paths.Count; i++)
                    {
                        sb.AppendLine((i + 1) + ". " + paths[i]);
                    }
                    workshopListText.text = sb.ToString();
                }
            });
        }
#endif
    }

    /// <summary>
    /// Hides the workshop panel.
    /// </summary>
    public void HideWorkshop()
    {
        if (workshopPanel != null)
        {
            workshopPanel.SetActive(false);
        }
    }

    /// <summary>
    /// Applies the first downloaded workshop pack (placeholder logic).
    /// </summary>
    public void ApplyFirstWorkshopItem()
    {
#if UNITY_STANDALONE
        if (downloadedPacks.Count > 0)
        {
            Debug.Log("Applying workshop content from " + downloadedPacks[0]);
            // Here you would load assets from the folder and apply them.
        }
#endif
    }
}
