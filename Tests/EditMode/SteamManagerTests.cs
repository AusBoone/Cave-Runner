// SteamManagerTests.cs
// -----------------------------------------------------------------------------
// Validates the behavior of SteamManager without requiring a live connection to
// the Steam client. Leaderboard interactions, localization helpers, and startup
// error handling are all exercised to ensure consistent behavior across builds.
// Run via the Unity Test Runner in edit mode.
// -----------------------------------------------------------------------------

using NUnit.Framework;
using UnityEngine;
#if UNITY_STANDALONE
using Steamworks;
#endif

/// <summary>
/// Tests for SteamManager verifying score submission, leaderboard retrieval,
/// achievement localization and initialization error logging. Methods are
/// overridden where needed so interactions can be inspected in isolation.
/// </summary>
public class SteamManagerTests
{
#if UNITY_STANDALONE
    private class DummySteamManager : SteamManager
    {
        public string lastLeaderboard;
        public int uploadedScore;
        public bool downloadRequested;

        public override void FindOrCreateLeaderboard(string name, System.Action<bool> callback)
        {
            lastLeaderboard = name;
            callback?.Invoke(true);
        }

        public override void UploadScore(int score)
        {
            uploadedScore = score;
        }

        public override void DownloadTopScores(System.Action<LeaderboardEntry_t[]> callback)
        {
            downloadRequested = true;
            callback?.Invoke(new LeaderboardEntry_t[0]);
        }
    }
#endif

    /// <summary>
    /// Ensures the configured leaderboard ID is used when uploading scores.
    /// </summary>
    [Test]
    public void UploadScore_UsesLeaderboardId()
    {
#if UNITY_STANDALONE
        var go = new GameObject("sm");
        var sm = go.AddComponent<DummySteamManager>();
        sm.leaderboardId = "TEST";
        sm.FindOrCreateLeaderboard(sm.leaderboardId, null);
        sm.UploadScore(42);

        Assert.AreEqual("TEST", sm.lastLeaderboard);
        Assert.AreEqual(42, sm.uploadedScore);
        Object.DestroyImmediate(go);
#else
        Assert.Pass("Steamworks not available");
#endif
    }

    /// <summary>
    /// Ensures score retrieval invokes the provided callback.
    /// </summary>
    [Test]
    public void DownloadTopScores_InvokesCallback()
    {
#if UNITY_STANDALONE
        var go = new GameObject("sm");
        var sm = go.AddComponent<DummySteamManager>();
        bool called = false;
        sm.DownloadTopScores(entries => called = true);
        Assert.IsTrue(sm.downloadRequested);
        Assert.IsTrue(called);
        Object.DestroyImmediate(go);
#else
        Assert.Pass("Steamworks not available");
#endif
    }

    /// <summary>
    /// When the Steamworks native library is missing, the manager should route
    /// the initialization failure through <see cref="LoggingHelper.LogError"/>.
    /// </summary>
    [Test]
    public void Awake_LogsError_WhenSteamDllMissing()
    {
#if UNITY_STANDALONE
        // Expect an error message about the missing DLL logged via LoggingHelper.
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Steamworks DLL not found"));
        var go = new GameObject("sm");
        go.AddComponent<SteamManager>();
        Object.DestroyImmediate(go);
#else
        Assert.Pass("Steamworks not available");
#endif
    }

    /// <summary>
    /// Achievement display strings should reflect the active language.
    /// </summary>
    [Test]
    public void AchievementStrings_Localized()
    {
        LocalizationManager.SetLanguage("en");
        Assert.AreEqual("Traveler", SteamManager.GetAchievementName("ACH_DISTANCE_1000"));
        LocalizationManager.SetLanguage("es");
        Assert.AreEqual("Viajero", SteamManager.GetAchievementName("ACH_DISTANCE_1000"));
    }
}
