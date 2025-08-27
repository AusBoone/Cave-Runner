// -----------------------------------------------------------------------------
// MagnetPowerUpTests.cs
// -----------------------------------------------------------------------------
// These edit-mode tests exercise the MagnetPowerUp component in isolation. Each
// case spawns lightweight GameObjects that mimic the player and required
// managers, allowing us to simulate collection events without running the full
// game. The tests verify that the power-up activates the player's CoinMagnet,
// plays feedback (audio and rumble) and either returns itself to an object pool
// or destroys itself when pooling is unavailable. Reflection is used to inspect
// private fields on components to ensure internal timers are set correctly.
// -----------------------------------------------------------------------------

using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Reflection;

/// <summary>
/// Edit mode tests validating <see cref="MagnetPowerUp"/> behaviour.
/// Each test constructs minimal GameObjects to simulate the player
/// collecting the power-up and verifies side effects such as magnet
/// activation, audio playback, rumble feedback and pooling logic.
/// Edge cases cover missing required components.
/// </summary>
public class MagnetPowerUpTests
{
    /// <summary>
    /// Verifies that collecting the magnet power-up activates the player's
    /// <see cref="CoinMagnet"/> component, plays the pickup sound, triggers
    /// rumble and returns the object to its pool.
    /// </summary>
    [Test]
    public void OnTriggerEnter2D_ActivatesMagnetAndReturnsToPool()
    {
        // -----------------------------------------------------------------
        // Arrange: create the minimal scene with audio, input, player and pool.
        // -----------------------------------------------------------------
        // AudioManager instance required for sound playback.
        var audioObj = new GameObject("audio");
        var am = audioObj.AddComponent<AudioManager>();
        am.effectsSource = audioObj.AddComponent<AudioSource>();
        am.musicSource = audioObj.AddComponent<AudioSource>();
        am.musicSourceSecondary = audioObj.AddComponent<AudioSource>();

        // Register a dummy gamepad so InputManager can start a rumble coroutine.
        var pad = InputSystem.AddDevice<Gamepad>();
        InputManager.SetRumbleEnabled(true);

        // Player tagged correctly and equipped with CoinMagnet + collider.
        var player = new GameObject("player");
        player.tag = "Player";
        var playerCollider = player.AddComponent<CapsuleCollider2D>();
        var magnet = player.AddComponent<CoinMagnet>();

        // Pool used to recycle the power-up after collection.
        var poolObj = new GameObject("pool");
        var pool = poolObj.AddComponent<ObjectPool>();

        // Power-up under test with required components.
        var powerObj = new GameObject("power");
        var mp = powerObj.AddComponent<MagnetPowerUp>();
        mp.collectClip = AudioClip.Create("pickup", 44100, 1, 44100, false);
        var po = powerObj.AddComponent<PooledObject>();
        po.Pool = pool;
        var col = powerObj.AddComponent<BoxCollider2D>();
        col.isTrigger = true;

        // -----------------------------------------------------------------
        // Act: simulate collision from the player.
        // -----------------------------------------------------------------
        mp.OnTriggerEnter2D(playerCollider);

        // -----------------------------------------------------------------
        // Assert: verify all expected side effects occurred.
        // -----------------------------------------------------------------
        // CoinMagnet should be active for the configured duration. We access
        // the private timer field via reflection to confirm it matches.
        float timer = (float)typeof(CoinMagnet)
            .GetField("magnetTimer", BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(magnet);
        Assert.AreEqual(mp.duration, timer, "Magnet duration should match power-up value");

        // AudioManager should begin playing the assigned clip.
        Assert.IsTrue(am.effectsSource.isPlaying, "Collect sound should play");

        // InputManager should have started a rumble coroutine.
        var routine = typeof(InputManager)
            .GetField("rumbleRoutine", BindingFlags.NonPublic | BindingFlags.Static)
            .GetValue(null);
        Assert.IsNotNull(routine, "Rumble should start on collection");

        // Power-up should be returned to the pool and deactivated.
        Assert.IsFalse(powerObj.activeSelf, "Returned power-up should be inactive");
        Assert.AreEqual(pool.transform, powerObj.transform.parent,
            "Returned power-up should be parented to its pool");

        // Cleanup to avoid polluting other tests.
        InputManager.Shutdown();
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(
            typeof(InputManager).TypeHandle);
        InputSystem.RemoveDevice(pad);
        Object.DestroyImmediate(player);
        Object.DestroyImmediate(powerObj);
        Object.DestroyImmediate(poolObj);
        Object.DestroyImmediate(audioObj);
    }

    /// <summary>
    /// Ensures the power-up handles a player lacking the required
    /// <see cref="CoinMagnet"/> component by still playing feedback and
    /// destroying itself when no pool is assigned.
    /// </summary>
    [Test]
    public void OnTriggerEnter2D_MissingCoinMagnet_DestroysPowerUp()
    {
        // -----------------------------------------------------------------
        // Arrange: setup with a player lacking the CoinMagnet component and no
        // object pool so the power-up must destroy itself after pickup.
        // -----------------------------------------------------------------
        var audioObj = new GameObject("audio");
        var am = audioObj.AddComponent<AudioManager>();
        am.effectsSource = audioObj.AddComponent<AudioSource>();
        am.musicSource = audioObj.AddComponent<AudioSource>();
        am.musicSourceSecondary = audioObj.AddComponent<AudioSource>();

        var pad = InputSystem.AddDevice<Gamepad>();
        InputManager.SetRumbleEnabled(true);

        // Player lacks CoinMagnet component which is required for normal
        // operation.
        var player = new GameObject("player");
        player.tag = "Player";
        var playerCollider = player.AddComponent<CapsuleCollider2D>();

        // Power-up without pool so it should destroy itself after collection.
        var powerObj = new GameObject("power");
        var mp = powerObj.AddComponent<MagnetPowerUp>();
        mp.collectClip = AudioClip.Create("pickup", 44100, 1, 44100, false);
        var col = powerObj.AddComponent<BoxCollider2D>();
        col.isTrigger = true;

        // -----------------------------------------------------------------
        // Act: simulate the player colliding with the power-up.
        // -----------------------------------------------------------------
        mp.OnTriggerEnter2D(playerCollider);

        // -----------------------------------------------------------------
        // Assert: the power-up should schedule itself for destruction yet still
        // provide feedback to the player.
        // -----------------------------------------------------------------
        Assert.IsTrue(powerObj == null, "Power-up should destroy itself when unpooled");

        // Feedback should still occur to maintain consistent UX.
        Assert.IsTrue(am.effectsSource.isPlaying, "Sound should play even without CoinMagnet");
        var routine = typeof(InputManager)
            .GetField("rumbleRoutine", BindingFlags.NonPublic | BindingFlags.Static)
            .GetValue(null);
        Assert.IsNotNull(routine, "Rumble should trigger even without CoinMagnet");

        // Cleanup.
        InputManager.Shutdown();
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(
            typeof(InputManager).TypeHandle);
        InputSystem.RemoveDevice(pad);
        Object.DestroyImmediate(player);
        Object.DestroyImmediate(audioObj);
    }
}

