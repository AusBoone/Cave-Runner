#if !ENABLE_INPUT_SYSTEM
using NUnit.Framework;

/// <summary>
/// Validates behaviour of <see cref="InputManager"/> when the project uses the
/// legacy input manager instead of Unity's new Input System. These tests ensure
/// that stubbed members compile and execute without side effects.
/// </summary>
public class InputManagerLegacyTests
{
    /// <summary>
    /// Calling <see cref="InputManager.TriggerRumble"/> should not throw even
    /// though rumble functionality is unavailable. This guards against build
    /// errors in projects that do not include the Input System package.
    /// </summary>
    [Test]
    public void TriggerRumble_NoInputSystem_DoesNothing()
    {
        // The method is expected to silently ignore requests; this assertion
        // verifies that no exception surfaces during the call.
        Assert.DoesNotThrow(() => InputManager.TriggerRumble(1f, 1f));
    }
}
#endif

