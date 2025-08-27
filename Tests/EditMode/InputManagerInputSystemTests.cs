#if ENABLE_INPUT_SYSTEM
using NUnit.Framework;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

/// <summary>
/// Edit mode tests focused on <see cref="InputManager"/> behaviour when the
/// new Input System package is available. These tests use
/// <see cref="InputTestFixture"/> to simulate devices so no physical hardware
/// is required. The suite covers interactive rebinding persistence, controller
/// rumble and shutdown cleanup to ensure no unmanaged resources linger between
/// play sessions.
///
/// Usage example:
/// <code>
/// // Rebind Jump to the K key and assert the preference was saved.
/// yield return RebindingJump_UpdatesPlayerPrefs();
/// </code>
///
/// Assumptions:
/// - The project has the Input System package installed.
/// - Rumble is enabled via <see cref="InputManager.SetRumbleEnabled"/>.
/// </summary>
public class InputManagerInputSystemTests : InputTestFixture
{
    /// <summary>
    /// Simple MonoBehaviour used solely to host coroutines during tests. Unity's
    /// rebinding API requires a live behaviour to run the asynchronous operation.
    /// </summary>
    private class CoroutineHost : MonoBehaviour { }

    /// <summary>
    /// Custom gamepad that tracks the last rumble values supplied via
    /// <see cref="Gamepad.SetMotorSpeeds"/> so tests can assert vibration state
    /// without touching native hardware drivers.
    /// </summary>
    [InputControlLayout(stateType = typeof(GamepadState))]
    private class TestGamepad : Gamepad
    {
        public float lastLowFrequency;  // Last low-frequency motor speed.
        public float lastHighFrequency; // Last high-frequency motor speed.

        /// <summary>
        /// Records rumble values rather than forwarding them to hardware.
        /// </summary>
        public override void SetMotorSpeeds(float lowFrequency, float highFrequency)
        {
            lastLowFrequency = lowFrequency;
            lastHighFrequency = highFrequency;
        }
    }

    /// <summary>
    /// Rebinding an action should update the corresponding PlayerPrefs entry so
    /// the new control persists across sessions.
    /// </summary>
    [UnityTest]
    public IEnumerator RebindingJump_UpdatesPlayerPrefs()
    {
        // Register a test keyboard and ensure the preference starts blank so the
        // test result is not influenced by previous runs.
        var keyboard = InputSystem.AddDevice<Keyboard>();
        PlayerPrefs.DeleteKey("JumpBinding");

        // Create a host behaviour to run the asynchronous rebind coroutine.
        var host = new GameObject("RebindHost").AddComponent<CoroutineHost>();

        // Begin rebinding and wait a frame for the operation to start listening.
        InputManager.StartRebindJump(host, null);
        yield return null;

        // Simulate the user pressing the K key to complete the rebind.
        Press(keyboard.kKey);
        yield return null; // Allow OnComplete handler to run.

        // PlayerPrefs should now store the new binding path.
        Assert.AreEqual("<Keyboard>/k", PlayerPrefs.GetString("JumpBinding"));

        // Clean up objects and preferences to avoid cross-test contamination.
        Object.DestroyImmediate(host.gameObject);
        PlayerPrefs.DeleteKey("JumpBinding");
    }

    /// <summary>
    /// TriggerRumble should start vibration on the provided controller and stop
    /// it automatically after the duration elapses.
    /// </summary>
    [UnityTest]
    public IEnumerator TriggerRumble_StartsAndStopsVibration()
    {
        // Register our custom test pad so rumble calls can be observed.
        InputSystem.RegisterLayout<TestGamepad>();
        var pad = InputSystem.AddDevice<TestGamepad>();

        // Ensure rumble is enabled then trigger a short vibration.
        InputManager.SetRumbleEnabled(true);
        InputManager.TriggerRumble(0.4f, 0.05f, pad);
        yield return null; // Allow coroutine to start.

        // Rumble should have begun with the specified strength.
        Assert.AreEqual(0.4f, pad.lastLowFrequency, 1e-5f);

        // Wait long enough for the vibration duration to expire.
        yield return new WaitForSecondsRealtime(0.1f);

        // Motors should have been reset to zero by the coroutine.
        Assert.AreEqual(0f, pad.lastLowFrequency, 1e-5f);
    }

    /// <summary>
    /// Shutdown should dispose actions and cancel any active rumble without
    /// throwing exceptions, ensuring the system can restart cleanly.
    /// </summary>
    [UnityTest]
    public IEnumerator Shutdown_DisposesActionsAndCancelsRumble()
    {
        // Register our test pad and start a long rumble to verify shutdown cancels it.
        InputSystem.RegisterLayout<TestGamepad>();
        var pad = InputSystem.AddDevice<TestGamepad>();
        InputManager.SetRumbleEnabled(true);
        InputManager.TriggerRumble(1f, 10f, pad);
        yield return null; // Coroutine begins and sets motor speeds.

        Assert.Greater(pad.lastLowFrequency, 0f, "Rumble should start before shutdown");

        // Call Shutdown and ensure it does not throw while also cancelling rumble.
        Assert.DoesNotThrow(() => InputManager.Shutdown());
        yield return null; // Allow any cleanup coroutines to run.

        // Motor speeds should be reset and actions cleared.
        Assert.AreEqual(0f, pad.lastLowFrequency, 1e-5f, "Shutdown should stop rumble");
        FieldInfo jumpField = typeof(InputManager).GetField("jumpAction", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNull(jumpField.GetValue(null), "Actions should be disposed during shutdown");

        // Reinitialize InputManager so later tests start with fresh actions.
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(InputManager).TypeHandle);
    }
}
#endif
