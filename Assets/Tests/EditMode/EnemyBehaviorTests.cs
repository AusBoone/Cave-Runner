// EnemyBehaviorTests.cs
// -----------------------------------------------------------------------------
// Validates that EnemyBehavior moves enemies toward the player using world
// coordinates, ensuring rotation does not alter the pursuit path.
// -----------------------------------------------------------------------------
using NUnit.Framework;
using UnityEngine;
using System.Reflection;

/// <summary>
/// Test suite verifying the rotation-independent movement of EnemyBehavior.
/// </summary>
public class EnemyBehaviorTests
{
    /// <summary>
    /// Ensures that two enemies, one rotated and one not, move identically
    /// toward the player, demonstrating that movement calculations occur in
    /// world space.
    /// </summary>
    [Test]
    public void Update_MovementIndependentOfRotation()
    {
        // Create a player target positioned to the right of the origin.
        var player = new GameObject("player");
        player.transform.position = new Vector3(5f, 0f, 0f);

        // Create two enemies at the origin, rotating one by 90 degrees.
        var enemyA = new GameObject("enemyA");
        var behaviorA = enemyA.AddComponent<EnemyBehavior>();
        var enemyB = new GameObject("enemyB");
        enemyB.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
        var behaviorB = enemyB.AddComponent<EnemyBehavior>();

        // Manually assign the private player field via reflection so Update can
        // execute without invoking Start().
        var field = typeof(EnemyBehavior).GetField("player", BindingFlags.NonPublic | BindingFlags.Instance);
        field.SetValue(behaviorA, player.transform);
        field.SetValue(behaviorB, player.transform);

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
    }
}
