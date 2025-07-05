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
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    public AudioSource musicSource;
    public AudioSource effectsSource;

// Name of the music clip located under Assets/Audio/Resources.
    public string backgroundMusicName = "Background";
    private AudioClip backgroundMusic;

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
        }
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

        if (musicSource != null && !musicSource.isPlaying)
        {
            if (backgroundMusic == null && !string.IsNullOrEmpty(backgroundMusicName))
            {
                backgroundMusic = Resources.Load<AudioClip>("Audio/" + backgroundMusicName);
            }
            if (backgroundMusic != null)
            {
                musicSource.clip = backgroundMusic;
                musicSource.loop = true;
                musicSource.Play();
            }
        }
    }

    /// <summary>
    /// Adjusts the music source volume. The provided value is clamped to the
    /// [0,1] range to match Unity's expected AudioSource volume.
    /// </summary>
    public void SetMusicVolume(float volume)
    {
        if (musicSource != null)
        {
            musicSource.volume = Mathf.Clamp01(volume);
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
    /// Releases the global instance reference when destroyed.
    /// </summary>
    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
