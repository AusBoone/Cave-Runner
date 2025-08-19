// UIManager.cs
// -----------------------------------------------------------------------------
// Centralizes control of the game's menus and heads-up-display elements. The
// manager now includes a progress reporting API so loading screens can display
// combined progress while assets stream asynchronously. This complements the
// existing show/hide indicator previously added for addressable loading.
//
// 2026 addition summary
// Added LoggingHelper usage to gate nonessential Debug output behind a global
// flag so production builds can remain silent while developers retain verbose
// information inside the editor.
//
// 2029 update summary
// When leaderboard communication fails a localized error message is now shown
// in the UI so players receive feedback instead of an empty panel.
// -----------------------------------------------------------------------------

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
/// Manages the game's simple UI screens such as the start menu, pause menu and
/// game-over display. Input listening for the pause command now routes through
/// <see cref="InputManager"/> so players can rebind the action with the new
/// Input System. Interfaces with the <see cref="GameManager"/> to start, pause
/// and restart the game. Also exposes <see cref="AnimateComboLabel"/> which is
/// used to draw attention to the coin combo multiplier when it changes. This
/// revision introduces a loading indicator that other systems toggle while
/// Addressable assets load asynchronously. It now detects mobile platforms at
/// runtime and instantiates <c>MobileUI.prefab</c> from the Resources folder so
/// phones and tablets display a touch‑friendly canvas.
/// </summary>
public class UIManager : MonoBehaviour
{
    /// <summary>
    /// Singleton instance so other managers can easily show/hide the loading
    /// indicator while asynchronous operations occur.
    /// </summary>
    public static UIManager Instance { get; private set; }

    public GameObject startPanel;
    public GameObject gameOverPanel;
    public GameObject pausePanel;
    public GameObject settingsPanel;
    public Text finalScoreLabel;
    public Text highScoreLabel;
    public Text coinScoreLabel;
    public GameObject leaderboardPanel;
    public Text leaderboardText;
    [Tooltip("Client used for non-Steam leaderboard requests.")]
    public LeaderboardClient leaderboardClient;
    public GameObject workshopPanel;
    public GameObject achievementsPanel;
    public GameObject shopPanel;
    public Text workshopListText;
    [Tooltip("Panel containing a simple loading indicator graphic.")]
    public GameObject loadingPanel;
    [Tooltip("Optional slider visualizing loading progress from 0 to 1.")]
    public Slider loadingProgressBar;
    [Tooltip("External form URL for player feedback. Leave blank to hide the button.")]
    public string feedbackUrl = "";

    private const float panelHideDelay = 0.5f; // wait so hide animation can play

    // Instantiated at runtime when running on mobile platforms. Holds the
    // simplified canvas defined by MobileUI.prefab.
    private GameObject mobileCanvas;

    /// <summary>
    /// Configure the singleton instance.
    /// </summary>
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        // Instantiate the simplified mobile UI when running on a handheld
        // device so players can easily tap the larger controls.
        if (Application.isMobilePlatform)
        {
            GameObject prefab = Resources.Load<GameObject>("UI/MobileUI");
            if (prefab != null)
            {
                mobileCanvas = Instantiate(prefab);
            }
            else
            {
                // Verbose logging alerts developers if the expected mobile
                // prefab is missing but remains silent in production builds.
                LoggingHelper.LogWarning("MobileUI prefab not found in Resources/UI");
            }
        }
    }

    // Immediately disables a panel without playing an animation
    private void HidePanelImmediate(GameObject panel)
    {
        if (panel != null)
        {
            panel.SetActive(false);
        }
    }

    // Enables a panel and triggers its Animator if present
    private void ShowPanel(GameObject panel)
    {
        if (panel != null)
        {
            panel.SetActive(true);
            Animator anim = panel.GetComponent<Animator>();
            anim?.SetTrigger("Show");
        }
    }

    // Starts the hide animation and disables the panel afterwards
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

    // Waits for the hide animation to finish before disabling the panel
    private System.Collections.IEnumerator DeactivateAfterDelay(GameObject panel)
    {
        yield return new WaitForSeconds(panelHideDelay);
        panel.SetActive(false);
    }

    /// <summary>
    /// Enables the loading panel while addressable assets are loading.
    /// </summary>
    public void ShowLoadingIndicator()
    {
        if (loadingPanel != null)
        {
            loadingPanel.SetActive(true);
        }
    }

    /// <summary>
    /// Hides the loading panel once asset loading has finished.
    /// </summary>
    public void HideLoadingIndicator()
    {
        if (loadingPanel != null)
        {
            loadingPanel.SetActive(false);
        }
    }

    // Stores the most recent normalized progress value so other systems or
    // unit tests can inspect how far asynchronous loading has advanced.
    private float loadingProgress;

    /// <summary>
    /// Updates the visual progress indicator. Callers provide a normalized
    /// value between 0 (no work complete) and 1 (all work finished). The value
    /// is clamped to this range to guard against invalid inputs. If a slider is
    /// assigned in the inspector it will be updated; otherwise the value is
    /// simply cached for later retrieval.
    /// </summary>
    /// <param name="progress">Normalized progress value.</param>
    public virtual void SetLoadingProgress(float progress)
    {
        loadingProgress = Mathf.Clamp01(progress);
        if (loadingProgressBar != null)
        {
            loadingProgressBar.value = loadingProgress;
        }
    }

    /// <summary>
    /// Current progress value in the range [0,1]. Exposed primarily for test
    /// verification but may be queried by other systems as needed.
    /// </summary>
    public float LoadingProgress => loadingProgress;

