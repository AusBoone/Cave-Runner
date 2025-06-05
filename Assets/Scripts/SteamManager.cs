using UnityEngine;
#if UNITY_STANDALONE
using Steamworks;
#endif

/// <summary>
/// Lightweight wrapper around Steamworks.NET for initializing the Steam API,
/// unlocking achievements and storing a single high score in the Steam Cloud.
/// </summary>
public class SteamManager : MonoBehaviour
{
    public static SteamManager Instance { get; private set; }

#if UNITY_STANDALONE
    private bool initialized;
#endif

    /// <summary>
    /// Initializes the Steam API on startup and enforces the singleton
    /// pattern so only one SteamManager exists.
    /// </summary>
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
#if UNITY_STANDALONE
            try
            {
                initialized = SteamAPI.Init();
            }
            catch (System.DllNotFoundException e)
            {
                Debug.LogError("Steamworks DLL not found: " + e);
            }
#endif
        }
        else
        {
            Destroy(gameObject);
        }
    }

#if UNITY_STANDALONE
    /// <summary>
    /// Processes Steam callbacks each frame if the API was successfully
    /// initialized.
    /// </summary>
    void Update()
    {
        if (initialized)
        {
            SteamAPI.RunCallbacks();
        }
    }
#endif

    /// <summary>
    /// Shuts down the Steam API when this component is destroyed.
    /// </summary>
    void OnDestroy()
    {
#if UNITY_STANDALONE
        if (Instance == this && initialized)
        {
            SteamAPI.Shutdown();
        }
#endif
    }

    /// <summary>
    /// Unlocks a Steam achievement by its identifier if the API is ready.
    /// </summary>
    public void UnlockAchievement(string id)
    {
#if UNITY_STANDALONE
        if (!initialized) return;
        bool achieved;
        if (SteamUserStats.GetAchievement(id, out achieved) && !achieved)
        {
            SteamUserStats.SetAchievement(id);
            SteamUserStats.StoreStats();
        }
#endif
    }

    /// <summary>
    /// Saves the provided high score value to the Steam Cloud.
    /// </summary>
    public void SaveHighScore(int score)
    {
#if UNITY_STANDALONE
        if (!initialized) return;
        byte[] data = System.BitConverter.GetBytes(score);
        SteamRemoteStorage.FileWrite("highscore.dat", data, data.Length);
#endif
    }

    /// <summary>
    /// Loads the high score from the Steam Cloud if it exists.
    /// </summary>
    public int LoadHighScore()
    {
#if UNITY_STANDALONE
        if (!initialized) return 0;
        if (SteamRemoteStorage.FileExists("highscore.dat"))
        {
            int size = SteamRemoteStorage.GetFileSize("highscore.dat");
            byte[] data = new byte[size];
            SteamRemoteStorage.FileRead("highscore.dat", data, size);
            return System.BitConverter.ToInt32(data, 0);
        }
#endif
        return 0;
    }
}
