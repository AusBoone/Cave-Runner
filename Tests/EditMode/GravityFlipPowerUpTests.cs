// -----------------------------------------------------------------------------
// GravityFlipPowerUpTests.cs
// -----------------------------------------------------------------------------
// Validates the behaviour of the GravityFlipPowerUp component. The tests
// construct minimal substitutes for the player, GameManager and other
// dependencies so we can trigger OnTriggerEnter2D and assert that gravity is
// inverted, audio plays, rumble fires and objects are pooled. Reflection is
// employed to check private timers on GameManager. Edge case coverage ensures
// the power-up fails gracefully when managers are missing.
// -----------------------------------------------------------------------------

using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Reflection;

/// <summary>
/// Tests for <see cref="GravityFlipPowerUp"/> ensuring gravity inversion is
/// applied through <see cref="GameManager"/> and that audio, rumble and pooling
/// behave as expected. Edge cases cover missing managers.
/// </summary>
public class GravityFlipPowerUpTests
{
    /// <summary>
    /// Lightweight GameManager used to capture state without running the full
    /// awake logic from the production singleton.
    /// </summary>
    private class TestGameManager : GameManager
    {
        new void Awake()
        {
            Instance = this;
        }
    }

    /// <summary>
    /// Collecting the power-up should flip gravity via the GameManager, play
    /// audio, start rumble and return the object to its pool.
    /// </summary>
    [Test]
    public void OnTriggerEnter2D_FlipsGravityAndReturnsToPool()
    {
        // -----------------------------------------------------------------
        // Arrange: capture current gravity and build minimal scene objects.
        // -----------------------------------------------------------------
        Vector2 originalGravity = Physics2D.gravity; // remember to restore

        var audioObj = new GameObject("audio");
        var am = audioObj.AddComponent<AudioManager>();
        am.effectsSource = audioObj.AddComponent<AudioSource>();
        am.musicSource = audioObj.AddComponent<AudioSource>();
        am.musicSourceSecondary = audioObj.AddComponent<AudioSource>();

        var gmObj = new GameObject("gm");
        var gm = gmObj.AddComponent<TestGameManager>();

        var pad = InputSystem.AddDevice<Gamepad>();
        InputManager.SetRumbleEnabled(true);

        var player = new GameObject("player");
        player.tag = "Player";
        var playerCollider = player.AddComponent<CapsuleCollider2D>();

        var poolObj = new GameObject("pool");
        var pool = poolObj.AddComponent<ObjectPool>();

        var powerObj = new GameObject("power");
        var gp = powerObj.AddComponent<GravityFlipPowerUp>();
        gp.collectClip = AudioClip.Create("pickup", 44100, 1, 44100, false);
        var po = powerObj.AddComponent<PooledObject>();
        po.Pool = pool;
        var col = powerObj.AddComponent<BoxCollider2D>();
        col.isTrigger = true;

        // -----------------------------------------------------------------
        // Act: simulate the player colliding with the power-up.
        // -----------------------------------------------------------------
        gp.OnTriggerEnter2D(playerCollider);

        // -----------------------------------------------------------------
        // Assert: gravity inversion, feedback, and pooling.
        // -----------------------------------------------------------------
        // Gravity should be inverted and internal timer set; reflection checks
        // the private GameManager state.
        float timer = (float)typeof(GameManager)
            .GetField("gravityFlipTimer", BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(gm);
        Assert.AreEqual(gp.duration, timer, "Gravity flip duration should match power-up value");
        Assert.AreEqual(-originalGravity.y, Physics2D.gravity.y, 0.001f,
            "Global gravity should be inverted");

        Assert.IsTrue(am.effectsSource.isPlaying, "Collect sound should play");
        var routine = typeof(InputManager)
            .GetField("rumbleRoutine", BindingFlags.NonPublic | BindingFlags.Static)
            .GetValue(null);
        Assert.IsNotNull(routine, "Rumble should start on collection");
        Assert.IsFalse(powerObj.activeSelf, "Returned power-up should be inactive");
        Assert.AreEqual(pool.transform, powerObj.transform.parent,
            "Returned power-up should be parented to its pool");

        // Cleanup
        Physics2D.gravity = originalGravity;
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
    /// When no <see cref="GameManager"/> exists the power-up should avoid
    /// altering gravity, play feedback and destroy itself because no pool is
    /// assigned.
    /// </summary>
    [Test]
    public void OnTriggerEnter2D_NoGameManager_DestroysPowerUp()
    {
        // -----------------------------------------------------------------
        // Arrange: lack a GameManager or pool to ensure the power-up cleans up
        // by destroying itself while leaving gravity unaffected.
        // -----------------------------------------------------------------
        Vector2 originalGravity = Physics2D.gravity;

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
        var gp = powerObj.AddComponent<GravityFlipPowerUp>();
        gp.collectClip = AudioClip.Create("pickup", 44100, 1, 44100, false);
        var col = powerObj.AddComponent<BoxCollider2D>();
        col.isTrigger = true;

        // -----------------------------------------------------------------
        // Act: simulate collection without the required manager.
        // -----------------------------------------------------------------
        gp.OnTriggerEnter2D(playerCollider);

        // -----------------------------------------------------------------
        // Assert: object destroys itself, gravity unchanged, feedback plays.
        // -----------------------------------------------------------------
        Assert.IsTrue(powerObj == null, "Power-up should destroy itself without GameManager");
        Assert.AreEqual(originalGravity.y, Physics2D.gravity.y, 0.001f,
            "Gravity should remain unchanged when GameManager is missing");
        Assert.IsTrue(am.effectsSource.isPlaying, "Sound should play even without GameManager");
        var routine = typeof(InputManager)
            .GetField("rumbleRoutine", BindingFlags.NonPublic | BindingFlags.Static)
            .GetValue(null);
        Assert.IsNotNull(routine, "Rumble should trigger even without GameManager");

        // Cleanup
        Physics2D.gravity = originalGravity;
        InputManager.Shutdown();
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(
            typeof(InputManager).TypeHandle);
        InputSystem.RemoveDevice(pad);
        Object.DestroyImmediate(player);
        Object.DestroyImmediate(audioObj);
    }
}

