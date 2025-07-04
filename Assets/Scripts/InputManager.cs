using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Centralised helper for reading player input. This file was expanded to
/// support Unity's new Input System package while retaining the original
/// KeyCode-based approach. When <c>ENABLE_INPUT_SYSTEM</c> is defined, input is
/// read through <see cref="InputAction"/> instances that expose jump, slide,
/// down and pause actions with keyboard and gamepad bindings. Horizontal movement
/// can also be queried through <see cref="GetHorizontal"/>. If the package is not
/// present the manager falls back to legacy <see cref="KeyCode"/> queries.
/// Key bindings are saved to <see cref="PlayerPrefs"/>.
/// </summary>
public static class InputManager
{
    private const string JumpPref = "JumpKey";
    private const string SlidePref = "SlideKey";
    private const string PausePref = "PauseKey";
    // New preference key used for the fast fall / down action.
    private const string DownPref = "DownKey";
    // Preference keys for custom left and right movement when using the
    // legacy input manager.
    private const string LeftPref = "LeftKey";
    private const string RightPref = "RightKey";
#if ENABLE_INPUT_SYSTEM
    private const string JumpBindingPref = "JumpBinding";
    private const string SlideBindingPref = "SlideBinding";
    private const string PauseBindingPref = "PauseBinding";
    // Binding preference for the down action when using the Input System.
    private const string DownBindingPref = "DownBinding";
    // Binding preferences for horizontal movement when using the Input System.
    private const string MoveLeftBindingPref = "MoveLeftBinding";
    private const string MoveRightBindingPref = "MoveRightBinding";

    // InputActions used when the new Input System package is present.
    private static InputAction jumpAction;
    private static InputAction slideAction;
    private static InputAction pauseAction;
    private static InputAction downAction;
    private static InputAction moveAction;
#endif

    // Joystick button codes for legacy input manager support. These arrays
    // cover common mappings on Xbox and PlayStation controllers so the game
    // works without the new Input System package installed.
    private static readonly KeyCode[] JumpButtons =
    {
        KeyCode.JoystickButton0, // A on Xbox, Square or Cross depending on driver
        KeyCode.JoystickButton1  // B on Xbox, Circle or Cross
    };
    private static readonly KeyCode[] SlideButtons =
    {
        KeyCode.JoystickButton1, // B or Circle
        KeyCode.JoystickButton2  // X or Square
    };
    private static readonly KeyCode[] PauseButtons =
    {
        KeyCode.JoystickButton7, // Start on Xbox
        KeyCode.JoystickButton9  // Options/Start on PlayStation
    };

    // Fallback KeyCodes used when the new Input System isn't available.
    public static KeyCode JumpKey { get; private set; }
    public static KeyCode SlideKey { get; private set; }
    public static KeyCode PauseKey { get; private set; }
    // KeyCode fallback used when the down input is queried without the
    // Input System package installed.
    public static KeyCode DownKey { get; private set; }
    // Keys used for horizontal movement when the legacy input manager is active.
    public static KeyCode MoveLeftKey { get; private set; }
    public static KeyCode MoveRightKey { get; private set; }

