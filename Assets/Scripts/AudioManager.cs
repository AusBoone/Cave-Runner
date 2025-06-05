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
        if (musicSource != null && !musicSource.isPlaying)
        {
            musicSource.loop = true;
            musicSource.Play();
        }
    }

    /// <summary>
    /// Plays a one-shot sound effect through the effects AudioSource.
    /// </summary>
    public void PlaySound(AudioClip clip)
    {
        if (clip != null && effectsSource != null)
        {
            effectsSource.PlayOneShot(clip);
        }
    }
}
