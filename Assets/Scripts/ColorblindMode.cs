/*
 * ColorblindMode.cs
 * -----------------------------------------------------------------------------
 * Behaviour that tints assigned Renderer materials based on the global
 * ColorblindManager setting. Typical usage attaches this script to a GameObject
 * with one or more Renderer components. The component listens for mode changes
 * via ColorblindManager.OnModeChanged and updates the material colours so
 * accessibility settings take effect immediately.
 * -----------------------------------------------------------------------------
 */
using UnityEngine;

/// <summary>
/// Adjusts Renderer material colors based on ColorblindManager state.
/// </summary>
public class ColorblindMode : MonoBehaviour
{
    /// <summary>
    /// Renderer components whose materials will be tinted by this behaviour.
    /// </summary>
    public Renderer[] targets;
    /// <summary>
    /// Default colour applied when colorblind mode is disabled.
    /// </summary>
    public Color normalColor = Color.white;
    /// <summary>
    /// Alternate colour used while colorblind mode is enabled.
    /// </summary>
    public Color colorblindColor = Color.yellow;

    /// <summary>
    /// Applies the current colorblind setting and registers for changes.
    /// </summary>
    void Start()
    {
        ColorblindManager.OnModeChanged += Apply;
        Apply(ColorblindManager.Enabled);
    }

    /// <summary>
    /// Unsubscribes from colorblind events when destroyed.
    /// </summary>
    void OnDestroy()
    {
        ColorblindManager.OnModeChanged -= Apply;
    }

    /// <summary>
    /// Applies the configured colour scheme to all target renderers. Invoked
    /// at startup and whenever the global colorblind preference changes.
    /// </summary>
    private void Apply(bool enabled)
    {
        if (targets == null) return;
        foreach (var r in targets)
        {
            if (r != null)
            {
                r.material.color = enabled ? colorblindColor : normalColor;
            }
        }
    }
}
