using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Collections;
using System.Reflection;

/// <summary>
/// Tests for <see cref="RotatingHazard"/> validating that spinning occurs only
/// while the game is running. These expectations mirror other movement scripts
/// in the project which pause their behaviour when <see cref="GameManager"/>
/// reports an inactive state.
/// </summary>
public class RotatingHazardTests
{
    /// <summary>
    /// Minimal GameManager allowing tests to flip the running state without
    /// executing the full production Awake logic.
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
    /// Confirms the hazard remains stationary when the game is not running and
    /// resumes rotation once gameplay continues.
    /// </summary>
    [UnityTest]
    public IEnumerator Update_RotatesOnlyWhenRunning()
    {
        var gmObj = new GameObject("gm");
        var gm = gmObj.AddComponent<MockGameManager>();
        gm.Awake();
        gm.SetRunning(false);

        var hazardObj = new GameObject("hazard");
        var hazard = hazardObj.AddComponent<RotatingHazard>();
        hazard.rotationSpeed = 90f;

        // With the game stopped the rotation should not change.
        float before = hazardObj.transform.eulerAngles.z;
        hazard.Update();
        Assert.That(hazardObj.transform.eulerAngles.z, Is.EqualTo(before),
            "Hazard rotated while game not running");

        // Enable gameplay and verify rotation now occurs.
        gm.SetRunning(true);
        before = hazardObj.transform.eulerAngles.z;
        hazard.Update();
        Assert.That(hazardObj.transform.eulerAngles.z, Is.Not.EqualTo(before),
            "Hazard failed to rotate when game running");

        Object.DestroyImmediate(gmObj);
        Object.DestroyImmediate(hazardObj);
        yield return null; // satisfy UnityTest signature
    }
}

