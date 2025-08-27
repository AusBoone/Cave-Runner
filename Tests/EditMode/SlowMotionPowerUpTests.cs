// -----------------------------------------------------------------------------
// SlowMotionPowerUpTests.cs
// -----------------------------------------------------------------------------
// Ensures the SlowMotionPowerUp component correctly manipulates the game's time
// scale through GameManager and triggers associated feedback mechanisms. These
// edit-mode tests rely on lightweight stand-ins for required managers and the
// player so behaviour can be validated without running the full game loop.
// Reflection reads private GameManager fields to confirm timer and scale values.
// Edge cases verify graceful handling when GameManager is absent.
// -----------------------------------------------------------------------------

using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Reflection;

/// <summary>
/// Tests for <see cref="SlowMotionPowerUp"/> verifying that slow motion is
/// applied via <see cref="GameManager"/> and that audio, rumble and pooling
/// behave correctly. Edge cases ensure missing managers are handled gracefully.
/// </summary>
public class SlowMotionPowerUpTests
{
    /// <summary>
    /// Minimal GameManager used to capture state changes without executing the
    /// full Awake routine.
    /// </summary>
    private class TestGameManager : GameManager
    {
        new void Awake()
        {
            Instance = this;
        }
    }

    /// <summary>
    /// Collecting the power-up should call <see cref="GameManager.ActivateSlowMotion"/>
    /// with the correct parameters, play a sound, trigger rumble and return to
    /// the pool.
    /// </summary>
    [Test]
    public void OnTriggerEnter2D_ActivatesSlowMotionAndReturnsToPool()
    {
        // -----------------------------------------------------------------
        // Arrange: record current time scale and create minimal objects.
        // -----------------------------------------------------------------
        float originalScale = Time.timeScale;

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
        var sp = powerObj.AddComponent<SlowMotionPowerUp>();
        sp.collectClip = AudioClip.Create("pickup", 44100, 1, 44100, false);
        var po = powerObj.AddComponent<PooledObject>();
        po.Pool = pool;
        var col = powerObj.AddComponent<BoxCollider2D>();
        col.isTrigger = true;

        // -----------------------------------------------------------------
        // Act: simulate collision to activate slow motion.
        // -----------------------------------------------------------------
        sp.OnTriggerEnter2D(playerCollider);

        // -----------------------------------------------------------------
        // Assert: GameManager state, global time scale, feedback and pooling.
        // -----------------------------------------------------------------
        // Verify GameManager updated its internal timers and scale via reflection.
        float timer = (float)typeof(GameManager)
            .GetField("slowMotionTimer", BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(gm);
        float scale = (float)typeof(GameManager)
            .GetField("slowMotionScale", BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(gm);
        Assert.AreEqual(sp.duration, timer, "Slow motion duration should match power-up value");
        Assert.AreEqual(sp.timeScale, scale, "Slow motion scale should match power-up value");
        Assert.AreEqual(sp.timeScale, Time.timeScale, 0.0001f,
            "Global time scale should reflect power-up setting");

        Assert.IsTrue(am.effectsSource.isPlaying, "Collect sound should play");
        var routine = typeof(InputManager)
            .GetField("rumbleRoutine", BindingFlags.NonPublic | BindingFlags.Static)
            .GetValue(null);
        Assert.IsNotNull(routine, "Rumble should start on collection");
        Assert.IsFalse(powerObj.activeSelf, "Returned power-up should be inactive");
        Assert.AreEqual(pool.transform, powerObj.transform.parent,
            "Returned power-up should be parented to its pool");

        // Cleanup
        Time.timeScale = originalScale;
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
    /// Without a <see cref="GameManager"/> the power-up should still play
    /// feedback but destroy itself and leave time scale unchanged.
    /// </summary>
    [Test]
    public void OnTriggerEnter2D_NoGameManager_DestroysPowerUp()
    {
        // -----------------------------------------------------------------
        // Arrange: no GameManager or pool to force self-destruction while
        // leaving the global time scale intact.
        // -----------------------------------------------------------------
        float originalScale = Time.timeScale;

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
        var sp = powerObj.AddComponent<SlowMotionPowerUp>();
        sp.collectClip = AudioClip.Create("pickup", 44100, 1, 44100, false);
        var col = powerObj.AddComponent<BoxCollider2D>();
        col.isTrigger = true;

        // -----------------------------------------------------------------
        // Act: simulate collection without GameManager involvement.
        // -----------------------------------------------------------------
        sp.OnTriggerEnter2D(playerCollider);

        // -----------------------------------------------------------------
        // Assert: object destroyed, time scale unchanged, feedback triggered.
        // -----------------------------------------------------------------
        Assert.IsTrue(powerObj == null, "Power-up should destroy itself without GameManager");
        Assert.AreEqual(originalScale, Time.timeScale, 0.0001f,
            "Time scale should remain unchanged when GameManager is missing");
        Assert.IsTrue(am.effectsSource.isPlaying, "Sound should play even without GameManager");
        var routine = typeof(InputManager)
            .GetField("rumbleRoutine", BindingFlags.NonPublic | BindingFlags.Static)
            .GetValue(null);
        Assert.IsNotNull(routine, "Rumble should trigger even without GameManager");

        // Cleanup
        Time.timeScale = originalScale;
        InputManager.Shutdown();
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(
            typeof(InputManager).TypeHandle);
        InputSystem.RemoveDevice(pad);
        Object.DestroyImmediate(player);
        Object.DestroyImmediate(audioObj);
    }
}

