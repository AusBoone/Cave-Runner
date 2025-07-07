#if ENABLE_INPUT_SYSTEM
using NUnit.Framework;
using System.Reflection;
using UnityEngine.InputSystem;

/// <summary>
/// Tests related to InputManager's controller bindings. Ensures that both
/// PlayStation and Xbox mappings are present when using the new Input System.
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
}
#endif
