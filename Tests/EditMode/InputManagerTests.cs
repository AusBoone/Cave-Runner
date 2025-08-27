#if ENABLE_INPUT_SYSTEM
using NUnit.Framework;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using System.Collections;

public class InputManagerTests
{
    [Test]
    public void JumpAction_IncludesConsoleBindings()
    {
        // Touch a method so the static constructor runs and actions are created.
        InputManager.GetJumpDown();

        // Access the private jumpAction field via reflection.
        FieldInfo field = typeof(InputManager).GetField("jumpAction", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(field, "jumpAction field missing");
        InputAction action = (InputAction)field.GetValue(null);

        bool dsFound = false;
        bool xbFound = false;
        foreach (var binding in action.bindings)
        {
            if (binding.effectivePath.Contains("DualShockGamepad") || binding.effectivePath.Contains("DualSenseGamepad"))
            {
                dsFound = true;
            }
            if (binding.effectivePath.Contains("XInputController"))
            {
                xbFound = true;
            }
        }
        Assert.IsTrue(dsFound && xbFound, "Expected bindings for DualShock/DualSense and XInput controllers");
    }

    [Test]
    public void MoveAction_HasKeyboardComposite()
    {
        // Force static constructor
        InputManager.GetHorizontal();

        FieldInfo field = typeof(InputManager).GetField("moveAction", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(field, "moveAction field missing");
        InputAction action = (InputAction)field.GetValue(null);

        bool hasComposite = false;
        foreach (var binding in action.bindings)
        {
            if (binding.isComposite && binding.effectivePath == "1DAxis")
            {
                hasComposite = true;
                break;
            }
        }

        Assert.IsTrue(hasComposite, "Move action should use a 1DAxis composite for keyboard input");
    }

    /// <summary>
    /// Corrupted binding strings in PlayerPrefs should not break input
    /// initialization. Each case seeds an invalid path and verifies that the
    /// static constructor logs a warning and falls back to the documented
    /// default binding.
    /// </summary>
    [TestCase("JumpBinding", "<Keyboard>/space", "jumpAction", 0)]
    [TestCase("SlideBinding", "<Keyboard>/leftCtrl", "slideAction", 0)]
    [TestCase("DownBinding", "<Keyboard>/s", "downAction", 0)]
    [TestCase("PauseBinding", "<Keyboard>/escape", "pauseAction", 0)]
    [TestCase("MoveLeftBinding", "<Keyboard>/a", "moveAction", 1)]
    [TestCase("MoveRightBinding", "<Keyboard>/d", "moveAction", 2)]
    public void InvalidSavedBinding_FallsBackToDefault(string prefKey, string defaultPath, string actionField, int bindingIndex)
    {
        // Seed an invalid binding to simulate corrupt or tampered PlayerPrefs.
        PlayerPrefs.SetString(prefKey, "invalid_path");

        // Ensure a clean slate before forcing the static constructor to run.
        InputManager.Shutdown();

        // The constructor should warn about the invalid binding and use the
        // default path so the game remains controllable.
        LogAssert.Expect(LogType.Warning, $"Invalid binding for {prefKey}. Falling back to default '{defaultPath}'.");
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(InputManager).TypeHandle);

        FieldInfo field = typeof(InputManager).GetField(actionField, BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(field, $"{actionField} field missing");
        InputAction action = (InputAction)field.GetValue(null);
        Assert.AreEqual(defaultPath, action.bindings[bindingIndex].path, "Expected fallback to default binding");

        // Clean up preferences and action state for subsequent tests.
        InputManager.Shutdown();
        PlayerPrefs.DeleteKey(prefKey);
    }

    /// <summary>
    /// Calling <see cref="InputManager.Shutdown"/> should disable and dispose all
    /// actions so they no longer consume native resources. The static action
    /// fields are cleared to allow garbage collection.
    /// </summary>
    [Test]
    public void Shutdown_DisposesActions()
    {
        // Initialize actions via any accessor.
        InputManager.GetJumpDown();

        // Cache action references before shutdown for state verification.
        FieldInfo jumpField = typeof(InputManager).GetField("jumpAction", BindingFlags.NonPublic | BindingFlags.Static);
        FieldInfo slideField = typeof(InputManager).GetField("slideAction", BindingFlags.NonPublic | BindingFlags.Static);
        FieldInfo pauseField = typeof(InputManager).GetField("pauseAction", BindingFlags.NonPublic | BindingFlags.Static);
        FieldInfo downField = typeof(InputManager).GetField("downAction", BindingFlags.NonPublic | BindingFlags.Static);
        FieldInfo moveField = typeof(InputManager).GetField("moveAction", BindingFlags.NonPublic | BindingFlags.Static);

        InputAction jump = (InputAction)jumpField.GetValue(null);
        InputAction slide = (InputAction)slideField.GetValue(null);
        InputAction pause = (InputAction)pauseField.GetValue(null);
        InputAction down = (InputAction)downField.GetValue(null);
        InputAction move = (InputAction)moveField.GetValue(null);

        // Sanity check that actions are enabled prior to shutdown.
        Assert.IsTrue(jump.enabled, "Jump action should start enabled");
        Assert.IsTrue(slide.enabled, "Slide action should start enabled");
        Assert.IsTrue(pause.enabled, "Pause action should start enabled");
        Assert.IsTrue(down.enabled, "Down action should start enabled");
        Assert.IsTrue(move.enabled, "Move action should start enabled");

        // Invoke shutdown and verify actions are released.
        InputManager.Shutdown();

        Assert.IsFalse(jump.enabled, "Jump action should be disabled after shutdown");
        Assert.IsFalse(slide.enabled, "Slide action should be disabled after shutdown");
        Assert.IsFalse(pause.enabled, "Pause action should be disabled after shutdown");
        Assert.IsFalse(down.enabled, "Down action should be disabled after shutdown");
        Assert.IsFalse(move.enabled, "Move action should be disabled after shutdown");

        Assert.IsNull(jumpField.GetValue(null), "Jump action reference should be cleared");
        Assert.IsNull(slideField.GetValue(null), "Slide action reference should be cleared");
        Assert.IsNull(pauseField.GetValue(null), "Pause action reference should be cleared");
        Assert.IsNull(downField.GetValue(null), "Down action reference should be cleared");
        Assert.IsNull(moveField.GetValue(null), "Move action reference should be cleared");

        // Reinitialize InputManager so subsequent tests have fresh actions.
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(InputManager).TypeHandle);
    }

    /// <summary>
    /// Destroying the primary <see cref="GameManager"/> should automatically
    /// invoke <see cref="InputManager.Shutdown"/>. This ensures that native
    /// <see cref="UnityEngine.InputSystem.InputAction"/> resources are released
    /// during application teardown without requiring manual calls.
    /// </summary>
    [Test]
    public void GameManagerDestroy_TriggersInputShutdown()
    {
        // Initialize actions so there is something for Shutdown to release.
        InputManager.GetJumpDown();

        FieldInfo jumpField = typeof(InputManager).GetField(
            "jumpAction", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(jumpField.GetValue(null),
            "jumpAction should exist before the GameManager is destroyed");

        // Create and immediately destroy a GameManager to simulate application
        // teardown. The OnDestroy handler should call InputManager.Shutdown.
        var gmObj = new GameObject();
        gmObj.AddComponent<GameManager>();
        Object.DestroyImmediate(gmObj);

        // InputManager.Shutdown should have cleared the action reference.
        Assert.IsNull(jumpField.GetValue(null),
            "GameManager destruction should shut down InputManager");

        // Clean up helper singletons created by GameManager so other tests start
        // with a predictable environment.
        if (SaveGameManager.Instance != null)
            Object.DestroyImmediate(SaveGameManager.Instance.gameObject);
        if (AnalyticsManager.Instance != null)
            Object.DestroyImmediate(AnalyticsManager.Instance.gameObject);

        // Reinitialize InputManager for subsequent tests.
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(
            typeof(InputManager).TypeHandle);
    }

    [Test]
    public void TriggerRumble_StartsCoroutine()
    {
        var gamepad = InputSystem.AddDevice<Gamepad>();
        InputManager.SetRumbleEnabled(true);

        InputManager.TriggerRumble(0.5f, 0.01f);

        FieldInfo field = typeof(InputManager).GetField("rumbleRoutine", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(field, "rumbleRoutine field missing");
        Assert.IsNotNull(field.GetValue(null), "TriggerRumble should start a coroutine when a gamepad is present");

        // Clean up so subsequent tests start with a fresh InputManager state.
        InputManager.Shutdown();
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(
            typeof(InputManager).TypeHandle);
        InputSystem.RemoveDevice(gamepad);
    }

    /// <summary>
    /// Requesting rumble with a zero second duration should be ignored. This
    /// guards against accidental calls that would otherwise start and stop a
    /// coroutine immediately, wasting CPU time.
    /// </summary>
    [Test]
    public void TriggerRumble_ZeroDuration_DoesNotStartCoroutine()
    {
        var gamepad = InputSystem.AddDevice<Gamepad>();
        InputManager.SetRumbleEnabled(true);

        // Duration is zero; rumble should be skipped entirely.
        InputManager.TriggerRumble(0.5f, 0f);

        FieldInfo field = typeof(InputManager).GetField("rumbleRoutine", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNull(field.GetValue(null),
            "Rumble coroutine should not start for zero duration requests");

        // Reset InputManager so later tests are not affected by this call.
        InputManager.Shutdown();
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(
            typeof(InputManager).TypeHandle);
        InputSystem.RemoveDevice(gamepad);
    }

    /// <summary>
    /// Negative durations are clamped to zero and should likewise result in no
    /// rumble. This ensures callers cannot accidentally schedule invalid
    /// vibration requests.
    /// </summary>
    [Test]
    public void TriggerRumble_NegativeDuration_DoesNotStartCoroutine()
    {
        var gamepad = InputSystem.AddDevice<Gamepad>();
        InputManager.SetRumbleEnabled(true);

        // Negative duration is clamped to zero; rumble should not begin.
        InputManager.TriggerRumble(0.5f, -1f);

        FieldInfo field = typeof(InputManager).GetField("rumbleRoutine", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNull(field.GetValue(null),
            "Rumble coroutine should not start for negative duration requests");

        // Reset InputManager so later tests run in a clean environment.
        InputManager.Shutdown();
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(
            typeof(InputManager).TypeHandle);
        InputSystem.RemoveDevice(gamepad);
    }

    /// <summary>
    /// Rumble should end even when Time.timeScale is zero. WaitForSecondsRealtime
    /// ensures the coroutine finishes while the game is paused.
    /// </summary>
    [UnityTest]
    public IEnumerator TriggerRumble_StopsWhilePaused()
    {
        var gamepad = InputSystem.AddDevice<Gamepad>();
        InputManager.SetRumbleEnabled(true);

        Time.timeScale = 0f;
        InputManager.TriggerRumble(0.5f, 0.01f);

        FieldInfo field = typeof(InputManager).GetField("rumbleRoutine", BindingFlags.NonPublic | BindingFlags.Static);
        while (field.GetValue(null) != null)
            yield return null;

        Assert.IsNull(field.GetValue(null), "Coroutine should complete even when paused");
        Time.timeScale = 1f;
        InputSystem.RemoveDevice(gamepad);
    }

    /// <summary>
    /// If the gamepad disconnects while a rumble coroutine is waiting, the
    /// routine should terminate without throwing and clear its reference so
    /// subsequent rumbles can start safely.
    /// </summary>
    [UnityTest]
    public IEnumerator RumbleRoutine_StopsOnGamepadDisconnect()
    {
        var pad = InputSystem.AddDevice<Gamepad>();
        InputManager.SetRumbleEnabled(true);

        // Start a rumble long enough that we can simulate a disconnect before it
        // completes naturally.
        InputManager.TriggerRumble(0.5f, 0.1f);

        FieldInfo field = typeof(InputManager).GetField(
            "rumbleRoutine", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(field.GetValue(null),
            "Rumble routine should be active before disconnect");

        // Remove the device to mimic an unexpected unplug.
        InputSystem.RemoveDevice(pad);

        // Wait a few frames for the coroutine to observe the missing pad and
        // exit. The loop guards against an infinite wait if the routine fails to
        // terminate.
        for (int i = 0; i < 10 && field.GetValue(null) != null; i++)
            yield return null;

        Assert.IsNull(field.GetValue(null),
            "Rumble routine should stop when the gamepad disconnects");

        // Reset InputManager so later tests start with a clean environment.
        InputManager.Shutdown();
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(
            typeof(InputManager).TypeHandle);
    }

    /// <summary>
    /// Calling Shutdown while a rumble coroutine is active should stop the
    /// vibration and clear the routine reference so subsequent rumbles can
    /// start cleanly.
    /// </summary>
    [Test]
    public void Shutdown_StopsActiveRumbleAndResetsMotors()
    {
        var gamepad = InputSystem.AddDevice<Gamepad>();
        InputManager.SetRumbleEnabled(true);

        // Start a rumble with a long duration so it would normally persist.
        InputManager.TriggerRumble(1f, 10f);

        FieldInfo routineField = typeof(InputManager).GetField(
            "rumbleRoutine", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(routineField.GetValue(null),
            "Rumble routine should be active before shutdown");

        InputManager.Shutdown();

        // The routine should be cleared and motors reset to zero.
        Assert.IsNull(routineField.GetValue(null),
            "Shutdown should clear the active rumble routine");
        float low, high;
        gamepad.GetMotorSpeeds(out low, out high);
        Assert.AreEqual(0f, low, 0.0001f, "Low-frequency motor should be zero after shutdown");
        Assert.AreEqual(0f, high, 0.0001f, "High-frequency motor should be zero after shutdown");

        // Reinitialize for subsequent tests since Shutdown disposed the actions.
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(
            typeof(InputManager).TypeHandle);
        InputSystem.RemoveDevice(gamepad);
    }

    /// <summary>
    /// When multiple controllers are connected, <see cref="InputManager.Shutdown"/>
    /// should clear rumble on every pad. This test manually sets motor speeds on
    /// two devices and verifies they are all reset, preventing leftover
    /// vibration on secondary controllers.
    /// </summary>
    [Test]
    public void Shutdown_ResetsAllConnectedGamepads()
    {
        // Add two gamepads to mimic a multiplayer setup with multiple
        // controllers attached simultaneously.
        var padOne = InputSystem.AddDevice<Gamepad>();
        var padTwo = InputSystem.AddDevice<Gamepad>();

        // Give both pads non-zero motor speeds to simulate an ongoing rumble
        // effect that should be cancelled during shutdown.
        padOne.SetMotorSpeeds(0.2f, 0.3f);
        padTwo.SetMotorSpeeds(0.4f, 0.5f);

        // Invoke shutdown; the new implementation iterates <see cref="Gamepad.all"/>
        // so every connected device should have its motors reset.
        InputManager.Shutdown();

        float low, high;
        padOne.GetMotorSpeeds(out low, out high);
        Assert.AreEqual(0f, low, 0.0001f,
            "Pad one low-frequency motor should be zero after shutdown");
        Assert.AreEqual(0f, high, 0.0001f,
            "Pad one high-frequency motor should be zero after shutdown");
        padTwo.GetMotorSpeeds(out low, out high);
        Assert.AreEqual(0f, low, 0.0001f,
            "Pad two low-frequency motor should be zero after shutdown");
        Assert.AreEqual(0f, high, 0.0001f,
            "Pad two high-frequency motor should be zero after shutdown");

        // Reinitialize the InputManager for subsequent tests and remove the
        // temporary devices from the Input System.
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(
            typeof(InputManager).TypeHandle);
        InputSystem.RemoveDevice(padOne);
        InputSystem.RemoveDevice(padTwo);
    }

    /// <summary>
    /// Supplying a specific <see cref="Gamepad"/> to
    /// <see cref="InputManager.TriggerRumble"/> should vibrate only that device,
    /// leaving others untouched. This ensures multiplayer setups can direct
    /// haptics to the appropriate player.
    /// </summary>
    [Test]
    public void TriggerRumble_TargetsSpecifiedGamepad()
    {
        // Create two controllers to mimic multiple players.
        var padOne = InputSystem.AddDevice<Gamepad>();
        var padTwo = InputSystem.AddDevice<Gamepad>();
        InputManager.SetRumbleEnabled(true);

        // Request rumble on the second pad only.
        InputManager.TriggerRumble(0.1f, 0.01f, padTwo);

        // First pad should remain idle because it was not targeted.
        float low, high;
        padOne.GetMotorSpeeds(out low, out high);
        Assert.AreEqual(0f, low, 0.0001f,
            "Pad one should not rumble when another pad is specified");

        // Second pad should receive the vibration request.
        padTwo.GetMotorSpeeds(out low, out high);
        Assert.Greater(low, 0f,
            "Pad two should rumble when passed to TriggerRumble");

        // Clean up devices and reset InputManager for later tests.
        InputManager.Shutdown();
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(
            typeof(InputManager).TypeHandle);
        InputSystem.RemoveDevice(padOne);
        InputSystem.RemoveDevice(padTwo);
    }

    /// <summary>
    /// The rumble host should be created only when rumble is actually requested,
    /// keeping the scene free of hidden objects in projects that never vibrate.
    /// </summary>
    [Test]
    public void RumbleHostCreated_OnDemand()
    {
        // Remove any existing host and reset the manager so the test starts
        // from a clean state without hidden objects.
        var existingHost = GameObject.Find("InputManagerRumbleHost");
        if (existingHost != null)
            Object.DestroyImmediate(existingHost);
        InputManager.Shutdown();
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(
            typeof(InputManager).TypeHandle);

        // Static initialization alone should not create the host.
        Assert.IsNull(GameObject.Find("InputManagerRumbleHost"),
            "Rumble host should not exist before any rumble requests");

        // Provide a gamepad and request rumble; this should spawn the host.
        var pad = InputSystem.AddDevice<Gamepad>();
        InputManager.SetRumbleEnabled(true);
        InputManager.TriggerRumble(0.1f, 0.01f);

        Assert.IsNotNull(GameObject.Find("InputManagerRumbleHost"),
            "Rumble host should be created when rumble is triggered");

        InputSystem.RemoveDevice(pad);
    }

    /// <summary>
    /// When a gamepad is present at scene load time the attributed
    /// <see cref="InputManager"/> initializer should automatically spawn the
    /// rumble host so vibration is available without an explicit rumble request.
    /// This test invokes the initializer via reflection to mimic Unity's
    /// runtime behaviour and confirms the host object exists afterward.
    /// </summary>
    [Test]
    public void RumbleHostCreated_AfterSceneLoad()
    {
        // Start from a clean slate: remove any lingering host and reset the
        // InputManager state so the initializer runs as it would in a new
        // session.
        var existingHost = GameObject.Find("InputManagerRumbleHost");
        if (existingHost != null)
            Object.DestroyImmediate(existingHost);
        InputManager.Shutdown();
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(
            typeof(InputManager).TypeHandle);

        // Provide a controller so the initialization routine has a device to
        // attach the host to.
        var pad = InputSystem.AddDevice<Gamepad>();

        // InitRumbleHost is private and normally invoked by Unity after scene
        // load. Reflection allows the test to simulate that callback.
        var method = typeof(InputManager).GetMethod(
            "InitRumbleHost", BindingFlags.NonPublic | BindingFlags.Static);
        method.Invoke(null, null);

        Assert.IsNotNull(GameObject.Find("InputManagerRumbleHost"),
            "Rumble host should exist after InitRumbleHost is invoked");

        InputSystem.RemoveDevice(pad);
    }

    /// <summary>
    /// Repeated rumble requests should reuse the same hidden host rather than
    /// spawning additional GameObjects, keeping the scene clean even if rumble is
    /// triggered many times during play.
    /// </summary>
    [Test]
    public void RumbleHost_ReusedBetweenRequests()
    {
        // Ensure no host lingers from previous tests so creation logic runs.
        var existingHost = GameObject.Find("InputManagerRumbleHost");
        if (existingHost != null)
            Object.DestroyImmediate(existingHost);
        InputManager.Shutdown();
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(
            typeof(InputManager).TypeHandle);

        var pad = InputSystem.AddDevice<Gamepad>();
        InputManager.SetRumbleEnabled(true);

        // First rumble spawns the host. Capture the reference via reflection so
        // we can compare it after a second request.
        InputManager.TriggerRumble(0.1f, 0.01f);
        FieldInfo hostField = typeof(InputManager).GetField(
            "rumbleHost", BindingFlags.NonPublic | BindingFlags.Static);
        var firstHost = (Object)hostField.GetValue(null);

        // A second rumble should not create a new host; the reference should be
        // unchanged.
        InputManager.TriggerRumble(0.1f, 0.01f);
        var secondHost = (Object)hostField.GetValue(null);
        Assert.AreSame(firstHost, secondHost, "Rumble host should be reused");

        // Clean up so later tests start from a known state.
        InputManager.Shutdown();
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(
            typeof(InputManager).TypeHandle);
        InputSystem.RemoveDevice(pad);
    }

    /// <summary>
    /// When the application is quitting the <see cref="RumbleHost"/> should
    /// destroy itself and clear the static reference so future rumble requests
    /// can recreate a fresh host without referencing a destroyed component.
    /// </summary>
    [Test]
    public void RumbleHostCleared_OnApplicationQuit()
    {
        // Start with a clean environment and spawn a host via a rumble request.
        var lingering = GameObject.Find("InputManagerRumbleHost");
        if (lingering != null)
            Object.DestroyImmediate(lingering);
        InputManager.Shutdown();
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(
            typeof(InputManager).TypeHandle);

        var pad = InputSystem.AddDevice<Gamepad>();
        InputManager.SetRumbleEnabled(true);
        InputManager.TriggerRumble(0.1f, 0.01f);

        // Obtain the host component and invoke its private OnApplicationQuit
        // method to simulate the application closing.
        FieldInfo hostField = typeof(InputManager).GetField(
            "rumbleHost", BindingFlags.NonPublic | BindingFlags.Static);
        var host = hostField.GetValue(null);
        Assert.IsNotNull(host, "Host should exist before quitting");
        var onQuit = host.GetType().GetMethod(
            "OnApplicationQuit", BindingFlags.NonPublic | BindingFlags.Instance);
        onQuit.Invoke(host, null);

        // The invocation should clear the static reference so subsequent rumble
        // requests know the host was destroyed.
        Assert.IsNull(hostField.GetValue(null),
            "Rumble host should clear its static reference on quit");

        // Reset InputManager and remove the test device for later tests.
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(
            typeof(InputManager).TypeHandle);
        InputSystem.RemoveDevice(pad);
    }
}
#endif
