using NUnit.Framework;
using UnityEngine;
using System.Reflection;
using System; // Needed for ArgumentOutOfRangeException assertions

/// <summary>
/// Unit tests for <see cref="AdaptiveDifficultyManager"/> verifying that spawn
/// multipliers adjust based on analytics data and designer-tuned combo bonus
/// settings.
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

        // Retrieve the default combo bonus step via reflection so the test stays
        // in sync if designers tweak the serialized value.
        float step = (float)typeof(AdaptiveDifficultyManager)
            .GetField("comboBonusStep", BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(diff);
        float expected = 1f + (5 - 1) * step;
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
    /// Ensures Awake throws clear exceptions when any serialized field is set
    /// to a non-positive value. Each check resets the singleton before invoking
    /// Awake so validation can be performed repeatedly on the same instance.
    /// </summary>
    [Test]
    public void Awake_ThrowsWhenParametersNonPositive()
    {
        var go = new GameObject("diff");
        var diff = go.AddComponent<AdaptiveDifficultyManager>();

        // Reset the singleton set during AddComponent so we can invoke Awake
        // manually with custom field values.
        typeof(AdaptiveDifficultyManager).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)
            .SetValue(null, null);

        diff.targetDistance = -1f; // invalid distance
        Assert.Throws<ArgumentOutOfRangeException>(() => diff.Awake());

        diff.targetDistance = 500f;
        diff.sampleSize = 0; // must sample at least one run
        Assert.Throws<ArgumentOutOfRangeException>(() => diff.Awake());

        diff.sampleSize = 5;
        diff.increaseStep = 0f; // non-positive increase step
        Assert.Throws<ArgumentOutOfRangeException>(() => diff.Awake());

        diff.increaseStep = 0.1f;
        diff.decreaseStep = -0.1f; // non-positive decrease step
        Assert.Throws<ArgumentOutOfRangeException>(() => diff.Awake());

        diff.decreaseStep = 0.1f;
        diff.minMultiplier = 0f; // multipliers must be positive
        Assert.Throws<ArgumentOutOfRangeException>(() => diff.Awake());

        diff.minMultiplier = 0.5f;
        diff.maxMultiplier = -1f; // invalid maximum multiplier
        Assert.Throws<ArgumentOutOfRangeException>(() => diff.Awake());

        Object.DestroyImmediate(go);
    }

    /// <summary>
    /// Verifies Awake rejects configurations where the minimum multiplier is
    /// greater than the maximum multiplier, preventing inverted clamping.
    /// </summary>
    [Test]
    public void Awake_ThrowsWhenMinExceedsMax()
    {
        var go = new GameObject("diff");
        var diff = go.AddComponent<AdaptiveDifficultyManager>();
        typeof(AdaptiveDifficultyManager).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)
            .SetValue(null, null);

        diff.minMultiplier = 2f;
        diff.maxMultiplier = 1f; // inconsistent bounds
        Assert.Throws<ArgumentOutOfRangeException>(() => diff.Awake());

        Object.DestroyImmediate(go);
    }

    /// <summary>
    /// Confirms a correctly configured manager completes Awake without throwing
    /// and sets the singleton instance as expected.
    /// </summary>
    [Test]
    public void Awake_AllowsValidConfiguration()
    {
        var go = new GameObject("diff");
        var diff = go.AddComponent<AdaptiveDifficultyManager>();
        typeof(AdaptiveDifficultyManager).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)
            .SetValue(null, null);

        diff.targetDistance = 500f;
        diff.sampleSize = 5;
        diff.increaseStep = 0.1f;
        diff.decreaseStep = 0.1f;
        diff.minMultiplier = 0.5f;
        diff.maxMultiplier = 2f;

        Assert.DoesNotThrow(() => diff.Awake());
        Assert.AreSame(diff, AdaptiveDifficultyManager.Instance,
            "Valid configuration should set the singleton instance.");

        Object.DestroyImmediate(go);
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

    /// <summary>
    /// Ensures a custom combo bonus step set through serialization affects the
    /// difficulty calculation. This allows designers to tune how strongly coin
    /// combos influence spawn multipliers without code changes.
    /// </summary>
    [Test]
    public void AdjustDifficulty_UsesCustomComboBonusStep()
    {
        var analyticsObj = new GameObject("am");
        var am = analyticsObj.AddComponent<AnalyticsManager>();
        am.LogRun(500f, 0, true); // baseline run at target distance

        var gmObj = new GameObject("gm");
        var gm = gmObj.AddComponent<GameManager>();
        typeof(GameManager).GetField("coinComboMultiplier", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(gm, 3); // moderate combo to scale

        var obstacleObj = new GameObject("obs");
        var obstacle = obstacleObj.AddComponent<ObstacleSpawner>();
        var hazardObj = new GameObject("haz");
        var hazard = hazardObj.AddComponent<HazardSpawner>();

        var diffObj = new GameObject("diff");
        var diff = diffObj.AddComponent<AdaptiveDifficultyManager>();
        diff.targetDistance = 500f;

        // Override the serialized combo bonus step to a custom value, simulating
        // a designer-adjusted parameter in the inspector.
        typeof(AdaptiveDifficultyManager).GetField("comboBonusStep", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(diff, 0.1f);

        diff.RegisterSpawners(obstacle, hazard);
        diff.AdjustDifficulty();

        float expected = 1f + (3 - 1) * 0.1f;
        Assert.AreEqual(expected, obstacle.spawnMultiplier, 0.0001f,
            "Custom combo bonus step should directly scale the spawn multiplier");
        Assert.AreEqual(expected, hazard.spawnMultiplier, 0.0001f);

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

