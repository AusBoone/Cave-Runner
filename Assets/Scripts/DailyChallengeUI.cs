using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Simple UI component that displays the current daily challenge text and
/// progress percentage. Attach this script to a panel containing a Text field
/// and optional progress slider.
/// </summary>
public class DailyChallengeUI : MonoBehaviour
{
    public Text challengeLabel;
    public Slider progressBar;

    void Update()
    {
        if (DailyChallengeManager.Instance == null)
            return;

        challengeLabel.text = DailyChallengeManager.Instance.GetChallengeText();

        int target = DailyChallengeManager.Instance.GetTarget();
        int progress = DailyChallengeManager.Instance.GetProgress();
        if (progressBar != null && target > 0)
        {
            progressBar.value = Mathf.Clamp01(progress / (float)target);
        }
    }
}
