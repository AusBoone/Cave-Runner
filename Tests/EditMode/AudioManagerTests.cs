using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

/// <summary>
/// Tests covering AudioManager's cross-fading logic, stage music selection,
/// and sound effect clip caching behavior.
/// </summary>
public class AudioManagerTests
{
    /// <summary>
    /// Basic subclass to expose the clip passed to CrossfadeTo for verification.
    /// </summary>
    private class TestAudioManager : AudioManager
    {
        public AudioClip lastClip;
        public override void CrossfadeTo(AudioClip newClip, float duration = 1f)
        {
            lastClip = newClip;
            base.CrossfadeTo(newClip, duration);
        }
    }

    /// <summary>
    /// StageManager subclass that returns a dummy clip instead of loading from
    /// Resources so tests do not rely on asset files.
    /// </summary>
    private class TestStageManager : StageManager
    {
        protected override AudioClip LoadStageMusic(string clipName)
        {
            var clip = AudioClip.Create(clipName, 441, 1, 44100, false);
            clip.name = clipName;
            return clip;
        }
    }

    /// <summary>
    /// AudioManager subclass used to track load and release counts. The
    /// overridden Addressables hooks return a completed handle containing a
    /// reusable stub clip so tests do not touch disk.
    /// </summary>
    private class CachingAudioManager : AudioManager
    {
        public int loadCount;
        public int releaseCount;
        public AudioClip stubClip = AudioClip.Create("stub", 441, 1, 44100, false);

        protected override AsyncOperationHandle<AudioClip> LoadClipHandle(string clipName)
        {
            loadCount++;
            stubClip.name = clipName;
            // Create a completed handle containing the stub clip.
            return Addressables.ResourceManager.CreateCompletedOperation<AudioClip>(stubClip, null);
        }

        protected override void ReleaseHandle(AsyncOperationHandle<AudioClip> handle)
        {
            // Track releases instead of delegating to Addressables.
            releaseCount++;
        }
    }

    /// <summary>
    /// Ensures the cross-fade coroutine blends volumes over the given duration.
    /// </summary>
    [UnityTest]
    public IEnumerator Crossfade_FadesOverDuration()
    {
        var obj = new GameObject("am");
        var am = obj.AddComponent<AudioManager>();
        am.musicSource = obj.AddComponent<AudioSource>();
        am.musicSourceSecondary = obj.AddComponent<AudioSource>();

        // Manually invoke Awake so private fields initialize
        typeof(AudioManager).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(am, null);

        // Start with an initial clip playing
        am.musicSource.clip = AudioClip.Create("start", 441, 1, 44100, false);
        am.musicSource.loop = true;
        am.musicSource.volume = 1f;
        am.musicSource.Play();

        var next = AudioClip.Create("next", 441, 1, 44100, false);
        float begin = Time.time;
        am.CrossfadeTo(next, 0.05f);

        // Wait for the coroutine to finish
        var routineField = typeof(AudioManager).GetField("fadeRoutine", BindingFlags.NonPublic | BindingFlags.Instance);
        while (routineField.GetValue(am) != null)
        {
            yield return null;
        }
        float elapsed = Time.time - begin;
        Assert.That(elapsed, Is.EqualTo(0.05f).Within(0.03f), "Fade duration should match parameter");

        var activeField = typeof(AudioManager).GetField("activeMusicSource", BindingFlags.NonPublic | BindingFlags.Instance);
        var inactiveField = typeof(AudioManager).GetField("inactiveMusicSource", BindingFlags.NonPublic | BindingFlags.Instance);
        AudioSource active = (AudioSource)activeField.GetValue(am);
        AudioSource inactive = (AudioSource)inactiveField.GetValue(am);
        Assert.AreEqual(1f, active.volume, 0.01f, "Active source should end at full volume");
        Assert.AreEqual(0f, inactive.volume, 0.01f, "Inactive source should end muted");
        Assert.AreEqual(next, active.clip, "New clip should be playing after fade");

        Object.DestroyImmediate(obj);
    }

