// SteamManager.cs
// -----------------------------------------------------------------------------
// Manages interactions with the Steamworks API including achievements, cloud
// saves and leaderboard submissions. This revision funnels all Unity logging
// through LoggingHelper to centralize output and simplify testing of failure
// scenarios.
//
// The Steamworks.NET plugin must be installed for this script to function.
// Refer to the "Steamworks Setup" section of docs/DeveloperSetup.md for
// detailed installation steps, placement of the steam_appid.txt file and
// troubleshooting guidance when initialization fails.
// -----------------------------------------------------------------------------

using UnityEngine;
#if UNITY_STANDALONE
using Steamworks;
#endif
using System.Collections.Generic;

/// <summary>
/// Handles initialization of the Steamworks API, achievement unlocking,
/// cloud saves and leaderboard submission. The leaderboard identifier can
/// be configured in the inspector so different boards may be used in
/// various builds. This revision adds helper methods to fetch localized
/// achievement names and descriptions via <see cref="LocalizationManager"/>.
/// </summary>
public class SteamManager : MonoBehaviour
{
    public static SteamManager Instance { get; private set; }

    // Mapping of Steam achievement identifiers to localization keys for
    // display names and descriptions. These tables allow the UI to fetch
    // translated strings without relying on Steam's built-in text.
    private static readonly Dictionary<string, string> achievementNameKeys = new Dictionary<string, string>
    {
        { "ACH_DISTANCE_1000", "ach_distance_1000_name" },
        { "ACH_DISTANCE_5000", "ach_distance_5000_name" },
        { "ACH_COINS_50", "ach_coins_50_name" },
        { "ACH_COINS_200", "ach_coins_200_name" },
        { "ACH_DAILY_COMPLETE", "ach_daily_complete_name" },
        { "ACH_COMBO_10", "ach_combo_10_name" },
        { "ACH_FIRST_BOSS", "ach_boss_first_name" },
        { "ACH_HARDCORE_WIN", "ach_hardcore_win_name" }
    };

    private static readonly Dictionary<string, string> achievementDescKeys = new Dictionary<string, string>
    {
        { "ACH_DISTANCE_1000", "ach_distance_1000_desc" },
        { "ACH_DISTANCE_5000", "ach_distance_5000_desc" },
        { "ACH_COINS_50", "ach_coins_50_desc" },
        { "ACH_COINS_200", "ach_coins_200_desc" },
        { "ACH_DAILY_COMPLETE", "ach_daily_complete_desc" },
        { "ACH_COMBO_10", "ach_combo_10_desc" },
        { "ACH_FIRST_BOSS", "ach_boss_first_desc" },
        { "ACH_HARDCORE_WIN", "ach_hardcore_win_desc" }
    };

#if UNITY_STANDALONE
    private bool initialized;
    private SteamLeaderboard_t leaderboard;
    private CallResult<LeaderboardFindResult_t> findResult;
    private CallResult<LeaderboardScoresDownloaded_t> downloadResult;
#endif

    [Tooltip("Steam leaderboard identifier used for global scores.")]
    public string leaderboardId = "HIGHSCORES";

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
                // Route initialization failures through the centralized helper
                // so test cases can assert on the emitted error message.
                LoggingHelper.LogError("Steamworks DLL not found: " + e);
            }
            // Pre-load the leaderboard so score submissions succeed
            FindOrCreateLeaderboard(leaderboardId, null);
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
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// Unlocks a Steam achievement by its identifier if the API is ready.
    /// </summary>
    /// <summary>
    /// Unlocks a Steam achievement by its identifier if the API is ready.
    /// Virtual so tests can override without calling the real API.
    /// </summary>
    public virtual void UnlockAchievement(string id)
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
    public virtual void SaveHighScore(int score)
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
    public virtual int LoadHighScore()
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

    /// <summary>
    /// Retrieves the localized display name for the given achievement.
    /// Falls back to <paramref name="id"/> when no mapping exists.
    /// </summary>
    public static string GetAchievementName(string id)
    {
        if (achievementNameKeys.TryGetValue(id, out string key))
        {
            return LocalizationManager.Get(key);
        }
        return id;
    }

    /// <summary>
    /// Retrieves the localized description for the given achievement.
    /// Returns <paramref name="id"/> if no translation key is defined.
    /// </summary>
    public static string GetAchievementDescription(string id)
    {
        if (achievementDescKeys.TryGetValue(id, out string key))
        {
            return LocalizationManager.Get(key);
        }
        return id;
    }

#if UNITY_STANDALONE
    /// <summary>
    /// Finds or creates a Steam leaderboard with the given name.
    /// </summary>
    public virtual void FindOrCreateLeaderboard(string name, System.Action<bool> callback)
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
    public virtual void UploadScore(int score)
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
    public virtual void DownloadTopScores(System.Action<LeaderboardEntry_t[]> callback)
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
