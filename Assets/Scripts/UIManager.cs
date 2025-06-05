using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_STANDALONE
using Steamworks;
#endif
using System.Collections.Generic;
using System.Collections;
using System.IO;
using System.Linq;

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
    public GameObject settingsPanel;
    public Text finalScoreLabel;
    public Text highScoreLabel;
    public Text coinScoreLabel;
    public GameObject leaderboardPanel;
    public Text leaderboardText;
    public GameObject workshopPanel;
    public Text workshopListText;
    [Tooltip("External form URL for player feedback. Leave blank to hide the button.")]
    public string feedbackUrl = "";

    private const float panelHideDelay = 0.5f;

    private void HidePanelImmediate(GameObject panel)
    {
        if (panel != null)
        {
            panel.SetActive(false);
        }
    }

    private void ShowPanel(GameObject panel)
    {
        if (panel != null)
        {
            panel.SetActive(true);
            Animator anim = panel.GetComponent<Animator>();
            anim?.SetTrigger("Show");
        }
    }

    private void HidePanel(GameObject panel)
    {
        if (panel != null)
        {
            Animator anim = panel.GetComponent<Animator>();
            if (anim != null)
            {
                anim.SetTrigger("Hide");
                StartCoroutine(DeactivateAfterDelay(panel));
            }
            else
            {
                panel.SetActive(false);
            }
        }
    }

    private System.Collections.IEnumerator DeactivateAfterDelay(GameObject panel)
    {
        yield return new WaitForSeconds(panelHideDelay);
        panel.SetActive(false);
    }

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
        ShowPanel(startPanel);
        HidePanelImmediate(gameOverPanel);
        HidePanelImmediate(pausePanel);
        HidePanelImmediate(leaderboardPanel);
        HidePanelImmediate(workshopPanel);
        HidePanelImmediate(settingsPanel);
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
        HidePanel(startPanel);
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
        ShowPanel(gameOverPanel);
    }

    /// <summary>
    /// Shows the pause menu and pauses the game via <see cref="GameManager"/>.
    /// </summary>
    public void Pause()
    {
        ShowPanel(pausePanel);
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
        HidePanel(pausePanel);
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
        ShowPanel(leaderboardPanel);
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
        HidePanel(leaderboardPanel);
    }

    /// <summary>
    /// Displays the workshop panel and lists subscribed items.
    /// </summary>
    public void ShowWorkshop()
    {
        ShowPanel(workshopPanel);
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
        HidePanel(workshopPanel);
    }

    /// <summary>
    /// Displays the settings panel.
    /// </summary>
    public void ShowSettings()
    {
        ShowPanel(settingsPanel);
    }

    /// <summary>
    /// Hides the settings panel.
    /// </summary>
    public void HideSettings()
    {
        HidePanel(settingsPanel);
    }

    /// <summary>
    /// Loads assets from the first downloaded workshop item and applies
    /// them to the scene. Searches for an AssetBundle or supported files
    /// like PNG images within the downloaded folder. The background sprite
    /// and an optional prefab can be swapped at runtime.
    /// </summary>
    public void ApplyFirstWorkshopItem()
    {
#if UNITY_STANDALONE
        if (downloadedPacks == null || downloadedPacks.Count == 0)
        {
            Debug.LogWarning("No downloaded workshop items available.");
            return;
        }

        string packPath = downloadedPacks[0];
        if (!Directory.Exists(packPath))
        {
            Debug.LogError("Workshop path not found: " + packPath);
            return;
        }

        try
        {
            // Look for an asset bundle first
            string bundlePath = Directory.GetFiles(packPath, "*.bundle").FirstOrDefault();
            if (string.IsNullOrEmpty(bundlePath))
            {
                bundlePath = Directory.GetFiles(packPath, "*.unity3d").FirstOrDefault();
            }

            if (!string.IsNullOrEmpty(bundlePath))
            {
                var bundle = AssetBundle.LoadFromFile(bundlePath);
                if (bundle == null)
                {
                    Debug.LogError("Failed to load AssetBundle: " + bundlePath);
                    return;
                }

                // Example sprite replacement
                Sprite bg = bundle.LoadAsset<Sprite>("BackgroundSprite");
                if (bg != null)
                {
                    var bgObj = FindObjectOfType<ParallaxBackground>();
                    if (bgObj != null)
                    {
                        var sr = bgObj.GetComponent<SpriteRenderer>();
                        if (sr != null)
                        {
                            sr.sprite = bg;
                            Debug.Log("Applied background sprite from bundle.");
                        }
                    }
                }
                else
                {
                    Debug.LogWarning("BackgroundSprite not found in bundle.");
                }

                // Example prefab instantiation
                GameObject prefab = bundle.LoadAsset<GameObject>("CustomPrefab");
                if (prefab != null)
                {
                    Instantiate(prefab, Vector3.zero, Quaternion.identity);
                    Debug.Log("Instantiated prefab from bundle.");
                }

                bundle.Unload(false);
            }
            else
            {
                // Fallback: try a raw PNG to swap background
                string pngPath = Directory.GetFiles(packPath, "*.png").FirstOrDefault();
                if (!string.IsNullOrEmpty(pngPath))
                {
                    byte[] data = File.ReadAllBytes(pngPath);
                    Texture2D tex = new Texture2D(2, 2);
                    if (tex.LoadImage(data))
                    {
                        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                        var bgObj = FindObjectOfType<ParallaxBackground>();
                        if (bgObj != null)
                        {
                            var sr = bgObj.GetComponent<SpriteRenderer>();
                            if (sr != null)
                            {
                                sr.sprite = sprite;
                                Debug.Log("Applied background sprite from file: " + Path.GetFileName(pngPath));
                            }
                        }
                    }
                    else
                    {
                        Debug.LogError("Failed to load texture from " + pngPath);
                    }
                }
                else
                {
                    Debug.LogWarning("No asset bundle or supported files found in " + packPath);
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Error applying workshop item: " + ex.Message);
        }
#endif
    }

    /// <summary>
    /// Opens an external feedback form using the configured URL. The button
    /// can be disabled by leaving <see cref="feedbackUrl"/> empty.
    /// </summary>
    public void OpenFeedbackForm()
    {
        if (!string.IsNullOrEmpty(feedbackUrl))
        {
            Application.OpenURL(feedbackUrl);
        }
    }
}
