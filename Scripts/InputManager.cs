using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using System.Collections;
using TMPro; // TextMeshPro used for binding label updates

public static class InputManager
{
    private const string JumpPref = "JumpKey";
    private const string SlidePref = "SlideKey";
    private const string PausePref = "PauseKey";
    // Preference key storing whether vibration is enabled.
    private const string RumblePref = "RumbleEnabled";
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
    // Host MonoBehaviour used for running rumble coroutines.
    private static RumbleHost rumbleHost;
    // Reference to the currently running rumble coroutine so it can be stopped.
    private static Coroutine rumbleRoutine;
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

    // State flags updated by TouchInputManager when on-screen buttons
    // are used. These emulate keyboard/gamepad input so the existing
    // accessor methods work for mobile without changes elsewhere.
    private static bool touchJumpHeld;
    private static bool touchJumpDown;
    private static bool touchJumpUp;
    private static bool touchSlideHeld;
    private static bool touchSlideDown;
    private static bool touchSlideUp;
    private static bool touchPauseDown;

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
    // True when controller rumble is allowed. Controlled via SettingsMenu.
    public static bool RumbleEnabled { get; private set; }

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
        // Load rumble preference (enabled by default).
        RumbleEnabled = PlayerPrefs.GetInt(RumblePref, 1) == 1;
#if ENABLE_INPUT_SYSTEM
        // Setup actions so either keyboard or various gamepads can trigger them.
        jumpAction = new InputAction("Jump", InputActionType.Button);
        try
        {
            // Attempt to apply the player's saved jump binding. If it is
            // invalid, we fall back to the default path so input remains
            // responsive.
            jumpAction.AddBinding(PlayerPrefs.GetString(JumpBindingPref, "<Keyboard>/space"));
        }
        catch (System.Exception)
        {
            LoggingHelper.LogWarning($"Invalid binding for {JumpBindingPref}. Falling back to default '<Keyboard>/space'."); // Use central helper so warnings respect verbose flag.
            jumpAction.AddBinding("<Keyboard>/space");
        }
        // Generic path covers any gamepad type.
        jumpAction.AddBinding("<Gamepad>/buttonSouth");
        // Explicit bindings ensure PlayStation and Xbox controllers work when
        // the generic layout is not available.
        jumpAction.AddBinding("<DualShockGamepad>/cross");
        jumpAction.AddBinding("<DualSenseGamepad>/cross");
        jumpAction.AddBinding("<XInputController>/a");
        jumpAction.Enable();

        slideAction = new InputAction("Slide", InputActionType.Button);
        try
        {
            // Restore the player's slide binding; revert to the default when the
            // saved path cannot be parsed.
            slideAction.AddBinding(PlayerPrefs.GetString(SlideBindingPref, "<Keyboard>/leftCtrl"));
        }
        catch (System.Exception)
        {
            LoggingHelper.LogWarning($"Invalid binding for {SlideBindingPref}. Falling back to default '<Keyboard>/leftCtrl'."); // LoggingHelper ensures build-time gating.
            slideAction.AddBinding("<Keyboard>/leftCtrl");
        }
        slideAction.AddBinding("<Gamepad>/buttonEast");
        slideAction.AddBinding("<DualShockGamepad>/circle");
        slideAction.AddBinding("<DualSenseGamepad>/circle");
        slideAction.AddBinding("<XInputController>/b");
        slideAction.Enable();

        downAction = new InputAction("Down", InputActionType.Button);
        try
        {
            // Saved fast-fall binding may be corrupt; use the default if
            // AddBinding throws an exception.
            downAction.AddBinding(PlayerPrefs.GetString(DownBindingPref, "<Keyboard>/s"));
        }
        catch (System.Exception)
        {
            LoggingHelper.LogWarning($"Invalid binding for {DownBindingPref}. Falling back to default '<Keyboard>/s'."); // Centralised logging keeps warnings consistent.
            downAction.AddBinding("<Keyboard>/s");
        }
        downAction.AddBinding("<Gamepad>/leftStick/down");
        downAction.AddBinding("<Gamepad>/dpad/down");
        downAction.AddBinding("<DualShockGamepad>/dpad/down");
        downAction.AddBinding("<DualSenseGamepad>/dpad/down");
        downAction.AddBinding("<XInputController>/dpad/down");
        downAction.Enable();

