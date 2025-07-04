// CoinBonusIndicator.cs
// -----------------------------------------------------------------------------
// UI component that displays the remaining duration of the coin bonus effect
// on screen. The text element is hidden whenever no bonus is active. Designed
// to be attached to a UI GameObject with a Text child.
// -----------------------------------------------------------------------------

using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Updates a UI <see cref="Text"/> to show the current coin bonus multiplier
/// and countdown. The label is automatically hidden when the bonus expires.
/// </summary>
public class CoinBonusIndicator : MonoBehaviour
{
    [Tooltip("UI text displaying multiplier and remaining seconds.")]
    public Text timerLabel;

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
