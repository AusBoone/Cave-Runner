using NUnit.Framework;
using UnityEngine;
using System.Reflection;

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

    [Test]
    public void TouchJump_TriggersPlayerJump()
    {
        // Setup player and ground so the controller detects it is on solid ground.
        var player = new GameObject("player") { tag = "Player" };
        player.AddComponent<Rigidbody2D>();
        player.AddComponent<CapsuleCollider2D>();
        var pc = player.AddComponent<PlayerController>();
        pc.groundLayer = LayerMask.GetMask("Default");
        var ground = new GameObject("ground");
        ground.AddComponent<BoxCollider2D>();
        ground.transform.position = new Vector3(0f, -0.05f, 0f);

        var gmObj = new GameObject("gm");
        var gm = gmObj.AddComponent<GameManager>();
        gm.StartGame();

        // Simulate tapping the jump button via the mobile UI.
        InputManager.TouchJumpDown();
        pc.Update();

        bool jumping = (bool)typeof(PlayerController)
            .GetField("isJumping", BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(pc);
        Assert.IsTrue(jumping, "Player should start jumping when jump button is touched");

        Object.DestroyImmediate(player);
        Object.DestroyImmediate(ground);
        Object.DestroyImmediate(gmObj);
    }

    [Test]
    public void TouchSlide_StartsSlideWhenGrounded()
    {
        // Setup player standing on a ground collider.
        var player = new GameObject("player") { tag = "Player" };
        player.AddComponent<Rigidbody2D>();
        player.AddComponent<CapsuleCollider2D>();
        var pc = player.AddComponent<PlayerController>();
        pc.groundLayer = LayerMask.GetMask("Default");
        var ground = new GameObject("ground");
        ground.AddComponent<BoxCollider2D>();
        ground.transform.position = new Vector3(0f, -0.05f, 0f);

        var gmObj = new GameObject("gm");
        var gm = gmObj.AddComponent<GameManager>();
        gm.StartGame();

        // Simulate pressing the slide button.
        InputManager.TouchSlideDown();
        pc.Update();

        bool sliding = (bool)typeof(PlayerController)
            .GetField("isSliding", BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(pc);
        Assert.IsTrue(sliding, "Player should begin sliding when slide button is touched");

        Object.DestroyImmediate(player);
        Object.DestroyImmediate(ground);
        Object.DestroyImmediate(gmObj);
    }
}