        pauseAction = new InputAction("Pause", InputActionType.Button);
        try
        {
            // Pause binding also loads from PlayerPrefs; invalid values are
            // replaced with the Escape key.
            pauseAction.AddBinding(PlayerPrefs.GetString(PauseBindingPref, "<Keyboard>/escape"));
        }
        catch (System.Exception)
        {
            LoggingHelper.LogWarning($"Invalid binding for {PauseBindingPref}. Falling back to default '<Keyboard>/escape'."); // Use helper so logs honour verbose setting.
            pauseAction.AddBinding("<Keyboard>/escape");
        }
        pauseAction.AddBinding("<Gamepad>/start");
        pauseAction.AddBinding("<DualShockGamepad>/options");
        pauseAction.AddBinding("<DualSenseGamepad>/options");
        pauseAction.AddBinding("<XInputController>/start");
        pauseAction.Enable();

        // Axis for horizontal movement with configurable keyboard bindings.
        moveAction = new InputAction("Move", InputActionType.Value);
        var wasd = moveAction.AddCompositeBinding("1DAxis");
        try
        {
            // Load left movement binding; invalid entries revert to the default
            // "A" key so players are never left without movement input.
            wasd.With("Negative", PlayerPrefs.GetString(MoveLeftBindingPref, "<Keyboard>/a"));
        }
        catch (System.Exception)
        {
            LoggingHelper.LogWarning($"Invalid binding for {MoveLeftBindingPref}. Falling back to default '<Keyboard>/a'."); // Maintains unified logging pipeline.
            wasd.With("Negative", "<Keyboard>/a");
        }
        try
        {
            // Load right movement binding with similar fallback behaviour.
            wasd.With("Positive", PlayerPrefs.GetString(MoveRightBindingPref, "<Keyboard>/d"));
        }
        catch (System.Exception)
        {
            LoggingHelper.LogWarning($"Invalid binding for {MoveRightBindingPref}. Falling back to default '<Keyboard>/d'."); // Ensures gating is applied to warnings.
            wasd.With("Positive", "<Keyboard>/d");
        }
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

#if ENABLE_INPUT_SYSTEM
    /// <summary>
    /// Shuts down the input system integration by disabling and disposing all
    /// <see cref="InputAction"/> instances. The Input System allocates native
    /// resources for each action, so explicit disposal is required to prevent
    /// memory leaks when the game or editor exits.
    /// </summary>
    public static void Shutdown()
    {
        // Disposing each action releases its unmanaged memory and ensures it
        // no longer processes callbacks. Setting the fields to null afterwards
        // allows the garbage collector to reclaim the managed wrappers.
        DisposeAction(ref jumpAction);
        DisposeAction(ref slideAction);
        DisposeAction(ref pauseAction);
        DisposeAction(ref downAction);
        DisposeAction(ref moveAction);

        // If a rumble coroutine is active, stop it so the gamepad does not
        // continue vibrating after the input system shuts down. Stopping the
        // coroutine also prevents a dangling reference to the routine object.
        if (rumbleRoutine != null)
        {
            // The host may be missing during teardown. Guard against null to
            // avoid a <see cref="NullReferenceException"/> while still clearing
            // the routine reference.
            if (rumbleHost != null)
            {
                rumbleHost.StopCoroutine(rumbleRoutine);
            }
            rumbleRoutine = null;
        }

        // Explicitly reset motor speeds for **all** connected controllers so
        // none remain in a vibrating state after the game exits. Earlier
        // versions only silenced <see cref="Gamepad.current"/>, which could
        // leave secondary pads rumbling if multiple devices were attached.
        foreach (var pad in Gamepad.all)
        {
            // Setting both low- and high-frequency motors to zero instantly
            // stops vibration on the given device.
            pad.SetMotorSpeeds(0f, 0f);
        }
    }

