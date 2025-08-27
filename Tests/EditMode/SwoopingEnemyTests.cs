using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Collections;
using System.Reflection;

/// <summary>
/// Unit tests validating <see cref="SwoopingEnemy"/> movement behaviour and
/// its ability to reset internal state when reused from a pool.
/// </summary>
public class SwoopingEnemyTests
{
    /// <summary>
    /// Lightweight GameManager stub used to manipulate the running state in
    /// isolation from the rest of the game systems.
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
    }

    /// <summary>
    /// Ensures the enemy remains stationary when the game is not running and
    /// begins its swooping motion once gameplay starts.
    /// </summary>
    [UnityTest]
    public IEnumerator Update_MovesOnlyWhenRunning()
    {
        var gmObj = new GameObject("gm");
        var gm = gmObj.AddComponent<MockGameManager>();
        gm.Awake();
        gm.SetRunning(false);

        var enemyObj = new GameObject("enemy");
        enemyObj.transform.position = Vector3.zero;
        var enemy = enemyObj.AddComponent<SwoopingEnemy>();
        enemy.speed = 1f;
        enemy.amplitude = 1f;
        enemy.duration = 1f;
        enemy.OnEnable();

        // With the game stopped no movement should occur.
        yield return null;
        Assert.That(enemyObj.transform.position, Is.EqualTo(Vector3.zero),
            "Enemy moved despite game not running");

        // Start the game and verify movement begins.
        gm.SetRunning(true);
        float startX = enemyObj.transform.position.x;
        yield return null;
        Assert.Less(enemyObj.transform.position.x, startX,
            "Enemy failed to translate left when running");

        Object.DestroyImmediate(gmObj);
        Object.DestroyImmediate(enemyObj);
    }

    /// <summary>
    /// Confirms that disabling and re-enabling the enemy (as occurs when using an
    /// object pool) resets its starting position and timer so each spawn begins
    /// the swoop from the top of its arc.
    /// </summary>
    [UnityTest]
    public IEnumerator PooledEnemy_ResetsOnReuse()
    {
        var gmObj = new GameObject("gm");
        var gm = gmObj.AddComponent<MockGameManager>();
        gm.Awake();
        gm.SetRunning(true);

        var enemyObj = new GameObject("enemy");
        enemyObj.transform.position = Vector3.zero;
        var enemy = enemyObj.AddComponent<SwoopingEnemy>();
        enemy.speed = 1f;
        enemy.amplitude = 1f;
        enemy.duration = 1f;
        enemy.OnEnable();

        // Let the enemy move for a frame then deactivate it to simulate pooling.
        yield return null;
        enemy.OnDisable();

        // Move to a new spawn location and re-enable; OnEnable should treat this
        // as the new starting position and reset the timer.
        enemyObj.transform.position = new Vector3(10f, 10f, 0f);
        enemy.OnEnable();
        Assert.AreEqual(new Vector3(10f, 10f, 0f), enemyObj.transform.position,
            "Enemy did not reset to new start position on enable");

        // After a frame the enemy should begin moving from the new location.
        float resetX = enemyObj.transform.position.x;
        yield return null;
        Assert.Less(enemyObj.transform.position.x, resetX,
            "Enemy failed to move after reactivation");

        Object.DestroyImmediate(gmObj);
        Object.DestroyImmediate(enemyObj);
    }
}

