// -----------------------------------------------------------------------------
// SpeedBoostPowerUpTests.cs
// -----------------------------------------------------------------------------
// Test suite for the SpeedBoostPowerUp component. These edit-mode tests create
// minimal stand-ins for the player, GameManager and related services so the
// power-up's behaviour can be validated without launching the full game. Each
// test verifies correct interaction with GameManager, audio and rumble feedback,
// and proper object pooling. Reflection is used to inspect private state on the
// GameManager. Edge cases cover scenarios where required managers are missing.
// -----------------------------------------------------------------------------

using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Reflection;

/// <summary>
/// Verifies that <see cref="SpeedBoostPowerUp"/> correctly interacts with
/// <see cref="GameManager"/>, plays audio feedback, triggers rumble and handles
/// pooling. Edge cases confirm graceful behaviour when required managers are
/// absent.
/// </summary>
public class SpeedBoostPowerUpTests
{
    /// <summary>
    /// Minimal GameManager subclass that bypasses heavy singleton initialization
    /// by replacing <c>Awake</c> with a lightweight version that merely assigns
    /// <see cref="GameManager.Instance"/>.
    /// </summary>
    private class TestGameManager : GameManager
    {
        new void Awake()
        {
            Instance = this;
        }
    }

    /// <summary>
    /// Collecting the power-up should call <see cref="GameManager.ActivateSpeedBoost"/>
    /// with the configured parameters, play the pickup sound, trigger rumble and
    /// return the object to its pool.
    /// </summary>
    [Test]
    public void OnTriggerEnter2D_ActivatesSpeedBoostAndReturnsToPool()
    {
        // -----------------------------------------------------------------
        // Arrange: build minimal objects for audio, manager, player and pool.
        // -----------------------------------------------------------------
        // Audio setup for verifying sound playback.
        var audioObj = new GameObject("audio");
        var am = audioObj.AddComponent<AudioManager>();
        am.effectsSource = audioObj.AddComponent<AudioSource>();
        am.musicSource = audioObj.AddComponent<AudioSource>();
        am.musicSourceSecondary = audioObj.AddComponent<AudioSource>();

        // GameManager that captures state changes without full initialization.
        var gmObj = new GameObject("gm");
        var gm = gmObj.AddComponent<TestGameManager>();

        // Register a dummy gamepad so rumble requests are honoured.
        var pad = InputSystem.AddDevice<Gamepad>();
        InputManager.SetRumbleEnabled(true);

        // Player object only needs a collider and tag for detection.
        var player = new GameObject("player");
        player.tag = "Player";
        var playerCollider = player.AddComponent<CapsuleCollider2D>();

        // Pool to verify return behaviour.
        var poolObj = new GameObject("pool");
        var pool = poolObj.AddComponent<ObjectPool>();

        // Speed boost power-up instance.
        var powerObj = new GameObject("power");
        var sb = powerObj.AddComponent<SpeedBoostPowerUp>();
        sb.collectClip = AudioClip.Create("pickup", 44100, 1, 44100, false);
        var po = powerObj.AddComponent<PooledObject>();
        po.Pool = pool;
        var col = powerObj.AddComponent<BoxCollider2D>();
        col.isTrigger = true;

        // -----------------------------------------------------------------
        // Act: simulate the player collecting the power-up.
        // -----------------------------------------------------------------
        sb.OnTriggerEnter2D(playerCollider);

        // -----------------------------------------------------------------
        // Assert: GameManager state, feedback systems and pooling.
        // -----------------------------------------------------------------
        // Verify GameManager received correct parameters via reflection.
        float timer = (float)typeof(GameManager)
            .GetField("speedBoostTimer", BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(gm);
        float mult = (float)typeof(GameManager)
            .GetField("speedMultiplier", BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(gm);
        Assert.AreEqual(sb.duration, timer, "Speed boost duration should match power-up value");
        Assert.AreEqual(sb.speedMultiplier, mult, "Speed multiplier should match power-up value");

        // Sound and rumble feedback should trigger so the player receives
        // immediate response that the item was collected.
        Assert.IsTrue(am.effectsSource.isPlaying, "Collect sound should play");
        var routine = typeof(InputManager)
            .GetField("rumbleRoutine", BindingFlags.NonPublic | BindingFlags.Static)
            .GetValue(null);
        Assert.IsNotNull(routine, "Rumble should start on collection");

        // Power-up should have been returned to the pool to avoid garbage.
        Assert.IsFalse(powerObj.activeSelf, "Returned power-up should be inactive");
        Assert.AreEqual(pool.transform, powerObj.transform.parent,
            "Returned power-up should be parented to its pool");

        // Cleanup.
        InputManager.Shutdown();
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(
            typeof(InputManager).TypeHandle);
        InputSystem.RemoveDevice(pad);
        Object.DestroyImmediate(player);
        Object.DestroyImmediate(powerObj);
        Object.DestroyImmediate(poolObj);
        Object.DestroyImmediate(audioObj);
        Object.DestroyImmediate(gmObj);
    }

    /// <summary>
    /// Without an active <see cref="GameManager"/> the power-up should still
    /// provide feedback but destroy itself instead of attempting to apply the
    /// boost.
    /// </summary>
    [Test]
    public void OnTriggerEnter2D_NoGameManager_DestroysPowerUp()
    {
        // -----------------------------------------------------------------
        // Arrange: create power-up with no GameManager or pool to validate
        // graceful destruction while still providing feedback.
        // -----------------------------------------------------------------
        var audioObj = new GameObject("audio");
        var am = audioObj.AddComponent<AudioManager>();
        am.effectsSource = audioObj.AddComponent<AudioSource>();
        am.musicSource = audioObj.AddComponent<AudioSource>();
        am.musicSourceSecondary = audioObj.AddComponent<AudioSource>();

        var pad = InputSystem.AddDevice<Gamepad>();
        InputManager.SetRumbleEnabled(true);

        var player = new GameObject("player");
        player.tag = "Player";
        var playerCollider = player.AddComponent<CapsuleCollider2D>();

        var powerObj = new GameObject("power");
        var sb = powerObj.AddComponent<SpeedBoostPowerUp>();
        sb.collectClip = AudioClip.Create("pickup", 44100, 1, 44100, false);
        var col = powerObj.AddComponent<BoxCollider2D>();
        col.isTrigger = true;

        // -----------------------------------------------------------------
        // Act: collide the player with the power-up.
        // -----------------------------------------------------------------
        sb.OnTriggerEnter2D(playerCollider);

        // -----------------------------------------------------------------
        // Assert: object destroys itself but still triggers feedback.
        // -----------------------------------------------------------------
        Assert.IsTrue(powerObj == null, "Power-up should destroy itself when GameManager is missing");
        Assert.IsTrue(am.effectsSource.isPlaying, "Sound should play even without GameManager");
        var routine = typeof(InputManager)
            .GetField("rumbleRoutine", BindingFlags.NonPublic | BindingFlags.Static)
            .GetValue(null);
        Assert.IsNotNull(routine, "Rumble should trigger even without GameManager");

        // Cleanup.
        InputManager.Shutdown();
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(
            typeof(InputManager).TypeHandle);
        InputSystem.RemoveDevice(pad);
        Object.DestroyImmediate(player);
        Object.DestroyImmediate(audioObj);
    }
}

