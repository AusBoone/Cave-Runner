using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Tests for core GameManager functionality such as coin counting and
/// speed boosts. Executed through Unity's EditMode Test Runner.
/// </summary>

// EditMode tests can be run through Unity's Test Runner window.
// Create this file under Assets/Tests/EditMode and open Window > General > Test Runner.
// Select EditMode and run the tests.

public class GameManagerTests
{
    [Test]
    public void AddCoins_IncreasesTotal()
    {
        // Create a temporary GameManager instance
        var go = new GameObject("gm");
        var gm = go.AddComponent<GameManager>();

        gm.AddCoins(2);
        gm.AddCoins(3);

        // Total coins should equal the sum added
        Assert.AreEqual(5, gm.GetCoins());
        Object.DestroyImmediate(go);
    }

    [Test]
    public void ActivateSpeedBoost_MultipliesSpeed()
    {
        // GameManager must be running for speed boosts to apply
        var go = new GameObject("gm");
        var gm = go.AddComponent<GameManager>();
        gm.StartGame();

        var baseSpeed = gm.GetSpeed();
        gm.ActivateSpeedBoost(1f, 2f); // double the speed for one second
        // Verify the multiplier applied immediately
        Assert.AreEqual(baseSpeed * 2f, gm.GetSpeed());
        Object.DestroyImmediate(go);
    }
}
