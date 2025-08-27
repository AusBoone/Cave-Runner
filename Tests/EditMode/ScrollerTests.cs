using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Collections;
using System.Reflection;

/// <summary>
/// Tests for the <see cref="Scroller"/> component verifying that objects move
/// only while the game is running and that pooled instances reset their
/// position when reused.
/// </summary>
public class ScrollerTests
{
    /// <summary>
    /// GameManager stub exposing helpers to control the running state and
    /// current speed used by <see cref="GameManager.GetSpeed"/>.
    /// </summary>
    private class MockGameManager : GameManager
    {
        public new void Awake()
        {
            typeof(GameManager).GetProperty("Instance", BindingFlags.Static | BindingFlags.Public)
                .SetValue(null, this, null);
        }

        public void SetRunning(bool running)
        {
            typeof(GameManager).GetField("isRunning", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(this, running);
        }

        public void SetSpeed(float speed)
        {
            typeof(GameManager).GetField("currentSpeed", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(this, speed);
        }
    }

    /// <summary>
    /// Validates that the scroller does not translate when the game is stopped
    /// but begins moving left once the GameManager reports an active run.
    /// </summary>
    [UnityTest]
    public IEnumerator Update_MovesOnlyWhenRunning()
    {
        var gmObj = new GameObject("gm");
        var gm = gmObj.AddComponent<MockGameManager>();
        gm.Awake();
        gm.SetRunning(false);
        gm.SetSpeed(5f); // speed ignored when not running

        var obj = new GameObject("scroll");
        obj.transform.position = Vector3.zero;
        obj.AddComponent<Scroller>();

        // Frame with game stopped - position should remain unchanged.
        yield return null;
        Assert.That(obj.transform.position.x, Is.EqualTo(0f),
            "Scroller moved despite game not running");

        // Enable running and verify movement.
        gm.SetRunning(true);
        yield return null;
        Assert.Less(obj.transform.position.x, 0f,
            "Scroller failed to move left when game running");

        Object.DestroyImmediate(gmObj);
        Object.DestroyImmediate(obj);
    }

    /// <summary>
    /// Ensures a scroller returned to an <see cref="ObjectPool"/> has its
    /// position reset when the pool hands it out again.
    /// </summary>
    [UnityTest]
    public IEnumerator PooledScroller_ResetsPositionOnReuse()
    {
        var gmObj = new GameObject("gm");
        var gm = gmObj.AddComponent<MockGameManager>();
        gm.Awake();
        gm.SetRunning(true);
        gm.SetSpeed(50f); // high speed so object quickly exits

        // Create pool and prefab that includes Scroller and PooledObject.
        var prefab = new GameObject("prefab");
        prefab.AddComponent<PooledObject>();
        prefab.AddComponent<Scroller>();

        var poolObj = new GameObject("pool");
        var pool = poolObj.AddComponent<ObjectPool>();
        pool.prefab = prefab;
        pool.initialSize = 0;

        // Spawn an instance and manually move it off-screen to trigger pooling.
        var instance = pool.GetObject(Vector3.zero, Quaternion.identity);
        instance.transform.position = new Vector3(-21f, 0f, 0f);
        yield return null; // allow Update to return it to the pool
        Assert.IsFalse(instance.activeSelf, "Instance should be inactive after being pooled");

        // Retrieve the same instance at a new position; it should adopt the new location.
        var reused = pool.GetObject(new Vector3(5f, 0f, 0f), Quaternion.identity);
        Assert.AreEqual(new Vector3(5f, 0f, 0f), reused.transform.position,
            "Pooled scroller did not reset position when reused");

        Object.DestroyImmediate(gmObj);
        Object.DestroyImmediate(prefab);
        Object.DestroyImmediate(poolObj);
    }
}