    static InputManager()
    {
        // Load keys from prefs or use defaults
        JumpKey = LoadKey(JumpPref, KeyCode.Space);
        SlideKey = LoadKey(SlidePref, KeyCode.LeftControl);
        PauseKey = LoadKey(PausePref, KeyCode.Escape);
        // Fast fall defaults to the "S" key when using legacy input.
        DownKey = LoadKey(DownPref, KeyCode.S);
        // WASD controls are enabled by default for horizontal movement.
        MoveLeftKey = LoadKey(LeftPref, KeyCode.A);
        MoveRightKey = LoadKey(RightPref, KeyCode.D);

#if ENABLE_INPUT_SYSTEM
        // Setup actions so either keyboard or various gamepads can trigger them.
        jumpAction = new InputAction("Jump", InputActionType.Button);
        jumpAction.AddBinding(PlayerPrefs.GetString(JumpBindingPref, "<Keyboard>/space"));
        // Generic path covers any gamepad type.
        jumpAction.AddBinding("<Gamepad>/buttonSouth");
        // Explicit bindings ensure PlayStation and Xbox controllers work when
        // the generic layout is not available.
        jumpAction.AddBinding("<DualShockGamepad>/cross");
        jumpAction.AddBinding("<DualSenseGamepad>/cross");
        jumpAction.AddBinding("<XInputController>/a");
        jumpAction.Enable();

        slideAction = new InputAction("Slide", InputActionType.Button);
        slideAction.AddBinding(PlayerPrefs.GetString(SlideBindingPref, "<Keyboard>/leftCtrl"));
        slideAction.AddBinding("<Gamepad>/buttonEast");
        slideAction.AddBinding("<DualShockGamepad>/circle");
        slideAction.AddBinding("<DualSenseGamepad>/circle");
        slideAction.AddBinding("<XInputController>/b");
        slideAction.Enable();

        downAction = new InputAction("Down", InputActionType.Button);
        downAction.AddBinding(PlayerPrefs.GetString(DownBindingPref, "<Keyboard>/s"));
        downAction.AddBinding("<Gamepad>/leftStick/down");
        downAction.AddBinding("<Gamepad>/dpad/down");
        downAction.AddBinding("<DualShockGamepad>/dpad/down");
        downAction.AddBinding("<DualSenseGamepad>/dpad/down");
        downAction.AddBinding("<XInputController>/dpad/down");
        downAction.Enable();

        pauseAction = new InputAction("Pause", InputActionType.Button);
        pauseAction.AddBinding(PlayerPrefs.GetString(PauseBindingPref, "<Keyboard>/escape"));
        pauseAction.AddBinding("<Gamepad>/start");
        pauseAction.AddBinding("<DualShockGamepad>/options");
        pauseAction.AddBinding("<DualSenseGamepad>/options");
        pauseAction.AddBinding("<XInputController>/start");
        pauseAction.Enable();

        // Axis for horizontal movement with configurable keyboard bindings.
        moveAction = new InputAction("Move", InputActionType.Value);
        var wasd = moveAction.AddCompositeBinding("1DAxis");
        wasd.With("Negative", PlayerPrefs.GetString(MoveLeftBindingPref, "<Keyboard>/a"));
        wasd.With("Positive", PlayerPrefs.GetString(MoveRightBindingPref, "<Keyboard>/d"));
        var arrows = moveAction.AddCompositeBinding("1DAxis");
        arrows.With("Negative", "<Keyboard>/leftArrow");
        arrows.With("Positive", "<Keyboard>/rightArrow");
        moveAction.AddBinding("<Gamepad>/leftStick/x");
        moveAction.AddBinding("<Gamepad>/dpad/x");
        moveAction.AddBinding("<DualShockGamepad>/dpad/x");
        moveAction.AddBinding("<DualSenseGamepad>/dpad/x");
        moveAction.AddBinding("<XInputController>/dpad/x");
        moveAction.Enable();
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
    /// Saves the provided key as the new down/fast fall binding.
    /// </summary>
    public static void SetDownKey(KeyCode key)
    {
        DownKey = key;
        PlayerPrefs.SetString(DownPref, key.ToString());
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Saves the provided key as the new left movement binding.
    /// </summary>
    public static void SetMoveLeftKey(KeyCode key)
    {
        MoveLeftKey = key;
        PlayerPrefs.SetString(LeftPref, key.ToString());
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Saves the provided key as the new right movement binding.
    /// </summary>
    public static void SetMoveRightKey(KeyCode key)
    {
        MoveRightKey = key;
        PlayerPrefs.SetString(RightPref, key.ToString());
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
    /// Begins an interactive rebinding operation for the down action.
    /// This input is used for fast falling.
    /// </summary>
    public static void StartRebindDown(MonoBehaviour owner, UnityEngine.UI.Text label)
    {
        owner.StartCoroutine(RebindRoutine(downAction, DownBindingPref, label));
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
        if (Input.GetKeyDown(JumpKey))
        {
            return true;
        }
        foreach (KeyCode code in JumpButtons)
        {
            if (Input.GetKeyDown(code))
            {
                return true;
            }
        }
        return false;
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
        if (Input.GetKey(JumpKey))
        {
            return true;
        }
        foreach (KeyCode code in JumpButtons)
        {
            if (Input.GetKey(code))
            {
                return true;
            }
        }
        return false;
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
        if (Input.GetKeyUp(JumpKey))
        {
            return true;
        }
        foreach (KeyCode code in JumpButtons)
        {
            if (Input.GetKeyUp(code))
            {
                return true;
            }
        }
        return false;
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
        if (Input.GetKeyDown(SlideKey))
        {
            return true;
        }
        foreach (KeyCode code in SlideButtons)
        {
            if (Input.GetKeyDown(code))
            {
                return true;
            }
        }
        return false;
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
        if (Input.GetKey(SlideKey))
        {
            return true;
        }
        foreach (KeyCode code in SlideButtons)
        {
            if (Input.GetKey(code))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Returns true during the frame the slide input is released. This is
    /// primarily used so <see cref="PlayerController"/> can cancel a slide
    /// early if the player lets go of the key.
    /// </summary>
    public static bool GetSlideUp()
    {
#if ENABLE_INPUT_SYSTEM
        if (slideAction != null)
        {
            return slideAction.WasReleasedThisFrame();
        }
#endif
        if (Input.GetKeyUp(SlideKey))
        {
            return true;
        }
        foreach (KeyCode code in SlideButtons)
        {
            if (Input.GetKeyUp(code))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Returns true while the down input is held. Used for fast falling
    /// when airborne.
    /// </summary>
    public static bool GetDown()
    {
#if ENABLE_INPUT_SYSTEM
        if (downAction != null)
        {
            return downAction.IsPressed();
        }
#endif
        // Check the configured key and fall back to the vertical axis which
        // reports negative values when a gamepad stick or dpad is pressed down.
        if (Input.GetKey(DownKey))
        {
            return true;
        }
        if (Input.GetAxisRaw("Vertical") < -0.5f)
        {
            return true;
        }
        return false;
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
        if (Input.GetKeyDown(PauseKey))
        {
            return true;
        }
        foreach (KeyCode code in PauseButtons)
        {
            if (Input.GetKeyDown(code))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Returns a horizontal input value in the range -1..1. Gamepad sticks or
    /// d-pads are used when available with a fallback to the legacy
    /// <c>Horizontal</c> axis. Negative values indicate left movement.
    /// </summary>
    public static float GetHorizontal()
    {
#if ENABLE_INPUT_SYSTEM
        if (moveAction != null)
        {
            float val = moveAction.ReadValue<float>();
            if (!Mathf.Approximately(val, 0f))
            {
                return val;
            }
        }
#endif
        // Combine custom key bindings with the default Horizontal axis for
        // joystick input when the new Input System is unavailable.
        float axis = 0f;
        if (Input.GetKey(MoveRightKey))
            axis += 1f;
        if (Input.GetKey(MoveLeftKey))
            axis -= 1f;
        if (!Mathf.Approximately(axis, 0f))
            return axis;
        return Input.GetAxisRaw("Horizontal");
    }
}
