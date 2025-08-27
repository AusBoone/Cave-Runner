using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.AddressableAssets;
using System.Collections;
using System.Collections.Generic;
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

        // Invalid addressable references should trigger error logs during
        // loading. Expect two errors: one for the background sprite and one
        // for the ground obstacle prefab list.
        var failurePattern = new System.Text.RegularExpressions.Regex("(Failed|Exception)");
        LogAssert.Expect(LogType.Error, failurePattern);
        LogAssert.Expect(LogType.Error, failurePattern);

        sm.ApplyStage(0);
        // Wait for the async coroutine to complete
        var field = typeof(StageManager).GetField("loadRoutine", BindingFlags.NonPublic | BindingFlags.Instance);
        while (field.GetValue(sm) != null)
        {
            yield return null;
        }
        // After loading finishes the coroutine reference should clear so callers
        // know no asynchronous stage load remains active.
        Assert.IsNull(field.GetValue(sm), "loadRoutine should be null when no load is active");

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
        // Coroutine should reset to null once the asynchronous load completes.
        Assert.IsNull(field.GetValue(sm), "loadRoutine should be null when the load finishes");

        Object.DestroyImmediate(smObj);
        Object.DestroyImmediate(asset);
    }

    /// <summary>
    /// Loading only stage music should still run over multiple frames because
    /// audio clips are fetched via Addressables rather than synchronously from
    /// Resources. This ensures the new music loading path is truly asynchronous
    /// and integrates with the stage coroutine.
    /// </summary>
    [UnityTest]
    public IEnumerator ApplyStage_MusicLoadsAsynchronously()
    {
        // Ensure no global AudioManager interferes with the test environment.
        if (AudioManager.Instance != null)
        {
            Object.DestroyImmediate(AudioManager.Instance.gameObject);
        }

        var smObj = new GameObject("sm");
        var sm = smObj.AddComponent<StageManager>();

        var asset = ScriptableObject.CreateInstance<StageDataSO>();
        asset.stage = new StageManager.StageData
        {
            // Invalid key forces the Addressables request to fail but still
            // exercise the asynchronous loading path.
            stageMusic = new[] { "00000000000000000000000000000006" }
        };
        sm.stages = new[] { asset };

        // Expect an error log due to the invalid addressable key.
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("(Failed|Exception)"));

        sm.ApplyStage(0);
        FieldInfo routineField = typeof(StageManager).GetField("loadRoutine", BindingFlags.NonPublic | BindingFlags.Instance);

        // Coroutine should begin immediately and persist across frames until the
        // Addressables request completes.
        Assert.IsNotNull(routineField.GetValue(sm));
        yield return null;
        Assert.IsNotNull(routineField.GetValue(sm));
        while (routineField.GetValue(sm) != null)
        {
            yield return null;
        }
        // Reference must clear after music load completes so callers can observe
        // that no pending stage operation remains.
        Assert.IsNull(routineField.GetValue(sm), "loadRoutine should be null when music loading ends");

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

        // Once the second stage finishes loading, there should be no active
        // coroutine left running.
        Assert.IsNull(field.GetValue(sm), "loadRoutine should be null after stage transition");

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
        // After the initial stage finishes loading the coroutine reference should clear.
        Assert.IsNull(routineField.GetValue(sm), "loadRoutine should be null after first load");
        var handles = (System.Collections.Generic.List<AsyncOperationHandle>)handlesField.GetValue(sm);
        AsyncOperationHandle oldHandle = handles.Count > 0 ? handles[0] : default;

        sm.ApplyStage(1);
        while (routineField.GetValue(sm) != null)
            yield return null;
        // Reference should again reset once the second stage completes.
        Assert.IsNull(routineField.GetValue(sm), "loadRoutine should be null after second load");
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

    /// <summary>
    /// When an invalid stage index is supplied the coroutine reference should
    /// clear immediately so callers do not hold a stale handle.
    /// </summary>
    [UnityTest]
    public IEnumerator LoadStageRoutine_InvalidIndex_ClearsReference()
    {
        var smObj = new GameObject("sm");
        var sm = smObj.AddComponent<StageManager>();
        sm.stages = new StageDataSO[0];

        sm.ApplyStage(5); // out of bounds

        FieldInfo field = typeof(StageManager).GetField("loadRoutine", BindingFlags.NonPublic | BindingFlags.Instance);
        yield return null; // allow coroutine to run
        Assert.IsNull(field.GetValue(sm), "loadRoutine should be cleared after early exit");

        Object.DestroyImmediate(smObj);
    }

    /// <summary>
    /// Combined progress from parallel loading operations should advance from
    /// 0 to 1 as each asset finishes loading. This verifies the new progress
    /// reporting logic aggregates multiple asynchronous tasks.
    /// </summary>
    [UnityTest]
    public IEnumerator LoadStageRoutine_ReportsCombinedProgress()
    {
        // Create a UI manager subclass that records progress values for
        // verification. Awake will assign the singleton instance automatically.
        var uiObj = new GameObject("ui");
        var ui = uiObj.AddComponent<ProgressUI>();

        var smObj = new GameObject("sm");
        var sm = smObj.AddComponent<StageManager>();
        sm.parallaxBackground = new GameObject("bg").AddComponent<ParallaxBackground>();
        sm.obstacleSpawner = smObj.AddComponent<ObstacleSpawner>();
        sm.hazardSpawner = smObj.AddComponent<HazardSpawner>();

        // Stage contains three invalid addressable references which should each
        // trigger an error log and generate progress updates as loading fails.
        var stage = ScriptableObject.CreateInstance<StageDataSO>();
        stage.stage = new StageManager.StageData
        {
            backgroundSprite = new AssetReferenceSprite("00000000000000000000000000000006"),
            groundObstacles = new[] { new AssetReferenceGameObject("00000000000000000000000000000006") },
            pits = new[] { new AssetReferenceGameObject("00000000000000000000000000000006") }
        };
        sm.stages = new[] { stage };

        var failurePattern = new System.Text.RegularExpressions.Regex("(Failed|Exception)");
        LogAssert.Expect(LogType.Error, failurePattern); // background
        LogAssert.Expect(LogType.Error, failurePattern); // ground obstacle
        LogAssert.Expect(LogType.Error, failurePattern); // pit prefab

        sm.ApplyStage(0);

        FieldInfo field = typeof(StageManager).GetField("loadRoutine", BindingFlags.NonPublic | BindingFlags.Instance);
        while (field.GetValue(sm) != null)
        {
            yield return null;
        }
        // After all assets either load or fail, the coroutine reference should
        // reset to indicate no further work remains.
        Assert.IsNull(field.GetValue(sm), "loadRoutine should be null once progress completes");

        // Progress should report at least start (0) and completion (1).
        Assert.GreaterOrEqual(ui.ProgressValues.Count, 2);
        Assert.AreEqual(1f, ui.ProgressValues[ui.ProgressValues.Count - 1]);

        // Clean up objects and reset the singleton so other tests remain isolated.
        Object.DestroyImmediate(smObj);
        Object.DestroyImmediate(stage);
        Object.DestroyImmediate(uiObj);
        typeof(UIManager).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)
            .SetValue(null, null);
    }

    /// <summary>
    /// Destroying the manager should reset any gravity modifications so later
    /// scenes start with the default physics settings.
    /// </summary>
    [UnityTest]
    public IEnumerator OnDestroy_RestoresGravity()
    {
        // Record the engine's default gravity prior to creating the manager.
        Vector2 startGravity = Physics2D.gravity;

        var smObj = new GameObject("sm");
        var sm = smObj.AddComponent<StageManager>();
        sm.parallaxBackground = new GameObject("bg").AddComponent<ParallaxBackground>();
        sm.obstacleSpawner = smObj.AddComponent<ObstacleSpawner>();
        sm.hazardSpawner = smObj.AddComponent<HazardSpawner>();

        var stage = ScriptableObject.CreateInstance<StageDataSO>();
        stage.stage = new StageManager.StageData
        {
            backgroundSprite = new AssetReferenceSprite("00000000000000000000000000000005"),
            groundObstacles = new AssetReferenceGameObject[0],
            gravityScale = 0.5f
        };
        sm.stages = new[] { stage };

        sm.ApplyStage(0);
        FieldInfo routineField = typeof(StageManager).GetField("loadRoutine", BindingFlags.NonPublic | BindingFlags.Instance);
        while (routineField.GetValue(sm) != null)
            yield return null;
        // The coroutine reference should clear before the manager is destroyed.
        Assert.IsNull(routineField.GetValue(sm), "loadRoutine should be null before destruction");

        Object.DestroyImmediate(smObj);
        Assert.AreEqual(startGravity.y, Physics2D.gravity.y, 0.001f);

        Object.DestroyImmediate(stage);
    }

    /// <summary>
    /// Applying a stage should scale only the vertical component of gravity
    /// while leaving the horizontal component untouched. Games may set a
    /// sideways force (e.g., wind) using Physics2D.gravity.x; losing that value
    /// when stages modify gravity would lead to inconsistent gameplay.
    /// </summary>
    [UnityTest]
    public IEnumerator ApplyStage_PreservesHorizontalGravity()
    {
        // Capture the engine's original gravity so it can be restored after the
        // test to avoid contaminating other tests.
        Vector2 originalGravity = Physics2D.gravity;

        // Introduce a horizontal gravity component to verify it persists after
        // stage application. The Y value is left at the engine default.
        Physics2D.gravity = new Vector2(1f, originalGravity.y);

        // Create a minimal StageManager with a single stage that doubles the
        // gravity scale. No spawners or backgrounds are required for this check.
        var stageManagerObject = new GameObject("stageManager");
        var stageManager = stageManagerObject.AddComponent<StageManager>();
        var stageAsset = ScriptableObject.CreateInstance<StageDataSO>();
        stageAsset.stage = new StageManager.StageData { gravityScale = 2f };
        stageManager.stages = new[] { stageAsset };

        stageManager.ApplyStage(0);

        // Wait for the asynchronous stage load to complete so gravity updates
        // can be evaluated.
        FieldInfo routineField = typeof(StageManager).GetField("loadRoutine", BindingFlags.NonPublic | BindingFlags.Instance);
        while (routineField.GetValue(stageManager) != null)
        {
            yield return null;
        }
        // The reference should clear once gravity scaling completes.
        Assert.IsNull(routineField.GetValue(stageManager), "loadRoutine should be null after gravity update");

        // Only the vertical component should change; horizontal gravity must
        // remain exactly as configured prior to applying the stage.
        Assert.AreEqual(1f, Physics2D.gravity.x);
        Assert.AreEqual(originalGravity.y * 2f, Physics2D.gravity.y, 0.001f);

        // Cleanup objects and restore original gravity so subsequent tests run
        // with the expected physics environment.
        Object.DestroyImmediate(stageManagerObject);
        Object.DestroyImmediate(stageAsset);
        Physics2D.gravity = originalGravity;
    }

    // Helper UI manager that records progress updates for verification. It
    // subclasses the real UIManager so StageManager can interact with it
    // normally while tests capture the reported values.
    private class ProgressUI : UIManager
    {
        public readonly List<float> ProgressValues = new List<float>();

        public override void SetLoadingProgress(float progress)
        {
            base.SetLoadingProgress(progress);
            ProgressValues.Add(progress);
        }
    }
}

