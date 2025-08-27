using UnityEngine;

/// <summary>
/// Simple first-run tutorial system displaying a sequence of panels. The
/// tutorial pauses gameplay while active and marks completion in the save file
/// so it only appears once per player profile unless manually restarted.
/// </summary>
public class TutorialManager : MonoBehaviour
{
    [Tooltip("Ordered list of tutorial panels shown to the player.")]
    public GameObject[] tutorialPanels;

    [Tooltip("Panel displayed after the player performs their first jump.")]
    public GameObject jumpTipPanel;

    [Tooltip("Panel displayed after the player performs their first slide.")]
    public GameObject slideTipPanel;

    public static TutorialManager Instance { get; private set; }

    private int index;
    private const string SeenKey = "TutorialSeen";

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        bool seen = SaveGameManager.Instance != null
            ? SaveGameManager.Instance.TutorialCompleted
            : PlayerPrefs.GetInt(SeenKey, 0) == 1;

        if (!seen)
        {
            BeginTutorial();
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Begins displaying tutorial panels and pauses time.
    /// </summary>
    public void BeginTutorial()
    {
        if (tutorialPanels == null || tutorialPanels.Length == 0)
            return;

        index = 0;
        Time.timeScale = 0f;
        ShowCurrent();
    }

    /// <summary>
    /// Advances to the next tutorial panel or ends the tutorial if done.
    /// Typically called by a "Next" button on each panel.
    /// </summary>
    public void Next()
    {
        index++;
        if (index >= tutorialPanels.Length)
        {
            EndTutorial();
        }
        else
        {
            ShowCurrent();
        }
    }

    // Activates the current panel and hides all others.
    private void ShowCurrent()
    {
        for (int i = 0; i < tutorialPanels.Length; i++)
        {
            if (tutorialPanels[i] != null)
                tutorialPanels[i].SetActive(i == index);
        }
    }

    // Finishes the tutorial and resumes normal time scale.
    private void EndTutorial()
    {
        if (SaveGameManager.Instance != null)
        {
            SaveGameManager.Instance.TutorialCompleted = true;
        }
        PlayerPrefs.SetInt(SeenKey, 1); // legacy fallback
        PlayerPrefs.Save();
        Time.timeScale = 1f;
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Triggered after the player's first jump. Shows the jump tip panel if it
    /// has not been displayed for the current profile.
    /// </summary>
    public void RegisterJump()
    {
        if (jumpTipPanel == null || SaveGameManager.Instance == null)
            return;
        if (!SaveGameManager.Instance.JumpTipShown)
        {
            SaveGameManager.Instance.JumpTipShown = true;
            PauseAndShow(jumpTipPanel);
        }
    }

    /// <summary>
    /// Triggered after the player's first slide to display the slide tip panel.
    /// </summary>
    public void RegisterSlide()
    {
        if (slideTipPanel == null || SaveGameManager.Instance == null)
            return;
        if (!SaveGameManager.Instance.SlideTipShown)
        {
            SaveGameManager.Instance.SlideTipShown = true;
            PauseAndShow(slideTipPanel);
        }
    }

    /// <summary>Hides the provided tip panel and resumes time.</summary>
    public void CloseTip(GameObject panel)
    {
        if (panel != null)
            panel.SetActive(false);
        Time.timeScale = 1f;
    }

    // Helper to pause the game and activate a tip panel.
    private void PauseAndShow(GameObject panel)
    {
        Time.timeScale = 0f;
        panel.SetActive(true);
    }
}
