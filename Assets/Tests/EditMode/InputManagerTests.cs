#if ENABLE_INPUT_SYSTEM
using NUnit.Framework;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Tests related to InputManager's controller bindings. Ensures that both
/// PlayStation and Xbox mappings are present when using the new Input System.
/// 2028 addition: verifies that <see cref="InputManager.Shutdown"/> correctly
/// releases actions to prevent memory leaks between play sessions.
/// </summary>
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

    [Test]
    public void TriggerRumble_StartsCoroutine()
    {
        var gamepad = InputSystem.AddDevice<Gamepad>();
        InputManager.SetRumbleEnabled(true);

        InputManager.TriggerRumble(0.5f, 0.01f);

        FieldInfo field = typeof(InputManager).GetField("rumbleRoutine", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(field, "rumbleRoutine field missing");
        Assert.IsNotNull(field.GetValue(null), "TriggerRumble should start a coroutine when a gamepad is present");
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
    /// Reinitializing InputManager should not spawn multiple rumble hosts.
    /// </summary>
    [UnityTest]
    public IEnumerator StaticConstructor_ReusesRumbleHost()
    {
        InputManager.GetJumpDown();
        yield return null;
        int before = 0;
        foreach (var go in Object.FindObjectsOfType<GameObject>())
        {
            if (go.name == "InputManagerRumbleHost")
                before++;
        }

        typeof(InputManager).GetField("rumbleHost", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, null);
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(InputManager).TypeHandle);
        yield return null;

        int after = 0;
        foreach (var go in Object.FindObjectsOfType<GameObject>())
        {
            if (go.name == "InputManagerRumbleHost")
                after++;
        }

        Assert.AreEqual(before, after, "Only one rumble host should exist after reinit");
    }
}
#endif
