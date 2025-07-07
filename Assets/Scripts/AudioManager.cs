#region AudioManager Overview
/*
 * Provides global music and sound effect control. Attach this component to a
 * GameObject in the starting scene and access <see cref="AudioManager.Instance"/>
 * to play effects:
 *
 *   AudioManager.Instance.PlaySound("Jump");
 *
 * Volume levels are saved via <see cref="SaveGameManager"/> so they persist
 * between sessions. The manager persists across scene loads.
 */
#endregion
using UnityEngine;

/// <summary>
/// Manages music and sound effect playback. This component is implemented
/// as a persistent singleton so sounds can continue across scene loads.
/// </summary>
/// <summary>
/// Extended in 2024 with cross-fading support so new music can blend
/// smoothly between stages. Two AudioSources swap roles when a new clip
/// is played and their volumes fade over a configurable duration.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    public AudioSource musicSource;
    [Tooltip("Secondary source used for cross-fading between tracks.")]
    public AudioSource musicSourceSecondary;
    public AudioSource effectsSource;

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
    /// Convenience overload that loads a clip from Resources/Audio by name.
    /// </summary>
    public void PlaySound(string clipName, float pitch = 1f)
    {
        if (string.IsNullOrEmpty(clipName) || effectsSource == null) return;
        AudioClip clip = Resources.Load<AudioClip>("Audio/" + clipName);
        if (clip != null)
        {
            PlaySound(clip, pitch);
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