    /// <summary>
    /// Helper that disables, disposes and clears a single action reference.
    /// </summary>
    /// <param name="action">Reference to the action to dispose.</param>
    private static void DisposeAction(ref InputAction action)
    {
        if (action == null)
            return;

        // Disable first to stop ongoing input processing.
        action.Disable();
        // Explicit disposal frees native buffers allocated by the Input System;
        // without it, those allocations would persist until a domain reload,
        // leading to steadily increasing memory usage.
        action.Dispose();
        action = null;
    }
#else
    /// <summary>
    /// Stubbed shutdown for builds that rely solely on the legacy input manager.
    /// </summary>
    public static void Shutdown() { }
#endif

    /// <summary>
    /// Retrieves a key binding from <see cref="PlayerPrefs"/> and returns the
    /// provided <paramref name="defaultKey"/> when the stored value is missing
    /// or invalid.
    /// </summary>
    /// <param name="pref">Preference key used to locate the saved binding.</param>
    /// <param name="defaultKey">Fallback key to use when parsing fails.</param>
    /// <returns>
    /// The parsed <see cref="KeyCode"/> if the preference contains a valid
    /// value; otherwise the supplied <paramref name="defaultKey"/>.
    /// </returns>
    /// <remarks>
    /// If parsing fails, the method logs a warning via
    /// <see cref="LoggingHelper.LogWarning"/> to aid in diagnosing corrupted or
    /// tampered preference data.
    /// </remarks>
    private static KeyCode LoadKey(string pref, KeyCode defaultKey)
    {
        // Obtain the saved string, defaulting to the provided key when no entry
        // exists. Using ToString here ensures consistency with how bindings are
        // saved by Set*Key helpers elsewhere in this class.
        string saved = PlayerPrefs.GetString(pref, defaultKey.ToString());

        // Attempt to convert the persisted string back into a KeyCode enum. The
        // Enum.TryParse call returns false when the data is corrupt or has been
        // manually edited to an unsupported value.
        if (System.Enum.TryParse(saved, out KeyCode key))
        {
            return key; // Successfully parsed; use the stored binding.
        }

        // Emit a warning so developers can spot the invalid preference during
        // testing. The message includes the preference key and invalid value for
        // easier debugging. The default key is returned to keep input usable.
        LoggingHelper.LogWarning(
            $"Invalid KeyCode '{saved}' for preference '{pref}'. Reverting to default '{defaultKey}'.");
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

    /// <summary>
    /// Enables or disables controller rumble feedback.
    /// </summary>
    public static void SetRumbleEnabled(bool enabled)
    {
        RumbleEnabled = enabled;
        PlayerPrefs.SetInt(RumblePref, enabled ? 1 : 0);
        PlayerPrefs.Save();
    }

    // The following methods are used by TouchInputManager so UI buttons can
    // mimic traditional input. Each method toggles internal flags that the
    // public accessors consume on the next query.

    /// <summary>
    /// Called when the on-screen jump button is pressed.
    /// </summary>
    public static void TouchJumpDown()
    {
        touchJumpHeld = true;
        touchJumpDown = true;
    }

    /// <summary>
    /// Called when the on-screen jump button is released.
    /// </summary>
    public static void TouchJumpUp()
    {
        touchJumpHeld = false;
        touchJumpUp = true;
    }

    /// <summary>
    /// Called when the on-screen slide button is pressed.
    /// </summary>
    public static void TouchSlideDown()
    {
        touchSlideHeld = true;
        touchSlideDown = true;
    }

    /// <summary>
    /// Called when the on-screen slide button is released.
    /// </summary>
    public static void TouchSlideUp()
    {
        touchSlideHeld = false;
        touchSlideUp = true;
    }

    /// <summary>
    /// Called when the on-screen pause button is tapped.
    /// </summary>
    public static void TouchPause()
    {
        touchPauseDown = true;
    }

#if ENABLE_INPUT_SYSTEM
    /// <summary>
    /// Begins an interactive rebinding operation for the jump action.
    /// The provided MonoBehaviour is used to start a coroutine so the
    /// operation can run asynchronously. The TMP_Text label is updated
    /// with the human readable binding string when complete.
    /// </summary>
    public static void StartRebindJump(MonoBehaviour owner, TMP_Text label)
    {
        owner.StartCoroutine(RebindRoutine(jumpAction, JumpBindingPref, label));
    }

    /// <summary>
    /// Begins an interactive rebinding operation for the slide action.
    /// </summary>
    public static void StartRebindSlide(MonoBehaviour owner, TMP_Text label)
    {
        owner.StartCoroutine(RebindRoutine(slideAction, SlideBindingPref, label));
    }

    /// <summary>
    /// Begins an interactive rebinding operation for the down action.
    /// This input is used for fast falling.
    /// </summary>
    public static void StartRebindDown(MonoBehaviour owner, TMP_Text label)
    {
        owner.StartCoroutine(RebindRoutine(downAction, DownBindingPref, label));
    }

    /// <summary>
    /// Begins an interactive rebinding operation for the pause action.
    /// </summary>
    public static void StartRebindPause(MonoBehaviour owner, TMP_Text label)
    {
        owner.StartCoroutine(RebindRoutine(pauseAction, PauseBindingPref, label));
    }

    // Coroutine that waits for the user to press a new control and stores the result.
    private static System.Collections.IEnumerator RebindRoutine(InputAction action, string pref, TMP_Text label)
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
        if (touchJumpDown)
        {
            touchJumpDown = false;
            return true;
        }
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
        if (touchJumpHeld)
        {
            return true;
        }
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
        if (touchJumpUp)
        {
            touchJumpUp = false;
            return true;
        }
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
        if (touchSlideDown)
        {
            touchSlideDown = false;
            return true;
        }
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
        if (touchSlideHeld)
        {
            return true;
        }
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
        if (touchSlideUp)
        {
            touchSlideUp = false;
            return true;
        }
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
        if (touchPauseDown)
        {
            touchPauseDown = false;
            return true;
        }
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

#if ENABLE_INPUT_SYSTEM
    /// <summary>
    /// Triggers controller rumble on a specific gamepad. When no device is
    /// supplied the system falls back to <see cref="Gamepad.current"/>. The
    /// vibration automatically stops after the supplied duration.
    /// </summary>
    /// <param name="strength">Strength from 0 to 1 for the vibration motors.</param>
    /// <param name="duration">Time in seconds the motors should run.</param>
    /// <param name="pad">Optional target controller. <c>null</c> routes the
    ///     rumble to <see cref="Gamepad.current"/>.</param>
    public static void TriggerRumble(float strength, float duration, Gamepad pad = null)
    {
        // Respect the player's preference: skip rumble entirely when disabled.
        if (!RumbleEnabled)
            return;

        // Resolve the target controller. When the caller passes null we default
        // to the system's currently active gamepad so callers can rely on the
        // original behaviour.
        pad ??= Gamepad.current;

        // Lazily create the coroutine host so hidden GameObjects are only
        // spawned if vibration is actually requested. InitRumbleHost performs
        // the same step at scene load for the current controller.
        EnsureRumbleHost(pad);

        // Abort if no compatible gamepad is connected or the host could not be
        // created (for example, in headless test environments).
        if (pad == null || rumbleHost == null)
            return;

        // Clamp parameters to safe ranges to avoid unexpected behaviour from
        // negative durations or out-of-range strengths.
        strength = Mathf.Clamp01(strength);
        duration = Mathf.Max(0f, duration);

        // If the caller requested a non-positive duration after clamping, there
        // is nothing to do. Exiting early avoids creating a coroutine that would
        // immediately end, keeping runtime overhead low and preventing subtle
        // bugs from zero-length rumble requests.
        if (duration <= 0f)
            return;

        // Stop any existing rumble so motor control doesn't overlap between
        // multiple requests.
        if (rumbleRoutine != null)
            rumbleHost.StopCoroutine(rumbleRoutine);

        // Begin the rumble coroutine which automatically resets after the
        // specified realtime duration.
        rumbleRoutine = rumbleHost.StartCoroutine(RumbleRoutine(pad, strength, duration));
    }

    /// <summary>
    /// Wrapper invoked automatically by Unity after each scene load. The
    /// runtime initialization attribute requires a parameterless method, so this
    /// helper simply forwards to <see cref="EnsureRumbleHost(Gamepad)"/> using
    /// the currently active controller.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InitRumbleHost()
    {
        // Delegate to the core creation routine. If no controller is connected,
        // the host is intentionally not created.
        EnsureRumbleHost(Gamepad.current);
    }

    /// <summary>
    /// Ensures a persistent <see cref="RumbleHost"/> exists to run rumble
    /// coroutines. Called by <see cref="InitRumbleHost"/> after each scene load
    /// and by <see cref="TriggerRumble"/> when rumble is requested. The host is
    /// created on demand only when a gamepad is connected, preventing unused
    /// hidden objects in scenes that never request rumble.
    ///
    /// The host's lifecycle is intentionally lightweight: if a previous
    /// <see cref="RumbleHost"/> instance survives a domain reload it will be
    /// reused, avoiding duplicated hidden objects. The component persists across
    /// scene loads via <see cref="Object.DontDestroyOnLoad"/> and cleans itself
    /// up when <c>Application.quitting</c> fires, destroying the underlying game
    /// object and clearing the static reference so fresh hosts can be spawned in
    /// subsequent sessions.
    /// </summary>
    /// <param name="pad">Controller that will receive rumble. A host is only
    /// created when this parameter is non-null.</param>
    private static void EnsureRumbleHost(Gamepad pad)
    {
        // Skip if a host already exists or the supplied controller is missing.
        if (rumbleHost != null || pad == null)
            return;

        // Reuse a surviving host from a previous play session when possible so
        // domain reloads do not accumulate additional objects.
        var existing = Object.FindObjectOfType<RumbleHost>();
        if (existing != null)
        {
            rumbleHost = existing;
            return;
        }

        // Otherwise create a new hidden object dedicated to running rumble
        // coroutines. HideAndDontSave ensures the object persists across scenes
        // but remains invisible in the hierarchy until <c>Application.quitting</c>
        // destroys it.
        var hostObj = new GameObject("InputManagerRumbleHost");
        hostObj.hideFlags = HideFlags.HideAndDontSave;
        Object.DontDestroyOnLoad(hostObj);
        rumbleHost = hostObj.AddComponent<RumbleHost>();
    }

    // Coroutine that applies rumble then resets the motor speeds on the
    // specified controller. The routine verifies the pad remains valid before
    // each motor adjustment, allowing it to abort safely if the device
    // disconnects mid-rumble.
    private static IEnumerator RumbleRoutine(Gamepad pad, float strength, float duration)
    {
        // Guard against missing controllers. If the pad reference is null at the
        // start, exit immediately so SetMotorSpeeds is never invoked on a
        // destroyed device, preventing a NullReferenceException.
        if (pad == null)
        {
            rumbleRoutine = null;
            yield break;
        }

        pad.SetMotorSpeeds(strength, strength);
        // Wait in realtime so pausing the game doesn't prolong vibration.
        yield return new WaitForSecondsRealtime(duration);

        // The controller may have been disconnected while waiting. Verify the
        // reference is still valid before attempting to stop rumble.
        if (pad == null)
        {
            rumbleRoutine = null;
            yield break;
        }

        pad.SetMotorSpeeds(0f, 0f);
        // Mark the routine as finished so another rumble can start.
        rumbleRoutine = null;
    }

    // Lightweight MonoBehaviour used solely to run coroutines for rumble.
    private class RumbleHost : MonoBehaviour
    {
        // Destroy the host when the application quits so a fresh instance is
        // created on the next domain reload instead of accumulating objects.
        void OnApplicationQuit()
        {
            // Proactively release InputAction resources to avoid memory leaks
            // from lingering unmanaged allocations retained by the Input System.
            Shutdown();
            Destroy(gameObject);
            // Clear the static host reference so subsequent rumble requests
            // can detect that the host no longer exists and avoid
            // dereferencing a destroyed component.
            rumbleHost = null;
        }
    }
#else
    /// <summary>
    /// Legacy-input stub that ignores rumble requests when the new Input System
    /// package is unavailable. The parameters mirror the full implementation so
    /// calling code does not require conditional compilation.
    /// </summary>
    /// <param name="strength">Requested rumble strength in the range [0,1].</param>
    /// <param name="duration">Requested vibration duration in seconds.</param>
    public static void TriggerRumble(float strength, float duration)
    {
        // Rumble is unsupported without the Input System; this method intentionally
        // performs no action to maintain compatibility on legacy builds.
    }
#endif
}
