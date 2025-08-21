// DailyChallengeUI.cs
// -----------------------------------------------------------------------------
// Presents the player's current daily challenge and progress on screen. The
// label now uses TextMeshPro to ensure crisp rendering on all resolutions.
// Attach this component to a panel with a TMP_Text child and optional Slider.
// -----------------------------------------------------------------------------
using UnityEngine;
using UnityEngine.UI; // Slider support remains in the legacy UI namespace
using TMPro;           // TextMeshPro provides the TMP_Text component

/// <summary>
/// Simple UI component that displays the current daily challenge text and
/// progress percentage. Attach this script to a panel containing a
/// <see cref="TMP_Text"/> field and optional progress slider.
/// </summary>
public class DailyChallengeUI : MonoBehaviour
{
    [Tooltip("Label displaying the current daily challenge description.")]
    public TMP_Text challengeLabel; // replaced legacy Text with TMP_Text
    [Tooltip("Optional slider showing completion progress from 0 to 1.")]
    public Slider progressBar;

    /// <summary>
    /// Refreshes the text and progress bar every frame. If the manager is not
    /// yet initialised the method returns early to avoid null references.
    /// </summary>
    void Update()
    {
        if (DailyChallengeManager.Instance == null)
            return; // manager not ready; nothing to display yet

        // Show the latest challenge description using localized text.
        challengeLabel.text = DailyChallengeManager.Instance.GetChallengeText();

        int target = DailyChallengeManager.Instance.GetTarget();
        int progress = DailyChallengeManager.Instance.GetProgress();
        if (progressBar != null && target > 0)
        {
            // Slider expects a value between 0 and 1, hence the clamp.
            progressBar.value = Mathf.Clamp01(progress / (float)target);
        }
    }
}