#if UNITY_STANDALONE
    private List<string> downloadedPacks = new List<string>();
#endif

    /// <summary>
    /// Listens for the Escape key to toggle the pause menu while the game
    /// is running.
    /// </summary>
    void Update()
    {
        // Toggle pause state when the configured pause input is pressed during a run
        if (InputManager.GetPauseDown() && GameManager.Instance != null && GameManager.Instance.IsRunning())
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
        HidePanelImmediate(achievementsPanel);
        HidePanelImmediate(shopPanel);
        HidePanelImmediate(settingsPanel);
        HidePanelImmediate(loadingPanel);
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
            string fmt = LocalizationManager.Get("final_score_format");
            finalScoreLabel.text = string.Format(fmt, score);
        }
        if (highScoreLabel != null)
        {
            string fmt = LocalizationManager.Get("high_score_format");
            highScoreLabel.text = string.Format(fmt, highScore);
        }
        if (coinScoreLabel != null)
        {
            string fmt = LocalizationManager.Get("coins_format");
            coinScoreLabel.text = string.Format(fmt, coins);
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
    /// Displays the leaderboard panel and populates it with scores retrieved
    /// from either Steam or the HTTP-based <see cref="LeaderboardClient"/>.
    /// </summary>
    public void ShowLeaderboard()
    {
        ShowPanel(leaderboardPanel);
#if UNITY_STANDALONE
        if (SteamManager.Instance != null)
        {
            // Use the configured leaderboard ID from SteamManager so multiple boards can be supported across deployments.
            string id = SteamManager.Instance.leaderboardId;
            SteamManager.Instance.FindOrCreateLeaderboard(id, success =>
            {
                if (success)
                {
                    SteamManager.Instance.DownloadTopScores(entries =>
                    {
                        if (leaderboardText != null && entries != null)
                        {
                            var sb = new System.Text.StringBuilder();
                            string fmt = LocalizationManager.Get("leaderboard_entry_format");
                            for (int i = 0; i < entries.Length; i++)
                            {
                                string name = SteamFriends.GetFriendPersonaName(entries[i].m_steamIDUser);
                                sb.AppendLine(string.Format(fmt, i + 1, name, entries[i].m_nScore));
                            }
                            leaderboardText.text = sb.ToString();
                        }
                    });
                }
                else if (leaderboardClient != null)
                {
                    // Steam call failed; fall back to HTTP client and report
                    // success status so the UI can surface any errors.
                    StartCoroutine(leaderboardClient.GetTopScores(DisplayScores));
                }
            });
        }
        else if (leaderboardClient != null)
        {
            StartCoroutine(leaderboardClient.GetTopScores(DisplayScores));
        }
#else
        if (leaderboardClient != null)
        {
            StartCoroutine(leaderboardClient.GetTopScores(DisplayScores));
        }
#endif
    }

    // Helper to format and display leaderboard entries in the panel.
    // Marked public so unit tests and other systems can invoke the formatting
    // logic directly without relying on coroutines.
    public void DisplayScores(List<LeaderboardClient.ScoreEntry> scores, bool success)
    {
        if (leaderboardText == null)
        {
            return;
        }

        // When the client reports failure, show a user-friendly message instead
        // of blank text so players understand the leaderboard could not be
        // reached. A localized string is used when available with an English
        // fallback otherwise.
        if (!success)
        {
            string msg = LocalizationManager.Get("leaderboard_error");
            if (msg == "leaderboard_error")
            {
                msg = "Failed to load leaderboard.";
            }
            leaderboardText.text = msg;
            return;
        }

        // Successful retrieval: format the list of scores according to the
        // localized entry pattern.
        if (scores != null)
        {
            var sb = new System.Text.StringBuilder();
            string fmt = LocalizationManager.Get("leaderboard_entry_format");
            for (int i = 0; i < scores.Count; i++)
            {
                sb.AppendLine(string.Format(fmt, i + 1, scores[i].name, scores[i].score));
            }
            leaderboardText.text = sb.ToString();
        }
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
    /// Displays the achievements panel populated by <see cref="AchievementsMenu"/>.
    /// </summary>
    public void ShowAchievements()
    {
        ShowPanel(achievementsPanel);
    }

    /// <summary>
    /// Hides the achievements panel.
    /// </summary>
    public void HideAchievements()
    {
        HidePanel(achievementsPanel);
    }

    /// <summary>
    /// Displays the shop panel where upgrades can be purchased.
    /// </summary>
    public void ShowShop()
    {
        ShowPanel(shopPanel);
    }

    /// <summary>
    /// Hides the shop panel.
    /// </summary>
    public void HideShop()
    {
        HidePanel(shopPanel);
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
    /// Performs a brief scaling animation on the provided combo multiplier
    /// label to draw the player's attention when the value changes.
    /// </summary>
    public void AnimateComboLabel(Text label)
    {
        if (label != null)
        {
            StartCoroutine(ScaleLabel(label));
        }
    }

    // Coroutine that smoothly scales the label up and back down.
    private IEnumerator ScaleLabel(Text label)
    {
        const float duration = 0.2f;
        Vector3 baseScale = label.transform.localScale;
        float timer = 0f;
        while (timer < duration)
        {
            float t = timer / duration;
            float scale = 1f + 0.5f * Mathf.Sin(t * Mathf.PI);
            label.transform.localScale = baseScale * scale;
            timer += Time.unscaledDeltaTime;
            yield return null;
        }
        label.transform.localScale = baseScale;
    }

    /// <summary>
    /// Loads assets from the first downloaded workshop item and applies
    /// them to the scene. Searches for an AssetBundle or supported files
    /// like PNG images within the downloaded folder. The background sprite
    /// and an optional prefab can be swapped at runtime.
    /// </summary>
    // Loads content from the first downloaded workshop item and applies it
    public void ApplyFirstWorkshopItem()
    {
#if UNITY_STANDALONE
        if (downloadedPacks == null || downloadedPacks.Count == 0)
        {
            // Inform developers when no workshop items were discovered while
            // keeping release builds free of noise.
            LoggingHelper.LogWarning("No downloaded workshop items available.");
            return;
        }

        string packPath = downloadedPacks[0];
        if (!Directory.Exists(packPath))
        {
            // Critical error logs still surface even when verbose logging is
            // disabled to ensure player issues are reported.
            LoggingHelper.LogError("Workshop path not found: " + packPath);
            return;
        }

        try
        {
            // Look for an asset bundle first. Unity packages can have either
            // the .bundle or legacy .unity3d extension.
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
                    LoggingHelper.LogError("Failed to load AssetBundle: " + bundlePath);
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
                            // This message helps during development to verify
                            // the correct assets are applied.
                            LoggingHelper.Log("Applied background sprite from bundle.");
                        }
                    }
                }
                else
                {
                    LoggingHelper.LogWarning("BackgroundSprite not found in bundle.");
                }

                // Example prefab instantiation
                GameObject prefab = bundle.LoadAsset<GameObject>("CustomPrefab");
                if (prefab != null)
                {
                    Instantiate(prefab, Vector3.zero, Quaternion.identity);
                    LoggingHelper.Log("Instantiated prefab from bundle.");
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
                                LoggingHelper.Log("Applied background sprite from file: " + Path.GetFileName(pngPath));
                            }
                        }
                    }
                    else
                    {
                        LoggingHelper.LogError("Failed to load texture from " + pngPath);
                    }
                }
                else
                {
                    LoggingHelper.LogWarning("No asset bundle or supported files found in " + packPath);
                }
            }
        }
        catch (System.Exception ex)
        {
            LoggingHelper.LogError("Error applying workshop item: " + ex.Message);
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
