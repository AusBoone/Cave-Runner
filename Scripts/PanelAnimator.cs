/*
 * PanelAnimator.cs
 *
 * Purpose:
 *   Provides a simple interface for playing show and hide animations on UI
 *   panels. The component activates the GameObject when shown and waits for a
 *   configurable delay before deactivating it when hidden.
 *
 * Usage Example:
 *   // Assume the Animator on this GameObject has "Show" and "Hide" triggers
 *   var panel = GetComponent<PanelAnimator>();
 *   panel.Show(); // plays the show animation and enables the panel
 *   panel.Hide(); // plays the hide animation then disables after a delay
 *
 * Assumptions:
 *   - The GameObject contains an Animator component with triggers named
 *     "Show" and "Hide".
 *   - hideDelay roughly matches the length of the hide animation.
 *
 */
using System.Collections;
using UnityEngine;

/// <summary>
/// Helper component to play show/hide animations when enabling UI panels.
/// </summary>
public class PanelAnimator : MonoBehaviour
{
    public Animator animator;
    public float hideDelay = 0.5f;
    
    // Reference to the currently running coroutine that handles delayed
    // deactivation. Storing it allows us to stop a previous routine if Hide()
    // is invoked again before the delay completes, preventing redundant
    // disable calls.
    private Coroutine hideRoutine;

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

            // If a previous hide coroutine is still running, stop it so we do
            // not schedule multiple deactivation routines. Without this guard,
            // repeated calls to Hide() could queue several DisableAfterDelay()
            // coroutines, each disabling the panel even if it has already been
            // reactivated in the meantime.
            if (hideRoutine != null)
            {
                StopCoroutine(hideRoutine);
            }

            // Start a new hide coroutine and remember it so it can be cancelled
            // if Hide() is invoked again.
            hideRoutine = StartCoroutine(DisableAfterDelay());
        }
        else
        {
            // No animator is available; immediately deactivate the panel.
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

        // After the delay, deactivate the panel. Clearing hideRoutine signals
        // that no hide coroutine is active, allowing future Hide() calls to
        // start a fresh one.
        gameObject.SetActive(false);
        hideRoutine = null;
    }
}
