using UnityEngine;

/// <summary>
/// Adjusts Renderer material colors based on ColorblindManager state.
/// </summary>
public class ColorblindMode : MonoBehaviour
{
    public Renderer[] targets;
    public Color normalColor = Color.white;
    public Color colorblindColor = Color.yellow;

    void Start()
    {
        ColorblindManager.OnModeChanged += Apply;
        Apply(ColorblindManager.Enabled);
    }

    void OnDestroy()
    {
        ColorblindManager.OnModeChanged -= Apply;
    }

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
