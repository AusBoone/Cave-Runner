using NUnit.Framework;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Tests for <see cref="LeaderboardClient"/> verifying JSON formatting and
/// local fallback behaviour when web requests fail.
/// </summary>
public class LeaderboardClientTests
{
    private class DummyClient : LeaderboardClient
    {
        public UnityWebRequest sentRequest;
        public bool succeed;
        public string payload;

        protected override IEnumerator SendWebRequest(UnityWebRequest req, System.Action<bool, string> cb)
        {
            sentRequest = req;
            cb?.Invoke(succeed, payload);
            yield break;
        }
    }

    [Test]
    public void UploadScore_FormatsBody()
    {
        var go = new GameObject("lb");
        var client = go.AddComponent<DummyClient>();
        var routine = client.UploadScore(42);
        while (routine.MoveNext()) { }

        var raw = (UploadHandlerRaw)client.sentRequest.uploadHandler;
        string body = System.Text.Encoding.UTF8.GetString(raw.data);
        Assert.IsTrue(body.Contains("\"score\":42"));
        Object.DestroyImmediate(go);
    }

    [Test]
    public void GetTopScores_FallsBackToLocal()
    {
        System.IO.File.Delete(System.IO.Path.Combine(
            Application.persistentDataPath, "savegame.json"));
        var saveObj = new GameObject("save");
        saveObj.AddComponent<SaveGameManager>();
        SaveGameManager.Instance.HighScore = 5;

        var go = new GameObject("lb");
        var client = go.AddComponent<DummyClient>();
        client.succeed = false; // simulate failure

        List<LeaderboardClient.ScoreEntry> result = null;
        var routine = client.GetTopScores(list => result = list);
        while (routine.MoveNext()) { }

        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(5, result[0].score);
        Object.DestroyImmediate(go);
        Object.DestroyImmediate(saveObj);
    }

    /// <summary>
    /// Fallback entry name should change based on the current language.
    /// </summary>
    [Test]
    public void GetTopScores_UsesLocalizedName()
    {
        System.IO.File.Delete(System.IO.Path.Combine(
            Application.persistentDataPath, "savegame.json"));
        var saveObj = new GameObject("saveL");
        saveObj.AddComponent<SaveGameManager>();
        SaveGameManager.Instance.HighScore = 2;

        var go = new GameObject("lb");
        var client = go.AddComponent<DummyClient>();
        client.succeed = false;

        LocalizationManager.SetLanguage("es");
        List<LeaderboardClient.ScoreEntry> result = null;
        var routine = client.GetTopScores(list => result = list);
        while (routine.MoveNext()) { }

        Assert.AreEqual("Local ES", result[0].name);
        Object.DestroyImmediate(go);
        Object.DestroyImmediate(saveObj);
    }
}
