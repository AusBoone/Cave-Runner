using NUnit.Framework;
using UnityEngine;
using System.Reflection;

/// <summary>
/// Unit tests for the StageManager component verifying stage application and
/// event-driven updates from the GameManager.
/// </summary>
public class StageManagerTests
{
    [Test]
    public void ApplyStage_UpdatesSpawnerLists()
    {
        // Setup basic objects and component hierarchy
        var gmObj = new GameObject("gm");
        var gm = gmObj.AddComponent<GameManager>();

        var bgObj = new GameObject("bg");
        bgObj.AddComponent<SpriteRenderer>();
        var bg = bgObj.AddComponent<ParallaxBackground>();

        var obsObj = new GameObject("obs");
        var obstacleSpawner = obsObj.AddComponent<ObstacleSpawner>();

        var hazObj = new GameObject("haz");
        var hazardSpawner = hazObj.AddComponent<HazardSpawner>();

        var smObj = new GameObject("sm");
        var sm = smObj.AddComponent<StageManager>();
        sm.parallaxBackground = bg;
        sm.obstacleSpawner = obstacleSpawner;
        sm.hazardSpawner = hazardSpawner;

        // Provide simple prefabs for stage data
        var dummy = new GameObject("prefab");
        var stageAsset = ScriptableObject.CreateInstance<StageDataSO>();
        stageAsset.stage = new StageManager.StageData
        {
            backgroundSprite = "stageA",
            groundObstacles = new[] { dummy },
            ceilingObstacles = new GameObject[0],
            movingPlatforms = new GameObject[0],
            rotatingHazards = new GameObject[0],
            pits = new GameObject[0],
            bats = new GameObject[0],
            obstacleSpawnMultiplier = 2f,
            hazardSpawnMultiplier = 0.5f,
            speedMultiplier = 1.5f,
            gravityScale = 0.5f
        };
        sm.stages = new[] { stageAsset };

        sm.ApplyStage(0);

        Assert.AreEqual("stageA", bg.spriteName);
        Assert.AreSame(dummy, obstacleSpawner.groundObstacles[0]);
        Assert.AreEqual(2f, obstacleSpawner.spawnMultiplier);
        Assert.AreEqual(0.5f, hazardSpawner.spawnMultiplier);
        var multField = typeof(GameManager).GetField("stageSpeedMultiplier", BindingFlags.NonPublic | BindingFlags.Instance);
        float mult = (float)multField.GetValue(gm);
        Assert.AreEqual(1.5f, mult);
        Assert.AreEqual(-9.81f * 0.5f, Physics2D.gravity.y, 0.001f);

        Object.DestroyImmediate(dummy);
        Object.DestroyImmediate(stageAsset);
        Object.DestroyImmediate(gmObj);
        Object.DestroyImmediate(bgObj);
        Object.DestroyImmediate(obsObj);
        Object.DestroyImmediate(hazObj);
        Object.DestroyImmediate(smObj);
    }

    [Test]
    public void Event_TriggersStageChange()
    {
        var gmObj = new GameObject("gm");
        var gm = gmObj.AddComponent<GameManager>();
        gm.stageGoals = new float[] { 0.1f };

        var bgObj = new GameObject("bg");
        bgObj.AddComponent<SpriteRenderer>();
        var bg = bgObj.AddComponent<ParallaxBackground>();

        var obsObj = new GameObject("obs");
        var obstacleSpawner = obsObj.AddComponent<ObstacleSpawner>();

        var hazObj = new GameObject("haz");
        var hazardSpawner = hazObj.AddComponent<HazardSpawner>();

        var smObj = new GameObject("sm");
        var sm = smObj.AddComponent<StageManager>();
        sm.parallaxBackground = bg;
        sm.obstacleSpawner = obstacleSpawner;
        sm.hazardSpawner = hazardSpawner;
        var start = ScriptableObject.CreateInstance<StageDataSO>();
        start.stage = new StageManager.StageData { backgroundSprite = "start" };
        var next = ScriptableObject.CreateInstance<StageDataSO>();
        next.stage = new StageManager.StageData { backgroundSprite = "next" };
        sm.stages = new[] { start, next };

        gm.StartGame();
        // Initial stage should apply index 0
        Assert.AreEqual("start", bg.spriteName);

        // Force distance over goal then call GameManager.Update via reflection
        var distField = typeof(GameManager).GetField("distance", BindingFlags.NonPublic | BindingFlags.Instance);
        distField.SetValue(gm, 0.2f);
        var update = typeof(GameManager).GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance);
        update.Invoke(gm, null);

        Assert.AreEqual("next", bg.spriteName);

        Object.DestroyImmediate(gmObj);
        Object.DestroyImmediate(bgObj);
        Object.DestroyImmediate(obsObj);
        Object.DestroyImmediate(hazObj);
        Object.DestroyImmediate(smObj);
    }

    [Test]
    public void Awake_RegistersSpawnersWithAdaptiveManager()
    {
        var diffObj = new GameObject("diff");
        var diff = diffObj.AddComponent<AdaptiveDifficultyManager>();

        var obsObj = new GameObject("obs");
        var obstacle = obsObj.AddComponent<ObstacleSpawner>();
        var hazObj = new GameObject("haz");
        var hazard = hazObj.AddComponent<HazardSpawner>();

        var smObj = new GameObject("sm");
        var sm = smObj.AddComponent<StageManager>();
        sm.obstacleSpawner = obstacle;
        sm.hazardSpawner = hazard;

        // Manually invoke Awake so registration occurs after fields are assigned
        typeof(StageManager).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(sm, null);

        var obsField = typeof(AdaptiveDifficultyManager).GetField("obstacleSpawner", BindingFlags.NonPublic | BindingFlags.Instance);
        var hazField = typeof(AdaptiveDifficultyManager).GetField("hazardSpawner", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.AreSame(obstacle, obsField.GetValue(diff));
        Assert.AreSame(hazard, hazField.GetValue(diff));

        Object.DestroyImmediate(smObj);
        Object.DestroyImmediate(obsObj);
        Object.DestroyImmediate(hazObj);
        Object.DestroyImmediate(diffObj);
        // Clear the static instance to avoid cross test contamination
        typeof(AdaptiveDifficultyManager).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)
            .SetValue(null, null);
    }
}

