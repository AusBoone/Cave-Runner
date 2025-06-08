using System.Collections;
using UnityEngine;

/// <summary>
/// Helper component to play show/hide animations when enabling UI panels.
/// </summary>
public class PanelAnimator : MonoBehaviour
{
    public Animator animator;
    public float hideDelay = 0.5f;

    /// <summary>
    /// Caches the Animator reference if not assigned.
    /// </summary>
    void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();
    }

    /// <summary>
    /// Plays the show animation and activates the panel.
    /// </summary>
    public void Show()
    {
        gameObject.SetActive(true);
        animator?.SetTrigger("Show");
    }

    /// <summary>
    /// Plays the hide animation and deactivates after a delay.
    /// </summary>
    public void Hide()
    {
        if (animator != null)
        {
            animator.SetTrigger("Hide");
            StartCoroutine(DisableAfterDelay());
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Waits for <c>hideDelay</c> so the hide animation can finish before
    /// disabling the panel.
    /// </summary>
    private IEnumerator DisableAfterDelay()
    {
        yield return new WaitForSeconds(hideDelay);
        gameObject.SetActive(false);
    }
}