    /// <summary>
    /// Verifies StageManager chooses a clip from StageData.stageMusic and passes
    /// it to AudioManager for playback.
    /// </summary>
    [UnityTest]
    public IEnumerator ApplyStage_PicksStageMusic()
    {
        var amObj = new GameObject("am");
        var am = amObj.AddComponent<TestAudioManager>();
        am.musicSource = amObj.AddComponent<AudioSource>();
        am.musicSourceSecondary = amObj.AddComponent<AudioSource>();
        typeof(AudioManager).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(am, null);

        var smObj = new GameObject("sm");
        var sm = smObj.AddComponent<TestStageManager>();
        sm.parallaxBackground = new GameObject("bg").AddComponent<ParallaxBackground>();
        sm.obstacleSpawner = smObj.AddComponent<ObstacleSpawner>();
        sm.hazardSpawner = smObj.AddComponent<HazardSpawner>();
        var asset = ScriptableObject.CreateInstance<StageDataSO>();
        asset.stage = new StageManager.StageData { stageMusic = new[] { "songA" } };
        sm.stages = new[] { asset };

        sm.ApplyStage(0);
        var routineField = typeof(StageManager).GetField("loadRoutine", BindingFlags.NonPublic | BindingFlags.Instance);
        while (routineField.GetValue(sm) != null)
        {
            yield return null;
        }

        Assert.IsNotNull(am.lastClip, "AudioManager should receive a clip to play");
        Assert.AreEqual("songA", am.lastClip.name, "Clip name should come from StageData");

        Object.DestroyImmediate(amObj);
        Object.DestroyImmediate(smObj);
        Object.DestroyImmediate(asset);
    }

    /// <summary>
    /// Ensures that <see cref="AudioManager.PlaySound(string, float)"/> caches
    /// loaded clips and reuses them on subsequent calls until the release
    /// timeout expires.
    /// </summary>
    [Test]
    public void PlaySound_CachesClipUntilRelease()
    {
        var obj = new GameObject("am");
        var am = obj.AddComponent<CachingAudioManager>();
        am.effectsSource = obj.AddComponent<AudioSource>();
        am.clipReleaseDelay = 100f; // Ensure the clip persists in cache.

        // Awake is normally called by Unity; invoke manually so the singleton
        // instance and internal fields initialize for the test environment.
        typeof(AudioManager).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(am, null);

        am.PlaySound("beep");
        am.PlaySound("beep");

        // The overridden loader should have been called only once because the
        // second call retrieves the clip from cache.
        Assert.AreEqual(1, am.loadCount, "Clip should be loaded once and then reused");
        Assert.AreEqual(0, am.releaseCount, "Clip should not be released while cached");

        // Access the private cache via reflection to verify the stored instance.
        var cacheField = typeof(AudioManager).GetField("clipCache", BindingFlags.NonPublic | BindingFlags.Instance);
        var cache = (System.Collections.IDictionary)cacheField.GetValue(am);
        Assert.IsTrue(cache.Contains("beep"), "Cache should contain the loaded clip");
        var entry = cache["beep"]; // ClipReference
        var handleField = entry.GetType().GetField("handle", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        var handle = (AsyncOperationHandle<AudioClip>)handleField.GetValue(entry);
        Assert.AreSame(am.stubClip, handle.Result, "Cached clip should match the loaded instance");

        Object.DestroyImmediate(obj);
    }

    /// <summary>
    /// Confirms that cached clips are released after the configured delay and
    /// subsequently reloaded when requested again.
    /// </summary>
    [UnityTest]
    public IEnumerator PlaySound_ReleasesAndReloadsClip()
    {
        var obj = new GameObject("am");
        var am = obj.AddComponent<CachingAudioManager>();
        am.effectsSource = obj.AddComponent<AudioSource>();
        am.clipReleaseDelay = 0f; // Immediate release for test speed.

        typeof(AudioManager).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(am, null);

        am.PlaySound("beep");
        // Allow the release coroutine to run.
        yield return null;

        Assert.AreEqual(1, am.releaseCount, "Clip should be released after delay");

        // Cache should no longer contain the clip.
        var cacheField = typeof(AudioManager).GetField("clipCache", BindingFlags.NonPublic | BindingFlags.Instance);
        var cache = (System.Collections.IDictionary)cacheField.GetValue(am);
        Assert.IsFalse(cache.Contains("beep"), "Cache should be empty after release");

        // Playing again should trigger another load.
        am.PlaySound("beep");
        Assert.AreEqual(2, am.loadCount, "Clip should reload after being released");

        Object.DestroyImmediate(obj);
    }
}
