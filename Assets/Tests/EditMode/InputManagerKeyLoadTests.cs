// InputManagerKeyLoadTests.cs
// -----------------------------------------------------------------------------
// Validates the behavior of InputManager's legacy PlayerPrefs key loading
// functionality. The tests simulate corrupted preference data to ensure the
// manager warns via LoggingHelper and safely falls back to default bindings.
// Run via the Unity Test Runner in edit mode.
// -----------------------------------------------------------------------------

using NUnit.Framework;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Exercises the <see cref="InputManager"/> key-loading path to confirm that
/// invalid PlayerPrefs entries trigger a warning and revert to expected
/// defaults. This guards against silent failures where tampered or corrupted
/// preferences could leave controls unresponsive without any developer
/// visibility.
/// </summary>
public class InputManagerKeyLoadTests
{
    /// <summary>
    /// When a saved key cannot be parsed into a <see cref="KeyCode"/>, the
    /// manager should emit a warning and fall back to the documented default.
    /// </summary>
    [Test]
    public void LoadKey_InvalidValue_LogsWarningAndRevertsToDefault()
    {
        // Ensure warnings are not suppressed so LogAssert can intercept them.
        LoggingHelper.VerboseEnabled = true;

        // Seed an invalid value to simulate corrupt or manually edited prefs.
        PlayerPrefs.SetString("JumpKey", "NotAKey");

        // The next initialization of InputManager should warn about the bad
        // value and then restore the default key binding.
        LogAssert.Expect(
            LogType.Warning,
            "Invalid KeyCode 'NotAKey' for preference 'JumpKey'. Reverting to default 'Space'.");

        // Reset any existing state and rerun the static constructor so LoadKey
        // executes with the corrupt preference in place.
        InputManager.Shutdown();
        RuntimeHelpers.RunClassConstructor(typeof(InputManager).TypeHandle);

        // After initialization, the JumpKey property should hold the default
        // binding since the saved value was rejected.
        Assert.AreEqual(
            KeyCode.Space,
            InputManager.JumpKey,
            "JumpKey should revert to Space when an invalid key is saved");

        // Clean up to avoid polluting other tests.
        PlayerPrefs.DeleteKey("JumpKey");
        InputManager.Shutdown();
    }
}

