#region AudioManager Overview

#endregion
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Manages music and sound effect playback. This component is implemented
/// as a persistent singleton so sounds can continue across scene loads.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    public AudioSource musicSource;
    [Tooltip("Secondary source used for cross-fading between tracks.")]
    public AudioSource musicSourceSecondary;
    public AudioSource effectsSource;

    // Cache storing Addressables handles for sound effects requested via
    // PlaySound(string). Each entry records the handle and a coroutine used to
    // release the clip after a period of inactivity. This approach avoids
    // repeated disk access while still allowing unused clips to be unloaded
    // automatically to free memory.
    private readonly Dictionary<string, ClipReference> clipCache = new Dictionary<string, ClipReference>();

    // Duration in seconds that a clip remains in the cache after its last use.
    // Tests can override this to zero for immediate release or increase it for
    // longer-lived caching depending on project needs.
    [Tooltip("Seconds a sound effect stays in cache after playback.")]
    public float clipReleaseDelay = 5f;

    // Internal record tying an Addressables handle to a coroutine that will
    // release it after clipReleaseDelay seconds.
    private class ClipReference
    {
        public AsyncOperationHandle<AudioClip> handle;
        public Coroutine releaseCoroutine;
    }

    // Name of the music clip located under Assets/Audio/Resources.
    public string backgroundMusicName = "Background";
    private AudioClip backgroundMusic;

    // Currently playing source while cross-fading
    private AudioSource activeMusicSource;
    // Source prepared with the next clip during a transition
    private AudioSource inactiveMusicSource;
    // Tracks the running fade coroutine so tests can observe timing
    private Coroutine fadeRoutine;

    /// <summary>
    /// Standard Unity callback. Ensures only one AudioManager exists
    /// and marks it to persist between scene loads.
    /// </summary>
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Initialize cross-fade sources so they can swap roles during playback
        activeMusicSource = musicSource;
        inactiveMusicSource = musicSourceSecondary;
    }

    /// <summary>
    /// Starts looping background music if an AudioSource has been
    /// assigned in the inspector.
    /// </summary>
    void Start()
    {
        // Apply saved volume levels before starting playback so the user
        // hears audio at the expected levels from the first frame.
        if (SaveGameManager.Instance != null)
        {
            SetMusicVolume(SaveGameManager.Instance.MusicVolume);
            SetEffectsVolume(SaveGameManager.Instance.EffectsVolume);
        }

        if (activeMusicSource != null && !activeMusicSource.isPlaying)
        {
            if (backgroundMusic == null && !string.IsNullOrEmpty(backgroundMusicName))
            {
                backgroundMusic = Resources.Load<AudioClip>("Audio/" + backgroundMusicName);
            }
            if (backgroundMusic != null)
            {
                activeMusicSource.clip = backgroundMusic;
                activeMusicSource.loop = true;
                activeMusicSource.volume = 1f;
                activeMusicSource.Play();
            }
        }
    }

    /// <summary>
    /// Adjusts the music source volume. The provided value is clamped to the
    /// [0,1] range to match Unity's expected AudioSource volume.
    /// </summary>
    public void SetMusicVolume(float volume)
    {
        float vol = Mathf.Clamp01(volume);
        if (musicSource != null)
        {
            musicSource.volume = vol;
        }
        if (musicSourceSecondary != null)
        {
            musicSourceSecondary.volume = vol;
        }
    }

    /// <summary>
    /// Adjusts the effects source volume. Values are clamped between 0 and 1.
    /// </summary>
    public void SetEffectsVolume(float volume)
    {
        if (effectsSource != null)
        {
            effectsSource.volume = Mathf.Clamp01(volume);
        }
    }

    /// <summary>
    /// Clears all cached sound effect handles. Each handle is released via the
    /// Addressables system before the cache is emptied. Tests or callers invoke
    /// this method for an immediate cleanup rather than waiting for the
    /// automatic timeout.
    /// </summary>
    public void ClearClipCache()
    {
        foreach (var kvp in clipCache)
        {
            if (kvp.Value.releaseCoroutine != null)
            {
                StopCoroutine(kvp.Value.releaseCoroutine);
            }
            ReleaseHandle(kvp.Value.handle);
        }
        clipCache.Clear();
    }

    /// <summary>
    /// Initiates an Addressables load for a clip. The default implementation
    /// simply forwards to <see cref="Addressables.LoadAssetAsync{TObject}(object)"/>
    /// and returns the handle so callers can monitor or release it. Unit tests
    /// override this method to inject stub clips without touching the disk.
    /// </summary>
    /// <param name="clipName">Addressable key used to locate the clip.</param>
    /// <returns>Async handle representing the load request.</returns>
    protected virtual AsyncOperationHandle<AudioClip> LoadClipHandle(string clipName)
    {
        return Addressables.LoadAssetAsync<AudioClip>(clipName);
    }

    /// <summary>
    /// Releases an Addressables handle. Exposed as virtual so unit tests can
    /// track when releases occur without depending on the Addressables runtime.
    /// </summary>
    protected virtual void ReleaseHandle(AsyncOperationHandle<AudioClip> handle)
    {
        Addressables.Release(handle);
    }

