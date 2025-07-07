using NUnit.Framework;
using UnityEngine;
using System.Reflection;

/// <summary>
/// Unit tests for <see cref="AdaptiveDifficultyManager"/> verifying that spawn
/// multipliers adjust based on analytics data.
/// </summary>
public class AdaptiveDifficultyManagerTests
{
    [SetUp]
    public void ClearPrefs()
    {
        PlayerPrefs.DeleteAll();
    }

    [Test]
    public void AdjustDifficulty_IncreasesWhenAverageHigh()
    {
        // Setup analytics with runs that greatly exceed the target distance
        var analyticsObj = new GameObject("am");
        var am = analyticsObj.AddComponent<AnalyticsManager>();
        am.LogRun(1000f, 0, true);
        am.LogRun(1200f, 0, true);

        var obstacleObj = new GameObject("obs");
        var obstacle = obstacleObj.AddComponent<ObstacleSpawner>();
        var hazardObj = new GameObject("haz");
        var hazard = hazardObj.AddComponent<HazardSpawner>();

        var diffObj = new GameObject("diff");
        var diff = diffObj.AddComponent<AdaptiveDifficultyManager>();
        diff.targetDistance = 500f;
        diff.RegisterSpawners(obstacle, hazard);
        diff.AdjustDifficulty();

        Assert.Greater(obstacle.spawnMultiplier, 1f);
        Assert.AreEqual(obstacle.spawnMultiplier, hazard.spawnMultiplier);

        Object.DestroyImmediate(diffObj);
        Object.DestroyImmediate(obstacleObj);
        Object.DestroyImmediate(hazardObj);
        Object.DestroyImmediate(analyticsObj);
    }

    [Test]
    public void AdjustDifficulty_DecreasesWhenAverageLow()
    {
        // Setup analytics with short runs below the target distance
        var analyticsObj = new GameObject("am");
        var am = analyticsObj.AddComponent<AnalyticsManager>();
        am.LogRun(50f, 0, true);
        am.LogRun(60f, 0, true);

        var obstacleObj = new GameObject("obs");
        var obstacle = obstacleObj.AddComponent<ObstacleSpawner>();
        var hazardObj = new GameObject("haz");
        var hazard = hazardObj.AddComponent<HazardSpawner>();

        var diffObj = new GameObject("diff");
        var diff = diffObj.AddComponent<AdaptiveDifficultyManager>();
        diff.targetDistance = 500f;
        diff.RegisterSpawners(obstacle, hazard);
        diff.AdjustDifficulty();

        Assert.Less(obstacle.spawnMultiplier, 1f);
        Assert.AreEqual(obstacle.spawnMultiplier, hazard.spawnMultiplier);

        Object.DestroyImmediate(diffObj);
        Object.DestroyImmediate(obstacleObj);
        Object.DestroyImmediate(hazardObj);
        Object.DestroyImmediate(analyticsObj);
    }

    /// <summary>
    /// Ensures a high coin combo from the previous run slightly
    /// increases spawn multipliers even when distance is average.
    /// </summary>
    [Test]
    public void AdjustDifficulty_IncludesComboBonus()
    {
        var analyticsObj = new GameObject("am");
        var am = analyticsObj.AddComponent<AnalyticsManager>();
        am.LogRun(500f, 0, true); // at target distance

        var gmObj = new GameObject("gm");
        var gm = gmObj.AddComponent<GameManager>();
        typeof(GameManager).GetField("coinComboMultiplier", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(gm, 5); // simulate high combo

        var obstacleObj = new GameObject("obs");
        var obstacle = obstacleObj.AddComponent<ObstacleSpawner>();
        var hazardObj = new GameObject("haz");
        var hazard = hazardObj.AddComponent<HazardSpawner>();

        var diffObj = new GameObject("diff");
        var diff = diffObj.AddComponent<AdaptiveDifficultyManager>();
        diff.targetDistance = 500f;
        diff.RegisterSpawners(obstacle, hazard);
        diff.AdjustDifficulty();

        float expected = 1f + (5 - 1) * 0.05f;
        Assert.AreEqual(expected, obstacle.spawnMultiplier, 0.0001f,
            "Combo multiplier should add a small bonus to the spawn multiplier");
        Assert.AreEqual(expected, hazard.spawnMultiplier, 0.0001f);

        Object.DestroyImmediate(diffObj);
        Object.DestroyImmediate(obstacleObj);
        Object.DestroyImmediate(hazardObj);
        Object.DestroyImmediate(gmObj);
        Object.DestroyImmediate(analyticsObj);
    }

    /// <summary>
    /// Verifies combo scaling respects the max multiplier limit.
    /// </summary>
    [Test]
    public void AdjustDifficulty_ComboBonusClampedByMax()
    {
        var analyticsObj = new GameObject("am");
        var am = analyticsObj.AddComponent<AnalyticsManager>();
        am.LogRun(500f, 0, true);

        var gmObj = new GameObject("gm");
        var gm = gmObj.AddComponent<GameManager>();
        typeof(GameManager).GetField("coinComboMultiplier", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(gm, 20); // extremely high combo

        var obstacleObj = new GameObject("obs");
        var obstacle = obstacleObj.AddComponent<ObstacleSpawner>();
        var hazardObj = new GameObject("haz");
        var hazard = hazardObj.AddComponent<HazardSpawner>();

        var diffObj = new GameObject("diff");
        var diff = diffObj.AddComponent<AdaptiveDifficultyManager>();
        diff.targetDistance = 500f;
        diff.maxMultiplier = 1.1f; // tight upper bound
        diff.RegisterSpawners(obstacle, hazard);
        diff.AdjustDifficulty();

        Assert.AreEqual(1.1f, obstacle.spawnMultiplier, 0.0001f,
            "Multiplier should not exceed configured maximum");
        Assert.AreEqual(1.1f, hazard.spawnMultiplier, 0.0001f);

        Object.DestroyImmediate(diffObj);
        Object.DestroyImmediate(obstacleObj);
        Object.DestroyImmediate(hazardObj);
        Object.DestroyImmediate(gmObj);
        Object.DestroyImmediate(analyticsObj);
    }

    [TearDown]
    public void ResetSingleton()
    {
        typeof(AdaptiveDifficultyManager).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)
            .SetValue(null, null);
    }
}

