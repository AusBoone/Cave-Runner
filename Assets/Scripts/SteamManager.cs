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
    private SteamLeaderboard_t leaderboard;
    private CallResult<LeaderboardFindResult_t> findResult;
    private CallResult<LeaderboardScoresDownloaded_t> downloadResult;
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

#if UNITY_STANDALONE
    /// <summary>
    /// Finds or creates a Steam leaderboard with the given name.
    /// </summary>
    public void FindOrCreateLeaderboard(string name, System.Action<bool> callback)
    {
        if (!initialized)
        {
            callback?.Invoke(false);
            return;
        }

        var handle = SteamUserStats.FindOrCreateLeaderboard(
            name,
            ELeaderboardSortMethod.k_ELeaderboardSortMethodDescending,
            ELeaderboardDisplayType.k_ELeaderboardDisplayTypeNumeric);

        findResult = CallResult<LeaderboardFindResult_t>.Create((result, failure) =>
        {
            if (!failure && result.m_bLeaderboardFound != 0)
            {
                leaderboard = result.m_hSteamLeaderboard;
                callback?.Invoke(true);
            }
            else
            {
                callback?.Invoke(false);
            }
        });
        findResult.Set(handle);
    }

    /// <summary>
    /// Uploads a score to the current leaderboard if available.
    /// </summary>
    public void UploadScore(int score)
    {
        if (!initialized || leaderboard.m_SteamLeaderboard == 0) return;
        SteamUserStats.UploadLeaderboardScore(
            leaderboard,
            ELeaderboardUploadScoreMethod.k_ELeaderboardUploadScoreMethodKeepBest,
            score,
            null,
            0);
    }

    /// <summary>
    /// Downloads the top 10 scores from the current leaderboard.
    /// </summary>
    public void DownloadTopScores(System.Action<LeaderboardEntry_t[]> callback)
    {
        if (!initialized || leaderboard.m_SteamLeaderboard == 0)
        {
            callback?.Invoke(null);
            return;
        }

        var handle = SteamUserStats.DownloadLeaderboardEntries(
            leaderboard,
            ELeaderboardDataRequest.k_ELeaderboardDataRequestGlobal,
            1,
            10);

        downloadResult = CallResult<LeaderboardScoresDownloaded_t>.Create((result, failure) =>
        {
            if (failure)
            {
                callback?.Invoke(null);
                return;
            }

            LeaderboardEntry_t[] entries = new LeaderboardEntry_t[result.m_cEntryCount];
            for (int i = 0; i < result.m_cEntryCount; i++)
            {
                SteamUserStats.GetDownloadedLeaderboardEntry(
                    result.m_hSteamLeaderboardEntries,
                    i,
                    out entries[i],
                    null,
                    0);
            }
            callback?.Invoke(entries);
        });
        downloadResult.Set(handle);
    }
#endif
}
