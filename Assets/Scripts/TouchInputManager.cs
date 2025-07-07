using UnityEngine;

/// <summary>
/// MonoBehaviour that relays UI button events to <see cref="InputManager"/> so
/// mobile devices can control the player with on-screen buttons. Attach this
/// component to a Canvas containing buttons and wire up the public methods
/// through the Button OnClick/OnPointer events.
/// </summary>
public class TouchInputManager : MonoBehaviour
{
    /// <summary>Invoked by the jump button's PointerDown event.</summary>
    public void OnJumpDown() => InputManager.TouchJumpDown();

    /// <summary>Invoked by the jump button's PointerUp event.</summary>
    public void OnJumpUp() => InputManager.TouchJumpUp();

    /// <summary>Invoked by the slide button's PointerDown event.</summary>
    public void OnSlideDown() => InputManager.TouchSlideDown();

    /// <summary>Invoked by the slide button's PointerUp event.</summary>
    public void OnSlideUp() => InputManager.TouchSlideUp();

    /// <summary>Invoked by the pause button's click event.</summary>
    public void OnPause() => InputManager.TouchPause();
}
