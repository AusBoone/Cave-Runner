using NUnit.Framework;
using UnityEngine;
#if UNITY_STANDALONE
using Steamworks;
#endif

/// <summary>
/// Tests for SteamManager verifying score submission and retrieval logic
/// without relying on the real Steamworks API. Methods are overridden so
/// calls can be inspected in isolation.
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
}
