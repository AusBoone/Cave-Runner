/*
 * CoinBonusIndicator.cs
 * -----------------------------------------------------------------------------
 * UI helper that displays the remaining duration of the coin bonus effect on
 * screen. When the bonus is inactive the label's GameObject is disabled so it
 * does not occupy layout space. Attach this script to a UI GameObject that has
 * a child TMP_Text component assigned to 'timerLabel'.
 * -----------------------------------------------------------------------------
 */

using UnityEngine;
using TMPro; // TextMeshPro provides TMP_Text for crisp UI text

/// <summary>
/// Updates a UI <see cref="TMP_Text"/> to show the current coin bonus
/// multiplier and countdown. The label is automatically hidden when the bonus
/// expires.
/// </summary>
public class CoinBonusIndicator : MonoBehaviour
{
    [Tooltip("UI text displaying multiplier and remaining seconds.")]
    /// <summary>
    /// Label that shows the current multiplier and seconds left. When the bonus
    /// expires this GameObject is disabled to collapse its layout.
    /// </summary>
    public TMP_Text timerLabel;

    /// <summary>
    /// Refreshes the bonus timer display each frame and hides the label when
    /// no bonus is active. The UI is enabled or disabled rather than merely
    /// clearing the text so layout elements collapse cleanly.
    /// </summary>
    void Update()
    {
        if (timerLabel == null) return;

        GameManager gm = GameManager.Instance;
        if (gm != null && gm.GetCoinBonusTimeRemaining() > 0f)
        {
            float time = gm.GetCoinBonusTimeRemaining();
            timerLabel.text = $"x{gm.GetCoinBonusMultiplier()} {time:F1}s";
            if (!timerLabel.gameObject.activeSelf)
                timerLabel.gameObject.SetActive(true);
        }
        else if (timerLabel.gameObject.activeSelf)
        {
            timerLabel.gameObject.SetActive(false);
        }
    }
}
