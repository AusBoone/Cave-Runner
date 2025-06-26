using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Tests verifying that <see cref="InputManager"/> correctly loads and stores
/// key bindings. These tests cover the KeyCode fallback behaviour so they run
/// even when the new Input System package is absent.
/// </summary>
public class InputManagerTests
{
    [SetUp]
    public void ClearPrefs()
    {
        PlayerPrefs.DeleteAll();
    }

    [Test]
    public void Defaults_AreLoaded_WhenPrefsMissing()
    {
        Assert.AreEqual(KeyCode.Space, InputManager.JumpKey);
        Assert.AreEqual(KeyCode.LeftControl, InputManager.SlideKey);
        Assert.AreEqual(KeyCode.Escape, InputManager.PauseKey);
    }

    [Test]
    public void SetJumpKey_PersistsValue()
    {
        InputManager.SetJumpKey(KeyCode.Z);
        Assert.AreEqual(KeyCode.Z, InputManager.JumpKey);
        Assert.AreEqual("Z", PlayerPrefs.GetString("JumpKey"));
    }
}
