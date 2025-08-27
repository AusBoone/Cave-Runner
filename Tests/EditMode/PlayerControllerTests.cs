// PlayerControllerTests.cs
// -----------------------
// Contains EditMode unit tests validating the behaviour of PlayerController.

using NUnit.Framework;
using UnityEngine;
using System.Reflection;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Tests for <see cref="PlayerController"/>. In addition to verifying the
/// buffered jump and slide behaviours, these tests now exercise the dynamic
/// ground detection logic. Ray lengths are derived from the collider bounds, so
/// we confirm ground contact works with both very small and very large player
/// colliders.
/// </summary>
public class PlayerControllerTests
{
    [Test]
    public void RequiredComponents_AutoAddedByAttribute()
    {
        // Creating a GameObject and adding PlayerController alone should
        // automatically populate essential components via the RequireComponent
        // attribute. This guards against null reference errors at runtime.
        var player = new GameObject("player");
        player.AddComponent<PlayerController>();

        Assert.IsNotNull(player.GetComponent<Rigidbody2D>(),
            "Rigidbody2D should be auto-added by RequireComponent");
        Assert.IsNotNull(player.GetComponent<CapsuleCollider2D>(),
            "CapsuleCollider2D should be auto-added by RequireComponent");
        Assert.IsNotNull(player.GetComponent<Animator>(),
            "Animator should be auto-added by RequireComponent");

        Object.DestroyImmediate(player);
    }

