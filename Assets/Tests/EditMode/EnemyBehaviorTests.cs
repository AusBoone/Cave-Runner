// EnemyBehaviorTests.cs
// -----------------------------------------------------------------------------
// Validates behaviour of EnemyBehavior including rotation-independent
// movement and the requirement for an explicitly assigned target. The tests
// simulate Unity's game loop by manually invoking Update after configuring a
// running GameManager and providing target transforms.
// -----------------------------------------------------------------------------
using NUnit.Framework;
using UnityEngine;
using System.Reflection;

/// <summary>
/// Test suite verifying EnemyBehavior functionality.
/// </summary>
public class EnemyBehaviorTests
{
    /// <summary>
    /// Ensures that two enemies, one rotated and one not, move identically
    /// toward the player, demonstrating that movement calculations occur in
    /// world space and are unaffected by the enemy's orientation.
    /// </summary>
    [Test]
    public void Update_MovementIndependentOfRotation()
    {
        // Create a running GameManager so EnemyBehavior.Update processes
        // movement. The field is private so reflection is used to set it.
        var gmObj = new GameObject("gm");
        var gm = gmObj.AddComponent<GameManager>();
        typeof(GameManager).GetField("isRunning", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(gm, true);

        // Create a player target positioned to the right of the origin.
        var player = new GameObject("player");
        player.transform.position = new Vector3(5f, 0f, 0f);

        // Create two enemies at the origin, rotating one by 90 degrees.
        var enemyA = new GameObject("enemyA");
        var behaviorA = enemyA.AddComponent<EnemyBehavior>();
        var enemyB = new GameObject("enemyB");
        enemyB.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
        var behaviorB = enemyB.AddComponent<EnemyBehavior>();

        // Assign the shared player target using the new SetTarget API instead of
        // relying on a tag lookup.
        behaviorA.SetTarget(player.transform);
        behaviorB.SetTarget(player.transform);

        // Invoke Update on both behaviors to move them toward the player.
        behaviorA.Update();
        behaviorB.Update();

        // Both enemies should occupy the same world position despite the
        // rotation applied to enemyB, proving movement uses world coordinates.
        Assert.AreEqual(enemyA.transform.position, enemyB.transform.position);

        // Clean up all dynamically created objects to avoid polluting other tests.
        Object.DestroyImmediate(enemyA);
        Object.DestroyImmediate(enemyB);
        Object.DestroyImmediate(player);
        Object.DestroyImmediate(gmObj);
    }

    /// <summary>
    /// Verifies that enemies only move when provided a valid target via
    /// <see cref="EnemyBehavior.SetTarget"/> and remain stationary otherwise.
    /// </summary>
    [Test]
    public void Update_MovementRequiresTarget()
    {
        // Create a running GameManager to satisfy the update checks.
        var gmObj = new GameObject("gm");
        var gm = gmObj.AddComponent<GameManager>();
        typeof(GameManager).GetField("isRunning", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(gm, true);

        // Player transform located one unit to the right.
        var player = new GameObject("player");
        player.transform.position = Vector3.right;

        // Enemy that receives a target and should therefore move.
        var enemyWithTarget = new GameObject("enemyWithTarget");
        var behaviorWithTarget = enemyWithTarget.AddComponent<EnemyBehavior>();
        behaviorWithTarget.SetTarget(player.transform);

        // Enemy left without a target which should stay idle.
        var enemyWithoutTarget = new GameObject("enemyWithoutTarget");
        var behaviorWithoutTarget = enemyWithoutTarget.AddComponent<EnemyBehavior>();

        // Perform a single update cycle for both enemies.
        behaviorWithTarget.Update();
        behaviorWithoutTarget.Update();

        // Enemy with a target should have moved away from the origin whereas the
        // idle enemy should remain in place.
        Assert.AreNotEqual(Vector3.zero, enemyWithTarget.transform.position,
            "Enemy provided a target is expected to move toward it.");
        Assert.AreEqual(Vector3.zero, enemyWithoutTarget.transform.position,
            "Enemy without a target should remain stationary.");

        // Clean up spawned objects.
        Object.DestroyImmediate(enemyWithTarget);
        Object.DestroyImmediate(enemyWithoutTarget);
        Object.DestroyImmediate(player);
        Object.DestroyImmediate(gmObj);
    }
}