/// <summary>
/// Plays a one-shot sound effect through the effects AudioSource.
/// </summary>
public void PlaySound(AudioClip clip, float pitch = 1f)
    {
        if (clip != null && effectsSource != null)
        {
            float originalPitch = effectsSource.pitch;
            effectsSource.pitch = pitch;
            effectsSource.PlayOneShot(clip);
            effectsSource.pitch = originalPitch;
        }
    }

    /// <summary>
    /// Convenience overload that loads a sound effect by addressable key. The
    /// clip is cached via its <see cref="AsyncOperationHandle"/> so subsequent
    /// plays do not trigger another load. After <see cref="clipReleaseDelay"/>
    /// seconds of inactivity the handle is automatically released to reclaim
    /// memory.
    /// </summary>
    /// <param name="clipName">Addressable key identifying the clip.</param>
    /// <param name="pitch">Optional pitch adjustment passed to PlaySound.</param>
    public void PlaySound(string clipName, float pitch = 1f)
    {
        if (string.IsNullOrEmpty(clipName) || effectsSource == null)
        {
            return;
        }

        if (!clipCache.TryGetValue(clipName, out ClipReference reference) ||
            !reference.handle.IsValid() || reference.handle.Result == null)
        {
            // Load clip via Addressables and wait synchronously for completion so
            // the one-shot can play immediately. In a production game this could
            // be fully asynchronous with a callback.
            var handle = LoadClipHandle(clipName);
            handle.WaitForCompletion();
            if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
            {
                LoggingHelper.LogWarning($"Sound clip '{clipName}' failed to load"); // Use helper so missing clips obey verbose gating.
                return;
            }

            reference = new ClipReference { handle = handle };
            clipCache[clipName] = reference;
        }

        // Play the loaded clip immediately.
        PlaySound(reference.handle.Result, pitch);

        // Restart the release timer so the clip remains cached while in recent use.
        if (reference.releaseCoroutine != null)
        {
            StopCoroutine(reference.releaseCoroutine);
        }
        reference.releaseCoroutine = StartCoroutine(ReleaseAfterDelay(clipName, clipReleaseDelay));
    }

    // Coroutine that waits the configured delay before releasing a cached clip.
    private IEnumerator ReleaseAfterDelay(string clipName, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (clipCache.TryGetValue(clipName, out ClipReference reference))
        {
            ReleaseHandle(reference.handle);
            clipCache.Remove(clipName);
        }
    }

    /// <summary>
    /// Cross-fades from the currently playing music to <paramref name="newClip"/>.
    /// If either AudioSource is missing the call is ignored.
    /// </summary>
    /// <param name="newClip">Clip to fade in.</param>
    /// <param name="duration">Seconds for the transition.</param>
    public virtual void CrossfadeTo(AudioClip newClip, float duration = 1f)
    {
        if (newClip == null || activeMusicSource == null || inactiveMusicSource == null)
            return;

        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
        }

        // Prepare the inactive source with the next track
        inactiveMusicSource.clip = newClip;
        inactiveMusicSource.volume = 0f;
        inactiveMusicSource.loop = true;
        inactiveMusicSource.Play();

        // Begin fading volumes over time
        fadeRoutine = StartCoroutine(CrossfadeRoutine(activeMusicSource, inactiveMusicSource, duration));

        // Swap source references so the new clip becomes active after the fade
        var temp = activeMusicSource;
        activeMusicSource = inactiveMusicSource;
        inactiveMusicSource = temp;
    }

    // Coroutine gradually blends volumes of two audio sources
    private IEnumerator CrossfadeRoutine(AudioSource from, AudioSource to, float duration)
    {
        float time = 0f;
        float start = from.volume;
        while (time < duration)
        {
            time += Time.deltaTime;
            float t = Mathf.Clamp01(time / duration);
            from.volume = Mathf.Lerp(start, 0f, t);
            to.volume = Mathf.Lerp(0f, 1f, t);
            yield return null;
        }
        from.Stop();
        from.clip = null;
        to.volume = 1f;
        fadeRoutine = null;
    }

    /// <summary>
    /// Releases the global instance reference when destroyed.
    /// </summary>
    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            if (fadeRoutine != null)
            {
                StopCoroutine(fadeRoutine);
            }
        }
    }
}
