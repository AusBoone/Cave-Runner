using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Collections;
using System.Reflection;

/// <summary>
/// Tests covering AudioManager's cross-fading logic and stage music selection.
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
}
