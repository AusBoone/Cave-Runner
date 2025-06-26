using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Centralised helper for reading player input. This file was expanded to
/// support Unity's new Input System package while retaining the original
/// KeyCode-based approach. When <c>ENABLE_INPUT_SYSTEM</c> is defined, input is
/// read through <see cref="InputAction"/> instances that expose jump, slide and
/// pause actions with keyboard and gamepad bindings. If the package is not
/// present the manager falls back to legacy <see cref="KeyCode"/> queries.
/// Key bindings are saved to <see cref="PlayerPrefs"/>.
/// </summary>
public static class InputManager
{
    private const string JumpPref = "JumpKey";
    private const string SlidePref = "SlideKey";
    private const string PausePref = "PauseKey";
#if ENABLE_INPUT_SYSTEM
    private const string JumpBindingPref = "JumpBinding";
    private const string SlideBindingPref = "SlideBinding";
    private const string PauseBindingPref = "PauseBinding";

    // InputActions used when the new Input System package is present.
    private static InputAction jumpAction;
    private static InputAction slideAction;
    private static InputAction pauseAction;
#endif

    // Fallback KeyCodes used when the new Input System isn't available.
    public static KeyCode JumpKey { get; private set; }
    public static KeyCode SlideKey { get; private set; }
    public static KeyCode PauseKey { get; private set; }

    static InputManager()
    {
        // Load keys from prefs or use defaults
        JumpKey = LoadKey(JumpPref, KeyCode.Space);
        SlideKey = LoadKey(SlidePref, KeyCode.LeftControl);
        PauseKey = LoadKey(PausePref, KeyCode.Escape);

#if ENABLE_INPUT_SYSTEM
        // Setup actions so either keyboard or gamepad can trigger them.
        jumpAction = new InputAction("Jump", InputActionType.Button);
        jumpAction.AddBinding(PlayerPrefs.GetString(JumpBindingPref, "<Keyboard>/space"));
        jumpAction.AddBinding("<Gamepad>/buttonSouth");
        jumpAction.Enable();

        slideAction = new InputAction("Slide", InputActionType.Button);
        slideAction.AddBinding(PlayerPrefs.GetString(SlideBindingPref, "<Keyboard>/leftCtrl"));
        slideAction.AddBinding("<Gamepad>/buttonEast");
        slideAction.Enable();

        pauseAction = new InputAction("Pause", InputActionType.Button);
        pauseAction.AddBinding(PlayerPrefs.GetString(PauseBindingPref, "<Keyboard>/escape"));
        pauseAction.AddBinding("<Gamepad>/start");
        pauseAction.Enable();
#endif
    }

    /// <summary>
    /// Loads a key binding from PlayerPrefs and falls back to the provided
    /// default when the stored value cannot be parsed.
    /// </summary>
    private static KeyCode LoadKey(string pref, KeyCode defaultKey)
    {
        string saved = PlayerPrefs.GetString(pref, defaultKey.ToString());
        if (System.Enum.TryParse(saved, out KeyCode key))
        {
            return key;
        }
        return defaultKey;
    }

    /// <summary>
    /// Saves the provided key as the new jump binding.
    /// </summary>
    public static void SetJumpKey(KeyCode key)
    {
        JumpKey = key;
        PlayerPrefs.SetString(JumpPref, key.ToString());
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Saves the provided key as the new slide binding.
    /// </summary>
    public static void SetSlideKey(KeyCode key)
    {
        SlideKey = key;
        PlayerPrefs.SetString(SlidePref, key.ToString());
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Saves the provided key as the new pause binding.
    /// </summary>
    public static void SetPauseKey(KeyCode key)
    {
        PauseKey = key;
        PlayerPrefs.SetString(PausePref, key.ToString());
        PlayerPrefs.Save();
    }

#if ENABLE_INPUT_SYSTEM
    /// <summary>
    /// Begins an interactive rebinding operation for the jump action.
    /// The provided MonoBehaviour is used to start a coroutine so the
    /// operation can run asynchronously. The label is updated with the
    /// human readable binding string when complete.
    /// </summary>
    public static void StartRebindJump(MonoBehaviour owner, UnityEngine.UI.Text label)
    {
        owner.StartCoroutine(RebindRoutine(jumpAction, JumpBindingPref, label));
    }

    /// <summary>
    /// Begins an interactive rebinding operation for the slide action.
    /// </summary>
    public static void StartRebindSlide(MonoBehaviour owner, UnityEngine.UI.Text label)
    {
        owner.StartCoroutine(RebindRoutine(slideAction, SlideBindingPref, label));
    }

    /// <summary>
    /// Begins an interactive rebinding operation for the pause action.
    /// </summary>
    public static void StartRebindPause(MonoBehaviour owner, UnityEngine.UI.Text label)
    {
        owner.StartCoroutine(RebindRoutine(pauseAction, PauseBindingPref, label));
    }

    // Coroutine that waits for the user to press a new control and stores the result.
    private static System.Collections.IEnumerator RebindRoutine(InputAction action, string pref, UnityEngine.UI.Text label)
    {
        action.Disable();
        var operation = action.PerformInteractiveRebinding()
            .WithControlsExcluding("Mouse")
            .OnComplete(op =>
            {
                action.Enable();
                op.Dispose();
                string path = action.bindings[0].effectivePath;
                PlayerPrefs.SetString(pref, path);
                PlayerPrefs.Save();
                if (label != null)
                {
                    label.text = InputControlPath.ToHumanReadableString(path, InputControlPath.HumanReadableStringOptions.OmitDevice);
                }
            });

        while (!operation.completed)
            yield return null;
    }
#endif

    /// <summary>
    /// Returns true during the frame the jump input is pressed.
    /// </summary>
    public static bool GetJumpDown()
    {
#if ENABLE_INPUT_SYSTEM
        if (jumpAction != null)
        {
            return jumpAction.WasPressedThisFrame();
        }
#endif
        return Input.GetKeyDown(JumpKey);
    }

    /// <summary>
    /// Returns true while the jump input is held.
    /// </summary>
    public static bool GetJump()
    {
#if ENABLE_INPUT_SYSTEM
        if (jumpAction != null)
        {
            return jumpAction.IsPressed();
        }
#endif
        return Input.GetKey(JumpKey);
    }

    /// <summary>
    /// Returns true the frame the jump input is released.
    /// </summary>
    public static bool GetJumpUp()
    {
#if ENABLE_INPUT_SYSTEM
        if (jumpAction != null)
        {
            return jumpAction.WasReleasedThisFrame();
        }
#endif
        return Input.GetKeyUp(JumpKey);
    }

    /// <summary>
    /// Returns true during the frame the slide input is pressed.
    /// </summary>
    public static bool GetSlideDown()
    {
#if ENABLE_INPUT_SYSTEM
        if (slideAction != null)
        {
            return slideAction.WasPressedThisFrame();
        }
#endif
        return Input.GetKeyDown(SlideKey);
    }

    /// <summary>
    /// Returns true while the slide input is held.
    /// </summary>
    public static bool GetSlide()
    {
#if ENABLE_INPUT_SYSTEM
        if (slideAction != null)
        {
            return slideAction.IsPressed();
        }
#endif
        return Input.GetKey(SlideKey);
    }

    /// <summary>
    /// Returns true during the frame the pause input is pressed.
    /// </summary>
    public static bool GetPauseDown()
    {
#if ENABLE_INPUT_SYSTEM
        if (pauseAction != null)
        {
            return pauseAction.WasPressedThisFrame();
        }
#endif
        return Input.GetKeyDown(PauseKey);
    }
}
