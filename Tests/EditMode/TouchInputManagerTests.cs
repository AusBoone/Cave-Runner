using NUnit.Framework;
using UnityEngine;
using System.Reflection;
using System.Linq;

/// <summary>
/// Tests for the new touch input helpers exposed through InputManager.
/// They ensure the flags set by TouchInputManager are consumed correctly
/// by the existing Get* methods. A regression test also verifies that the
/// mobile‑specific canvas created by <see cref="UIManager"/> does not
/// duplicate itself when scenes change, which would otherwise clutter the
/// hierarchy with redundant UI objects.
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

    [Test]
    public void MobileCanvas_DoesNotDuplicateAcrossScenes()
    {
        // Instantiate the first UIManager to simulate the initial scene.
        var uiObj1 = new GameObject("ui1");
        var ui1 = uiObj1.AddComponent<UIManager>();

        // Manually create and register a mobile canvas as if Awake() had run
        // on a mobile platform. Tests execute in the editor where
        // Application.isMobilePlatform is false, so we inject the canvas via
        // reflection to exercise the cleanup logic.
        var prefab = Resources.Load<GameObject>("UI/MobileUI");
        var canvas1 = Object.Instantiate(prefab);
        typeof(UIManager)
            .GetField("mobileCanvas", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(ui1, canvas1);
        Object.DontDestroyOnLoad(canvas1);

        // Destroy the first manager to mimic a scene transition. Without a
        // cleanup method this would leave the mobile canvas behind.
        Object.DestroyImmediate(uiObj1);

        // Create a second manager as would happen when a new scene loads.
        var uiObj2 = new GameObject("ui2");
        var ui2 = uiObj2.AddComponent<UIManager>();
        var canvas2 = Object.Instantiate(prefab);
        typeof(UIManager)
            .GetField("mobileCanvas", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(ui2, canvas2);
        Object.DontDestroyOnLoad(canvas2);

        // Count the mobile canvases that survived the transition. Only the
        // second one should remain because OnDestroy should remove the first.
        var mobileCanvases = Object.FindObjectsOfType<Canvas>()
            .Where(c => c.gameObject.name.Contains("MobileUI"));
        Assert.AreEqual(1, mobileCanvases.Count(),
            "Only one mobile canvas should exist after recreating UIManager");

        // Clean up objects created during the test to keep the editor state
        // pristine for subsequent tests.
        Object.DestroyImmediate(canvas2);
        Object.DestroyImmediate(uiObj2);
    }
}
