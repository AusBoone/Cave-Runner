using UnityEngine;

/// <summary>
/// Simple first-run tutorial system displaying a sequence of panels. The
/// tutorial pauses gameplay while active and marks completion in PlayerPrefs so
/// it only appears once unless manually restarted.
/// </summary>
public class TutorialManager : MonoBehaviour
{
    [Tooltip("Ordered list of tutorial panels shown to the player.")]
    public GameObject[] tutorialPanels;

    private int index;
    private const string SeenKey = "TutorialSeen";

    void Start()
    {
        if (PlayerPrefs.GetInt(SeenKey, 0) == 0)
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
        PlayerPrefs.SetInt(SeenKey, 1);
        PlayerPrefs.Save();
        Time.timeScale = 1f;
        gameObject.SetActive(false);
    }
}