    [Test]
    public void BufferedJump_TriggersAfterLanding()
    {
        // Create player object with required components
        var player = new GameObject("player");
        var rb = player.AddComponent<Rigidbody2D>();
        var col = player.AddComponent<CapsuleCollider2D>();
        var pc = player.AddComponent<PlayerController>();
        pc.groundLayer = LayerMask.GetMask("Default");
        // Place ground just below the origin so the raycast detects it
        var ground = new GameObject("ground");
        ground.AddComponent<BoxCollider2D>();
        ground.transform.position = new Vector3(0f, -0.05f, 0f);

        // Start above the ground and prime the jump buffer
        player.transform.position = new Vector3(0f, 1f, 0f);
        typeof(PlayerController).GetField("jumpBufferTimer", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(pc, 0.05f);
        typeof(PlayerController).GetField("isGrounded", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(pc, false);
        typeof(PlayerController).GetField("coyoteTimer", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(pc, 0f);
        typeof(PlayerController).GetField("jumpsRemaining", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(pc, 0);

        // Update while airborne - jump should not occur yet
        pc.Update();
        Assert.IsFalse((bool)typeof(PlayerController).GetField("isJumping", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(pc));

        // Land on the ground and update again - buffered jump should queue a jump
        player.transform.position = Vector3.zero;
        pc.Update();
        // Jump state should be armed prior to the physics step.
        Assert.IsTrue((bool)typeof(PlayerController).GetField("isJumping", BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(pc));
        // Run physics to apply the queued jump force.
        typeof(PlayerController).GetMethod("FixedUpdate", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(pc, null);
        Assert.IsTrue(rb.velocity.y > 0f,
            "Jump force should be applied during FixedUpdate");

        Object.DestroyImmediate(player);
        Object.DestroyImmediate(ground);
    }

    [Test]
    public void ShortHop_ExtraGravityAppliedWhenJumpReleased()
    {
        // Verify the enhanced gravity logic shortens upward velocity when the
        // jump input is no longer held.
        var player = new GameObject("player");
        var rb = player.AddComponent<Rigidbody2D>();
        player.AddComponent<CapsuleCollider2D>();
        var pc = player.AddComponent<PlayerController>();

        // Simulate upward motion without holding jump.
        rb.velocity = new Vector2(0f, 5f);

        // Call the private method directly with reflection so we control the
        // delta time used for the calculation.
        typeof(PlayerController)
            .GetMethod("ApplyEnhancedGravity", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(pc, new object[] { 0.1f, false });

        Assert.Less(rb.velocity.y, 5f, "Velocity should decrease due to extra gravity");

        Object.DestroyImmediate(player);
    }

    [Test]
    public void BufferedSlide_TriggersAfterLanding()
    {
        // Similar to the jump buffer test but for slide input.
        var player = new GameObject("player");
        player.AddComponent<Rigidbody2D>();
        player.AddComponent<CapsuleCollider2D>();
        var pc = player.AddComponent<PlayerController>();
        pc.groundLayer = LayerMask.GetMask("Default");

        var ground = new GameObject("ground");
        ground.AddComponent<BoxCollider2D>();
        ground.transform.position = new Vector3(0f, -0.05f, 0f);

        player.transform.position = new Vector3(0f, 1f, 0f);
        typeof(PlayerController).GetField("slideBufferTimer", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(pc, 0.05f);
        typeof(PlayerController).GetField("isGrounded", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(pc, false);

        pc.Update();
        Assert.IsFalse((bool)typeof(PlayerController).GetField("isSliding", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(pc));

        player.transform.position = Vector3.zero;
        pc.Update();
        Assert.IsTrue((bool)typeof(PlayerController).GetField("isSliding", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(pc));

        Object.DestroyImmediate(player);
        Object.DestroyImmediate(ground);
    }

    [Test]
    public void FastFall_IncreasesDescentVelocity()
    {
        // Ensure fast fall multiplies gravity when the down input is held.
        var player = new GameObject("player");
        var rb = player.AddComponent<Rigidbody2D>();
        player.AddComponent<CapsuleCollider2D>();
        var pc = player.AddComponent<PlayerController>();

        // Simulate a downward velocity.
        rb.velocity = new Vector2(0f, -5f);

        typeof(PlayerController)
            .GetMethod("ApplyEnhancedGravity", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(pc, new object[] { 0.1f, true });

        Assert.Less(rb.velocity.y, -5f, "Velocity should increase when fast falling");

        Object.DestroyImmediate(player);
    }

    [Test]
    public void SlideCancel_EndsSlideEarly()
    {
        // Verify that calling EndSlide exits the slide state immediately. Input
        // events can't be simulated here so we invoke the private method via
        // reflection to mimic releasing the slide key.
        var player = new GameObject("player");
        player.AddComponent<Rigidbody2D>();
        var col = player.AddComponent<CapsuleCollider2D>();
        var pc = player.AddComponent<PlayerController>();

        // Begin a slide by modifying collider size and flagging the state.
        var sizeField = typeof(PlayerController).GetField("colliderSize", BindingFlags.NonPublic | BindingFlags.Instance);
        var offsetField = typeof(PlayerController).GetField("colliderOffset", BindingFlags.NonPublic | BindingFlags.Instance);
        Vector2 origSize = col.size;
        Vector2 origOffset = col.offset;
        sizeField.SetValue(pc, origSize);
        offsetField.SetValue(pc, origOffset);
        col.size = new Vector2(origSize.x, origSize.y / 2f);
        col.offset = new Vector2(origOffset.x, origOffset.y - origSize.y / 4f);
        typeof(PlayerController).GetField("isSliding", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(pc, true);
        typeof(PlayerController).GetField("slideTimer", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(pc, 1f);

        // Cancel the slide as if the player released the key
        typeof(PlayerController).GetMethod("EndSlide", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(pc, null);

        Assert.IsFalse((bool)typeof(PlayerController).GetField("isSliding", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(pc),
            "Slide state should clear when cancelled");

        // Collider should be reset to its original dimensions
        Assert.AreEqual(origSize, col.size);
        Assert.AreEqual(origOffset, col.offset);

        Object.DestroyImmediate(player);
    }

    [Test]
    public void AirDash_AppliesHorizontalImpulse()
    {
        // The private TryAirDash method should push the player in the chosen direction.
        var player = new GameObject("player");
        var rb = player.AddComponent<Rigidbody2D>();
        player.AddComponent<CapsuleCollider2D>();
        var pc = player.AddComponent<PlayerController>();

        rb.velocity = Vector2.zero;
        typeof(PlayerController)
            .GetMethod("TryAirDash", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(pc, new object[] { 1f });
        // Physics step applies the queued dash impulse.
        typeof(PlayerController).GetMethod("FixedUpdate", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(pc, null);

        Assert.Greater(rb.velocity.x, 0f, "Air dash should add positive X velocity");

        Object.DestroyImmediate(player);
    }

    [Test]
    public void GroundCheck_DetectsGroundWithSmallCollider()
    {
        // Ensure dynamic ray length still detects ground when the collider is very short.
        VerifyGroundedDetection(0.2f, -0.15f);
    }

    [Test]
    public void GroundCheck_DetectsGroundWithLargeCollider()
    {
        // A tall collider used to exceed the fixed ray length. Dynamic sizing should now hit the ground.
        VerifyGroundedDetection(3f, -1.6f);
    }

    /// <summary>
    /// Helper that constructs a player with a collider of the specified height and
    /// verifies that <see cref="PlayerController"/>'s ground raycast registers the
    /// nearby surface.
    /// </summary>
    /// <param name="colliderHeight">Height assigned to the capsule collider.</param>
    /// <param name="groundY">Y position for the ground object.</param>
    private static void VerifyGroundedDetection(float colliderHeight, float groundY)
    {
        var player = new GameObject("player");
        player.AddComponent<Rigidbody2D>();
        var col = player.AddComponent<CapsuleCollider2D>();
        col.size = new Vector2(1f, colliderHeight);
        var pc = player.AddComponent<PlayerController>();
        pc.groundLayer = LayerMask.GetMask("Default");

        var ground = new GameObject("ground");
        ground.AddComponent<BoxCollider2D>();
        ground.transform.position = new Vector3(0f, groundY, 0f);

        typeof(PlayerController)
            .GetMethod("CheckGrounded", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(pc, null);

        bool grounded = (bool)typeof(PlayerController)
            .GetField("isGrounded", BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(pc);
        Assert.IsTrue(grounded, "Ray length based on collider bounds should register the ground");

        Object.DestroyImmediate(player);
        Object.DestroyImmediate(ground);
    }

    [Test]
    public void GroundCheck_RespectsDiagonalGravity()
    {
        // When gravity points diagonally, CheckGrounded should cast along that
        // vector and still detect surfaces positioned in the same direction.
        var player = new GameObject("player");
        player.AddComponent<Rigidbody2D>();
        var col = player.AddComponent<CapsuleCollider2D>();
        col.size = new Vector2(1f, 1f);
        var pc = player.AddComponent<PlayerController>();
        pc.groundLayer = LayerMask.GetMask("Default");

        // Use a diagonal gravity vector and place the ground along that path.
        Vector2 gravity = new Vector2(5f, -5f);
        Physics2D.gravity = gravity;
        Vector2 rayDir = gravity.normalized;
        Vector3 extents3 = col.bounds.extents;
        Vector2 extents = new Vector2(extents3.x, extents3.y);
        Vector2 absDir = new Vector2(Mathf.Abs(rayDir.x), Mathf.Abs(rayDir.y));
        float distance = Vector2.Dot(extents, absDir) + 0.05f;

        var ground = new GameObject("ground");
        ground.AddComponent<BoxCollider2D>();
        Vector2 groundPos = (Vector2)player.transform.position + rayDir * distance;
        ground.transform.position = new Vector3(groundPos.x, groundPos.y, 0f);

        typeof(PlayerController)
            .GetMethod("CheckGrounded", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(pc, null);

        bool grounded = (bool)typeof(PlayerController)
            .GetField("isGrounded", BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(pc);
        Assert.IsTrue(grounded, "Ray should hit ground along diagonal gravity");

        Physics2D.gravity = new Vector2(0f, -9.81f);
        Object.DestroyImmediate(player);
        Object.DestroyImmediate(ground);
    }

    [Test]
    public void GroundCheck_DefaultsDownWithZeroGravity()
    {
        // With zero gravity the ray should fall back to Vector2.down so ground
        // directly beneath the player is still detected.
        var player = new GameObject("player");
        player.AddComponent<Rigidbody2D>();
        player.AddComponent<CapsuleCollider2D>();
        var pc = player.AddComponent<PlayerController>();
        pc.groundLayer = LayerMask.GetMask("Default");

        Physics2D.gravity = Vector2.zero;

        var ground = new GameObject("ground");
        ground.AddComponent<BoxCollider2D>();
        ground.transform.position = new Vector3(0f, -0.05f, 0f);

        typeof(PlayerController)
            .GetMethod("CheckGrounded", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(pc, null);

        bool grounded = (bool)typeof(PlayerController)
            .GetField("isGrounded", BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(pc);
        Assert.IsTrue(grounded, "Ray should default downward when gravity is zero");

        Physics2D.gravity = new Vector2(0f, -9.81f);
        Object.DestroyImmediate(player);
        Object.DestroyImmediate(ground);
    }

    [Test]
    public void EnhancedGravity_IgnoresZeroGravity()
    {
        // Physics2D.gravity.normalized returns NaN when gravity is zero. The
        // method should early out to avoid corrupting the velocity vector.
        var player = new GameObject("player");
        var rb = player.AddComponent<Rigidbody2D>();
        player.AddComponent<CapsuleCollider2D>();
        var pc = player.AddComponent<PlayerController>();

        rb.velocity = new Vector2(0f, 1f);
        Physics2D.gravity = Vector2.zero;

        typeof(PlayerController)
            .GetMethod("ApplyEnhancedGravity", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(pc, new object[] { 0.1f, false });

        Assert.AreEqual(1f, rb.velocity.y, 0.001f, "Velocity should remain unchanged when gravity is zero");

        Physics2D.gravity = new Vector2(0f, -9.81f);
        Object.DestroyImmediate(player);
    }

    [Test]
    public void AirDive_DefaultsDownWhenGravityZero()
    {
        // The air-dive uses the gravity vector's direction. When gravity has
        // zero magnitude the controller should fall back to Vector2.down so the
        // dive still behaves sensibly and does not introduce NaN velocity.

        // Ensure a GameManager exists and is running so PlayerController.Update
        // processes input and movement logic.
        var gmObj = new GameObject("gm");
        var gm = gmObj.AddComponent<GameManager>();
        typeof(GameManager).GetField("isRunning", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(gm, true);

        var player = new GameObject("player");
        var rb = player.AddComponent<Rigidbody2D>();
        player.AddComponent<CapsuleCollider2D>();
        var pc = player.AddComponent<PlayerController>();

        // Start airborne with zero gravity so the dive direction must default
        // to Vector2.down.
        typeof(PlayerController).GetField("isGrounded", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(pc, false);
        rb.velocity = Vector2.zero;
        Physics2D.gravity = Vector2.zero;

        // Trigger the slide input to initiate the dive.
        InputManager.TouchSlideDown();
        pc.Update();
        // Apply the queued downward velocity.
        typeof(PlayerController).GetMethod("FixedUpdate", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(pc, null);
        InputManager.TouchSlideUp();
        InputManager.GetSlideUp();

        Assert.AreEqual(-pc.airDiveForce, rb.velocity.y, 0.001f,
            "Air-dive should push the player downward when gravity is zero");

        // Restore global state to avoid affecting other tests.
        Physics2D.gravity = new Vector2(0f, -9.81f);
        Object.DestroyImmediate(player);
        Object.DestroyImmediate(gmObj);
    }

#if ENABLE_INPUT_SYSTEM
    [Test]
    public void AttemptJump_TriggersRumble()
    {
        var gamepad = InputSystem.AddDevice<Gamepad>();
        var player = new GameObject("player");
        player.AddComponent<Rigidbody2D>();
        player.AddComponent<CapsuleCollider2D>();
        var pc = player.AddComponent<PlayerController>();

        typeof(PlayerController).GetField("isGrounded", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(pc, true);

        InputManager.SetRumbleEnabled(true);
        typeof(PlayerController).GetMethod("AttemptJump", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(pc, null);

        FieldInfo field = typeof(InputManager).GetField("rumbleRoutine", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(field.GetValue(null), "Rumble should start when jumping with a gamepad connected");

        Object.DestroyImmediate(player);
        InputSystem.RemoveDevice(gamepad);
    }
#endif
}

