using System.Collections;
using UnityEngine;

/// <summary>
/// Helper component to play show/hide animations when enabling UI panels.
/// </summary>
public class PanelAnimator : MonoBehaviour
{
    public Animator animator;
    public float hideDelay = 0.5f;

    void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();
    }

    public void Show()
    {
        gameObject.SetActive(true);
        animator?.SetTrigger("Show");
    }

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

    private IEnumerator DisableAfterDelay()
    {
        yield return new WaitForSeconds(hideDelay);
        gameObject.SetActive(false);
    }
}
