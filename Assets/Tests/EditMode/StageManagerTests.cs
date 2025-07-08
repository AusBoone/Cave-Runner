using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.AddressableAssets;
using System.Collections;
using System.Reflection;

/// <summary>
/// Unit tests for the StageManager component verifying stage application and
/// event-driven updates from the GameManager.
/// </summary>
public class StageManagerTests
{
    [UnityTest]
    public IEnumerator ApplyStage_UpdatesSpawnerLists()
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
            backgroundSprite = new AssetReferenceSprite("00000000000000000000000000000000"),
            groundObstacles = new[] { new AssetReferenceGameObject("00000000000000000000000000000000") },
            ceilingObstacles = new AssetReferenceGameObject[0],
            movingPlatforms = new AssetReferenceGameObject[0],
            rotatingHazards = new AssetReferenceGameObject[0],
            pits = new AssetReferenceGameObject[0],
            bats = new AssetReferenceGameObject[0],
            obstacleSpawnMultiplier = 2f,
            hazardSpawnMultiplier = 0.5f,
            speedMultiplier = 1.5f,
            gravityScale = 0.5f
        };
        sm.stages = new[] { stageAsset };

        sm.ApplyStage(0);
        // Wait for the async coroutine to complete
        var field = typeof(StageManager).GetField("loadRoutine", BindingFlags.NonPublic | BindingFlags.Instance);
        while (field.GetValue(sm) != null)
        {
            yield return null;
        }

        // Asset reference is invalid so no prefab will load, but multipliers should still apply
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

    /// <summary>
    /// Verifies that applying a stage occurs over multiple frames so the main
    /// thread remains responsive while Addressables load.
    /// </summary>
    [UnityTest]
    public IEnumerator ApplyStage_IsAsynchronous()
    {
        var smObj = new GameObject("sm");
        var sm = smObj.AddComponent<StageManager>();
        sm.parallaxBackground = new GameObject("bg").AddComponent<ParallaxBackground>();
        sm.obstacleSpawner = smObj.AddComponent<ObstacleSpawner>();
        sm.hazardSpawner = smObj.AddComponent<HazardSpawner>();

        var asset = ScriptableObject.CreateInstance<StageDataSO>();
        asset.stage = new StageManager.StageData
        {
            backgroundSprite = new AssetReferenceSprite("00000000000000000000000000000003"),
            groundObstacles = new AssetReferenceGameObject[0]
        };
        sm.stages = new[] { asset };

        sm.ApplyStage(0);
        var field = typeof(StageManager).GetField("loadRoutine", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field.GetValue(sm));
        yield return null; // ensure at least one frame passes
        Assert.IsNotNull(field.GetValue(sm));
        while (field.GetValue(sm) != null)
        {
            yield return null;
        }

        Object.DestroyImmediate(smObj);
        Object.DestroyImmediate(asset);
    }

    [UnityTest]
    public IEnumerator Event_TriggersStageChange()
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
        start.stage = new StageManager.StageData { backgroundSprite = new AssetReferenceSprite("00000000000000000000000000000001") };
        var next = ScriptableObject.CreateInstance<StageDataSO>();
        next.stage = new StageManager.StageData { backgroundSprite = new AssetReferenceSprite("00000000000000000000000000000002") };
        sm.stages = new[] { start, next };

        gm.StartGame();
        // Initial stage should apply index 0
        Assert.AreEqual("start", bg.spriteName);

        // Force distance over goal then call GameManager.Update via reflection
        var distField = typeof(GameManager).GetField("distance", BindingFlags.NonPublic | BindingFlags.Instance);
        distField.SetValue(gm, 0.2f);
        var update = typeof(GameManager).GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance);
        update.Invoke(gm, null);

        // Wait for StageManager to apply the new stage asynchronously
        var field = typeof(StageManager).GetField("loadRoutine", BindingFlags.NonPublic | BindingFlags.Instance);
        while (field.GetValue(sm) != null)
        {
            yield return null;
        }

        Assert.AreEqual("next", bg.spriteName);

        Object.DestroyImmediate(gmObj);
        Object.DestroyImmediate(bgObj);
        Object.DestroyImmediate(obsObj);
        Object.DestroyImmediate(hazObj);
        Object.DestroyImmediate(smObj);
    }

    /// <summary>
    /// Applying a new stage should release addressable handles from the
    /// previous stage to prevent memory leaks.
    /// </summary>
    [UnityTest]
    public IEnumerator ApplyStage_ReleasesOldHandles()
    {
        var smObj = new GameObject("sm");
        var sm = smObj.AddComponent<StageManager>();
        sm.parallaxBackground = new GameObject("bg").AddComponent<ParallaxBackground>();
        sm.obstacleSpawner = smObj.AddComponent<ObstacleSpawner>();
        sm.hazardSpawner = smObj.AddComponent<HazardSpawner>();

        var first = ScriptableObject.CreateInstance<StageDataSO>();
        first.stage = new StageManager.StageData { backgroundSprite = new AssetReferenceSprite("00000000000000000000000000000003") };
        var second = ScriptableObject.CreateInstance<StageDataSO>();
        second.stage = new StageManager.StageData { backgroundSprite = new AssetReferenceSprite("00000000000000000000000000000004") };
        sm.stages = new[] { first, second };

        sm.ApplyStage(0);
        FieldInfo routineField = typeof(StageManager).GetField("loadRoutine", BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo handlesField = typeof(StageManager).GetField("loadedHandles", BindingFlags.NonPublic | BindingFlags.Instance);
        while (routineField.GetValue(sm) != null)
            yield return null;
        var handles = (System.Collections.Generic.List<AsyncOperationHandle>)handlesField.GetValue(sm);
        AsyncOperationHandle oldHandle = handles.Count > 0 ? handles[0] : default;

        sm.ApplyStage(1);
        while (routineField.GetValue(sm) != null)
            yield return null;
        handles = (System.Collections.Generic.List<AsyncOperationHandle>)handlesField.GetValue(sm);
        Assert.IsFalse(oldHandle.IsValid(), "Old handles should be released");
        Assert.AreEqual(1, handles.Count, "Handle list should reflect only current stage");

        Object.DestroyImmediate(smObj);
        Object.DestroyImmediate(first);
        Object.DestroyImmediate(second);
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

