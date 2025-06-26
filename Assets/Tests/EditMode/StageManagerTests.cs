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
        var stage = new StageManager.StageData
        {
            backgroundSprite = "stageA",
            groundObstacles = new[] { dummy },
            ceilingObstacles = new GameObject[0],
            movingPlatforms = new GameObject[0],
            rotatingHazards = new GameObject[0],
            pits = new GameObject[0],
            bats = new GameObject[0]
        };
        sm.stages = new[] { stage };

        sm.ApplyStage(0);

        Assert.AreEqual("stageA", bg.spriteName);
        Assert.AreSame(dummy, obstacleSpawner.groundObstacles[0]);

        Object.DestroyImmediate(dummy);
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
        sm.stages = new[] {
            new StageManager.StageData { backgroundSprite = "start" },
            new StageManager.StageData { backgroundSprite = "next" }
        };

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
}
