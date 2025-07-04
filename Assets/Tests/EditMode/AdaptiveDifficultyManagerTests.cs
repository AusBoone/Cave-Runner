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

    [TearDown]
    public void ResetSingleton()
    {
        typeof(AdaptiveDifficultyManager).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)
            .SetValue(null, null);
    }
}

