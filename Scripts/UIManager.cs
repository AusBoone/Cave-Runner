// UIManager.cs
// -----------------------------------------------------------------------------
// For a high-level system diagram see docs/ArchitectureOverview.md.
// Centralizes control of the game's menus and heads-up-display elements. The
// manager now includes a progress reporting API so loading screens can display
// combined progress while assets stream asynchronously. This complements the
// existing show/hide indicator previously added for addressable loading.

using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI; // Retained for Slider and other legacy UI components
#if UNITY_STANDALONE
using Steamworks;
#endif
using TMPro; // TextMeshPro for TMP_Text components
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
    public TMP_Text finalScoreLabel;
    public TMP_Text highScoreLabel;
    public TMP_Text coinScoreLabel;
    public GameObject leaderboardPanel;
    public TMP_Text leaderboardText;
    [Tooltip("Client used for non-Steam leaderboard requests.")]
    public LeaderboardClient leaderboardClient;
    public GameObject workshopPanel;
    public GameObject achievementsPanel;
    public GameObject shopPanel;
    public TMP_Text workshopListText;
    [Tooltip("Panel containing a simple loading indicator graphic.")]
    public GameObject loadingPanel;
    [Tooltip("Optional slider visualizing loading progress from 0 to 1.")]
    public Slider loadingProgressBar;
    [Tooltip("Small icon or status text shown while network calls are pending.")]
    public GameObject networkSpinner;
    [Tooltip("External form URL for player feedback. Leave blank to hide the button.")]
    public string feedbackUrl = "";

    /// <summary>
    /// Cached reference to the level's parallax background. Assign in the
    /// inspector for manual control or leave empty to have <see cref="Awake"/>
    /// discover it once at startup.
    /// </summary>
    [SerializeField]
    private ParallaxBackground parallaxBackground;

    /// <summary>
    /// Read-only accessor used by tests and other systems that need the
    /// background reference.
    /// </summary>
    public ParallaxBackground ParallaxBackground => parallaxBackground;

    private const float panelHideDelay = 0.5f; // wait so hide animation can play

    // Instantiated at runtime when running on mobile platforms. Holds the
    // simplified canvas defined by MobileUI.prefab.
    private GameObject mobileCanvas;

    // Flags indicating whether critical UI references are present. Awake sets
    // these after validation so later methods can quickly skip features that
    // would otherwise dereference missing objects and throw exceptions.
    private bool hasStartPanel;
    private bool hasGameOverPanel;
    private bool hasPausePanel;
    private bool hasFinalScoreLabel;
    private bool hasHighScoreLabel;
    private bool hasCoinScoreLabel;

    /// <summary>
    /// Centralized helper that verifies a serialized field was assigned in the
    /// inspector. When the reference is missing, an error is logged and false is
    /// returned so callers can disable dependent functionality.
    /// </summary>
    /// <param name="obj">Reference to validate.</param>
    /// <param name="name">Human readable field name used in the log message.</param>
    /// <returns>True when the reference is non-null and safe to use.</returns>
    private bool ValidateReference(Object obj, string name)
    {
        if (obj == null)
        {
            LoggingHelper.LogError($"{name} reference is missing; related UI features will be disabled to prevent errors.");
            return false;
        }
        return true;
    }

    /// <summary>
    /// Configure the singleton instance and validate inspector assignments. The
    /// validation step ensures all critical UI references are present before
    /// other systems invoke this manager.
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
        // device so players can easily tap the larger controls. The prefab is
        // now loaded asynchronously so the main thread is never blocked while
        // Unity retrieves assets from disk. Awake cannot yield directly, so a
        // coroutine performs the asynchronous work and notifies us via
        // callbacks once complete.
        if (Application.isMobilePlatform)
        {
            // Kick off the coroutine that loads the prefab using
            // Resources.LoadAsync and instantiates it when ready.
            StartCoroutine(LoadMobileCanvasAsync());
        }

        // Resolve the parallax background once so subsequent lookups do not
        // allocate or accidentally miss the object if it appears later.
        if (parallaxBackground == null)
        {
            parallaxBackground = FindObjectOfType<ParallaxBackground>();
        }

        // Validate that critical UI elements were wired in the inspector. Each
        // reference logs an explicit error and toggles a flag so later
        // interactions can safely skip unavailable features instead of throwing
        // a NullReferenceException at runtime.
        hasStartPanel = ValidateReference(startPanel, nameof(startPanel));
        hasGameOverPanel = ValidateReference(gameOverPanel, nameof(gameOverPanel));
        hasPausePanel = ValidateReference(pausePanel, nameof(pausePanel));
        hasFinalScoreLabel = ValidateReference(finalScoreLabel, nameof(finalScoreLabel));
        hasHighScoreLabel = ValidateReference(highScoreLabel, nameof(highScoreLabel));
        hasCoinScoreLabel = ValidateReference(coinScoreLabel, nameof(coinScoreLabel));
    }

    /// <summary>
    /// Asynchronously loads the MobileUI prefab from the Resources folder and
    /// instantiates it once the asset is available. The coroutine yields until
    /// <see cref="Resources.LoadAsync"/> finishes so initialization does not
    /// block the main thread. A warning is logged when the prefab cannot be
    /// located, allowing developers to diagnose missing assets.
    /// </summary>
    /// <param name="resourcePath">
    /// Optional Resources path used to locate the prefab. Tests pass a
    /// non‑existent path to exercise the warning case. Defaults to
    /// "UI/MobileUI" for production usage.
    /// </param>
    private IEnumerator LoadMobileCanvasAsync(string resourcePath = "UI/MobileUI")
    {
        // Begin loading the prefab without blocking. Unity will continue
        // executing other startup logic while this request processes.
        ResourceRequest request = Resources.LoadAsync<GameObject>(resourcePath);

        // Yield control until the asynchronous request completes. This ensures
        // the prefab is fully loaded before instantiation proceeds.
        yield return request;

        // Extract the loaded asset. Casting directly would throw if the load
        // failed, so we perform a safe cast and validate the result.
        GameObject prefab = request.asset as GameObject;
        if (prefab != null)
        {
            // Create the mobile canvas and persist it across scene loads so the
            // touch controls remain available during transitions.
            mobileCanvas = Instantiate(prefab);
            DontDestroyOnLoad(mobileCanvas);
        }
        else
        {
            // Verbose logging alerts developers to missing assets while release
            // builds remain silent.
            LoggingHelper.LogWarning($"{resourcePath} prefab not found in Resources");
        }
    }

    /// <summary>
    /// Cleans up persistent UI when this manager is destroyed. The mobile
    /// canvas is marked as "DontDestroyOnLoad" so scene changes do not remove
    /// it automatically. Destroying it here prevents multiple copies from
    /// lingering if a new <see cref="UIManager"/> is created after a
    /// transition. The singleton instance reference is also cleared so future
    /// instances can initialize correctly.
    /// </summary>
    void OnDestroy()
    {
        if (mobileCanvas != null)
        {
            Destroy(mobileCanvas);
            mobileCanvas = null;
        }

        if (Instance == this)
        {
            Instance = null;
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

    /// <summary>
    /// Shows a small spinner or status label to indicate that a network
    /// operation is currently in progress. Network-related classes call this
    /// before initiating web requests so players receive immediate visual
    /// feedback. The spinner remains visible until
    /// <see cref="HideNetworkSpinner"/> is invoked.
    /// </summary>
    public void ShowNetworkSpinner()
    {
        if (networkSpinner != null)
        {
            networkSpinner.SetActive(true);
        }
    }

    /// <summary>
    /// Hides the network activity spinner once all outstanding network
    /// operations have completed. Callers are responsible for pairing each
    /// <see cref="ShowNetworkSpinner"/> invocation with this method to ensure
    /// the spinner accurately reflects current activity.
    /// </summary>
    public void HideNetworkSpinner()
    {
        if (networkSpinner != null)
        {
            networkSpinner.SetActive(false);
        }
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
        // Initialize panels based on the presence flags computed in Awake. Each
        // helper method already null-checks, but the flags avoid invoking logic
        // for features explicitly disabled due to missing references.
        if (hasStartPanel)
        {
            ShowPanel(startPanel);
        }
        if (hasGameOverPanel)
        {
            HidePanelImmediate(gameOverPanel);
        }
        if (hasPausePanel)
        {
            HidePanelImmediate(pausePanel);
        }
        HidePanelImmediate(leaderboardPanel);
        HidePanelImmediate(workshopPanel);
        HidePanelImmediate(achievementsPanel);
        HidePanelImmediate(shopPanel);
        HidePanelImmediate(settingsPanel);
        HidePanelImmediate(loadingPanel);
        HidePanelImmediate(networkSpinner);
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
        if (hasStartPanel)
        {
            HidePanel(startPanel);
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
        if (hasFinalScoreLabel)
        {
            string fmt = LocalizationManager.Get("final_score_format");
            finalScoreLabel.text = string.Format(fmt, score);
        }
        if (hasHighScoreLabel)
        {
            string fmt = LocalizationManager.Get("high_score_format");
            highScoreLabel.text = string.Format(fmt, highScore);
        }
        if (hasCoinScoreLabel)
        {
            string fmt = LocalizationManager.Get("coins_format");
            coinScoreLabel.text = string.Format(fmt, coins);
        }
        if (hasGameOverPanel)
        {
            ShowPanel(gameOverPanel);
        }
    }

    /// <summary>
    /// Shows the pause menu and pauses the game via <see cref="GameManager"/>.
    /// </summary>
    public void Pause()
    {
        if (hasPausePanel)
        {
            ShowPanel(pausePanel);
        }
        if (GameManager.Instance != null)
        {
            // Delegates the actual pausing and time-scale adjustment to
            // GameManager so only one system controls global time state.
            GameManager.Instance.PauseGame();
        }
    }

    /// <summary>
    /// Hides the pause menu and resumes gameplay.
    /// </summary>
    public void Resume()
    {
        if (hasPausePanel)
        {
            HidePanel(pausePanel);
        }
        if (GameManager.Instance != null)
        {
            // GameManager restores normal gameplay and resets time scale.
            GameManager.Instance.ResumeGame();
        }
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
    public void DisplayScores(List<LeaderboardClient.ScoreEntry> scores, bool success, LeaderboardClient.ErrorCode error = LeaderboardClient.ErrorCode.Unknown)
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
            ShowLeaderboardError(error);
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
    /// Shows a localized error message in the leaderboard text field based on
    /// the provided error code. This centralizes mapping of codes to
    /// human‑readable strings so both upload and download operations present
    /// consistent messaging.
    /// </summary>
    public void ShowLeaderboardError(LeaderboardClient.ErrorCode code)
    {
        if (leaderboardText == null)
        {
            return;
        }

        string key;
        switch (code)
        {
            case LeaderboardClient.ErrorCode.CertificateError:
                key = "leaderboard_error_certificate";
                break;
            case LeaderboardClient.ErrorCode.HttpError:
                key = "leaderboard_error_http";
                break;
            case LeaderboardClient.ErrorCode.NetworkError:
                key = "leaderboard_error_network";
                break;
            case LeaderboardClient.ErrorCode.Timeout:
                key = "leaderboard_error_timeout";
                break;
            default:
                key = "leaderboard_error_unknown";
                break;
        }

        string msg = LocalizationManager.Get(key);
        if (msg == key)
        {
            // Fallback English strings when localization is missing to ensure
            // the player still receives useful feedback.
            switch (code)
            {
                case LeaderboardClient.ErrorCode.CertificateError:
                    msg = "Certificate validation failed.";
                    break;
                case LeaderboardClient.ErrorCode.HttpError:
                    msg = "Server returned an error.";
                    break;
                case LeaderboardClient.ErrorCode.NetworkError:
                    msg = "Network unreachable.";
                    break;
                case LeaderboardClient.ErrorCode.Timeout:
                    msg = "Request timed out.";
                    break;
                default:
                    msg = "Failed to load leaderboard.";
                    break;
            }
        }

        leaderboardText.text = msg;
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
    /// label to draw the player's attention when the value changes. Uses
    /// <see cref="TMP_Text"/> to leverage TextMeshPro rendering.
    /// </summary>
    public void AnimateComboLabel(TMP_Text label)
    {
        if (label != null)
        {
            StartCoroutine(ScaleLabel(label));
        }
    }

    // Coroutine that smoothly scales the label up and back down.
    private IEnumerator ScaleLabel(TMP_Text label)
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
                    // Utilize the cached parallax background to avoid costly
                    // scene searches each time a workshop item is applied.
                    if (parallaxBackground != null)
                    {
                        var sr = parallaxBackground.GetComponent<SpriteRenderer>();
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
                        // Use the cached background reference and skip work if
                        // it is missing from the active scene.
                        if (parallaxBackground != null)
                        {
                            var sr = parallaxBackground.GetComponent<SpriteRenderer>();
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
