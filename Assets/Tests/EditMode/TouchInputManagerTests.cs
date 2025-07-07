using NUnit.Framework;

/// <summary>
/// Tests for the new touch input helpers exposed through InputManager.
/// They ensure the flags set by TouchInputManager are consumed correctly
/// by the existing Get* methods.
/// </summary>
public class TouchInputManagerTests
{
    [SetUp]
    public void ClearState()
    {
        // Reset any lingering touch flags before each test.
        InputManager.TouchJumpUp();
        InputManager.TouchSlideUp();
        InputManager.TouchPause();
    }

    [Test]
    public void TouchJumpDown_SetsGetJumpDown()
    {
        InputManager.TouchJumpDown();
        Assert.IsTrue(InputManager.GetJumpDown(), "Jump down should be reported");
        Assert.IsFalse(InputManager.GetJumpDown(), "Flag should clear after read");
    }

    [Test]
    public void TouchSlideHold_ReflectsInGetSlide()
    {
        InputManager.TouchSlideDown();
        Assert.IsTrue(InputManager.GetSlide(), "Slide should be held while touch flag active");
        InputManager.TouchSlideUp();
        Assert.IsFalse(InputManager.GetSlide(), "Slide should clear after release");
    }

    [Test]
    public void TouchPause_TriggersGetPauseDownOnce()
    {
        InputManager.TouchPause();
        Assert.IsTrue(InputManager.GetPauseDown(), "Pause down should report after touch");
        Assert.IsFalse(InputManager.GetPauseDown(), "Flag should clear after first read");
    }
}
