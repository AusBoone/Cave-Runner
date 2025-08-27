using NUnit.Framework;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Networking; // Required for UnityWebRequest types used in tests
using UnityEngine.TestTools; // Enables log checking utilities
using System.Net; // Provides HttpListener for mock HTTP servers
using System.Threading.Tasks; // Supports asynchronous server responses

/// <summary>
/// Tests for <see cref="LeaderboardClient"/> verifying JSON formatting,
/// local fallback behaviour when web requests fail, and enforcement of the
/// HTTPS requirement on <c>serviceUrl</c>.
/// </summary>
public class LeaderboardClientTests
{
    private class DummyClient : LeaderboardClient
    {
        public UnityWebRequest sentRequest;
        public bool succeed;
        public string payload;

        protected override IEnumerator SendWebRequest(UnityWebRequest req, System.Action<bool, string, LeaderboardClient.ErrorCode> cb)
        {
            sentRequest = req;
            var code = succeed ? LeaderboardClient.ErrorCode.None : LeaderboardClient.ErrorCode.NetworkError;
            cb?.Invoke(succeed, payload, code);
            yield break;
        }
    }

    /// <summary>
    /// Helper client exposing <see cref="SendWebRequest"/> for direct testing
    /// of error-handling behaviour with a real <see cref="UnityWebRequest"/>.
    /// </summary>
    private class PublicClient : LeaderboardClient
    {
        public IEnumerator InvokeSend(UnityWebRequest req, System.Action<bool, string, LeaderboardClient.ErrorCode> cb)
        {
            return SendWebRequest(req, cb);
        }
    }

    /// <summary>
    /// Client used to verify retry behaviour. It records how many times a
    /// request is attempted and optionally succeeds on a specific attempt.
    /// </summary>
    private class RetryClient : LeaderboardClient
    {
        public int calls = 0;
        public int succeedOn = int.MaxValue; // Attempt index (1-based) when request should succeed
        public int lastTimeout;

        protected override IEnumerator SendWebRequest(UnityWebRequest req, System.Action<bool, string, LeaderboardClient.ErrorCode> cb)
        {
            calls++;
            lastTimeout = req.timeout;
            bool ok = calls == succeedOn;
            var code = ok ? LeaderboardClient.ErrorCode.None : LeaderboardClient.ErrorCode.NetworkError;
            cb?.Invoke(ok, null, code);
            yield break;
        }
    }

    /// <summary>
    /// Client used to verify that the network spinner is shown during
    /// asynchronous web requests.
    /// </summary>
    private class SpinnerClient : LeaderboardClient
    {
        protected override IEnumerator SendWebRequest(UnityWebRequest req, System.Action<bool, string, LeaderboardClient.ErrorCode> cb)
        {
            yield return null; // simulate an async web call
            cb?.Invoke(true, "[]", LeaderboardClient.ErrorCode.None);
        }
    }

    [Test]
    public void UploadScore_FormatsBody()
    {
        var go = new GameObject("lb");
        var client = go.AddComponent<DummyClient>();
        client.serviceUrl = "https://example.com"; // ensure URL passes HTTPS check
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
        client.serviceUrl = "https://example.com";
        client.succeed = false; // simulate failure

        List<LeaderboardClient.ScoreEntry> result = null;
        bool success = true;
        var routine = client.GetTopScores((list, ok, _err) => { result = list; success = ok; });
        while (routine.MoveNext()) { }

        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(5, result[0].score);
        Assert.IsFalse(success, "Callback should report failure when falling back to local scores");
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
        client.serviceUrl = "https://example.com";
        client.succeed = false;

        LocalizationManager.SetLanguage("es");
        List<LeaderboardClient.ScoreEntry> result = null;
        bool success = true;
        var routine = client.GetTopScores((list, ok, _err) => { result = list; success = ok; });
        while (routine.MoveNext()) { }

        Assert.AreEqual("Local ES", result[0].name);
        Assert.IsFalse(success, "Localized fallback should still report failure");
        Object.DestroyImmediate(go);
        Object.DestroyImmediate(saveObj);
    }

    /// <summary>
    /// Successful web requests should report a positive status through the
    /// callback so callers know remote data was used.
    /// </summary>
    [Test]
    public void GetTopScores_ReportsSuccess()
    {
        var go = new GameObject("lbOk");
        var client = go.AddComponent<DummyClient>();
        client.serviceUrl = "https://example.com";
        client.succeed = true;
        client.payload = "[{\"name\":\"A\",\"score\":1}]"; // valid JSON array

        List<LeaderboardClient.ScoreEntry> result = null;
        bool success = false;
        var routine = client.GetTopScores((list, ok, _err) => { result = list; success = ok; });
        while (routine.MoveNext()) { }

        Assert.IsTrue(success, "Callback should report success when request completes");
        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.Count);

        Object.DestroyImmediate(go);
    }

    /// <summary>
    /// Network requests should be skipped when the service URL is missing or
    /// uses HTTP instead of HTTPS.
    /// </summary>
    [Test]
    public void ServiceUrl_MustUseHttps()
    {
        var go = new GameObject("lbSecure");
        var client = go.AddComponent<DummyClient>();

        // Empty URL is rejected.
        var routine = client.UploadScore(10);
        while (routine.MoveNext()) { }
        Assert.IsNull(client.sentRequest, "Request should not be sent without a URL");

        // HTTP URL is also rejected.
        client.serviceUrl = "http://insecure";
        routine = client.UploadScore(10);
        while (routine.MoveNext()) { }
        Assert.IsNull(client.sentRequest, "Request should not be sent over HTTP");

        Object.DestroyImmediate(go);
    }

    /// <summary>
    /// UploadScore should surface a NetworkError when no valid HTTPS URL is
    /// configured. This ensures misconfiguration is treated the same as other
    /// connectivity issues so callers can present a consistent message.
    /// </summary>
    [Test]
    public void UploadScore_InvalidUrlReportsNetworkError()
    {
        var go = new GameObject("lbInvalidUrl");
        var client = go.AddComponent<LeaderboardClient>(); // Uses default empty serviceUrl

        bool success = true;
        LeaderboardClient.ErrorCode err = LeaderboardClient.ErrorCode.None;

        var routine = client.UploadScore(10, (ok, code) => { success = ok; err = code; });
        while (routine.MoveNext()) { }

        Assert.IsFalse(success, "Upload should fail when serviceUrl is invalid");
        Assert.AreEqual(LeaderboardClient.ErrorCode.NetworkError, err,
            "Invalid URL should report a NetworkError");

        Object.DestroyImmediate(go);
    }

    /// <summary>
    /// Upload operations should retry on failure and apply the configured
    /// timeout to each attempt.
    /// </summary>
    [Test]
    public void UploadScore_RetriesAndSetsTimeout()
    {
        var go = new GameObject("lbRetry");
        var client = go.AddComponent<RetryClient>();
        client.serviceUrl = "https://example.com"; // pass HTTPS validation
        client.succeedOn = 2; // Fail first attempt, succeed on second
        bool result = false;

        var routine = client.UploadScore(10, (ok, _err) => result = ok);
        while (routine.MoveNext()) { }

        Assert.AreEqual(2, client.calls, "Upload should retry once before succeeding");
        Assert.AreEqual(10, client.lastTimeout, "Request timeout should be applied");
        Assert.IsTrue(result, "Callback should report success after retries");

        Object.DestroyImmediate(go);
    }

    /// <summary>
    /// UploadScore should surface failure through its callback so callers can
    /// react appropriately (e.g., display an error message).
    /// </summary>
    [Test]
    public void UploadScore_ReportsStatus()
    {
        var go = new GameObject("lbStatus");
        var client = go.AddComponent<DummyClient>();
        client.serviceUrl = "https://example.com";

        // Simulate successful upload
        client.succeed = true;
        bool success = false;
        var routine = client.UploadScore(1, (ok, _err) => success = ok);
        while (routine.MoveNext()) { }
        Assert.IsTrue(success, "Callback should report success when request succeeds");

        // Simulate failure
        client.succeed = false;
        success = true;
        routine = client.UploadScore(1, (ok, _err) => success = ok);
        while (routine.MoveNext()) { }
        Assert.IsFalse(success, "Callback should report failure when request fails");

        Object.DestroyImmediate(go);
    }

    /// <summary>
    /// Network spinner should appear while leaderboard data downloads and be
    /// hidden once the coroutine completes.
    /// </summary>
    [Test]
    public void GetTopScores_TogglesNetworkSpinner()
    {
        var uiObj = new GameObject("ui");
        var ui = uiObj.AddComponent<UIManager>();
        ui.networkSpinner = new GameObject("spinner");

        var go = new GameObject("lbSpin");
        var client = go.AddComponent<SpinnerClient>();
        client.serviceUrl = "https://example.com";

        var routine = client.GetTopScores(null);

        // Spinner should activate on first step of the coroutine.
        Assert.IsFalse(ui.networkSpinner.activeSelf);
        Assert.IsTrue(routine.MoveNext());
        Assert.IsTrue(ui.networkSpinner.activeSelf);

        while (routine.MoveNext()) { }
        Assert.IsFalse(ui.networkSpinner.activeSelf);

        Object.DestroyImmediate(go);
        Object.DestroyImmediate(ui.networkSpinner);
        Object.DestroyImmediate(uiObj);
    }

    /// <summary>
    /// SendWebRequest should differentiate between client and server errors by
    /// inspecting the HTTP status code.
    /// </summary>
    [Test]
    public void SendWebRequest_LogsErrorCategories()
    {
        // Start a simple HTTP server that returns 404 to trigger a client error
        int port = 8085;
        var listener = new HttpListener();
        listener.Prefixes.Add($"http://localhost:{port}/");
        listener.Start();
        Task.Run(() =>
        {
            var ctx = listener.GetContext();
            ctx.Response.StatusCode = 404;
            ctx.Response.Close();
            listener.Stop();
        });

        var go = new GameObject("lbErr");
        var client = go.AddComponent<PublicClient>();
        var req = UnityWebRequest.Get($"http://localhost:{port}/");

        // Expect a log message mentioning a client error (4xx)
        LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Client error 404"));

        var routine = client.InvokeSend(req, (ok, _text, err) =>
        {
            Assert.IsFalse(ok);
            Assert.AreEqual(LeaderboardClient.ErrorCode.HttpError, err);
        });
        while (routine.MoveNext()) { }

        Object.DestroyImmediate(go);

        // Repeat with a server error
        port = 8086;
        listener = new HttpListener();
        listener.Prefixes.Add($"http://localhost:{port}/");
        listener.Start();
        Task.Run(() =>
        {
            var ctx = listener.GetContext();
            ctx.Response.StatusCode = 500;
            ctx.Response.Close();
            listener.Stop();
        });

        go = new GameObject("lbErr2");
        client = go.AddComponent<PublicClient>();
        req = UnityWebRequest.Get($"http://localhost:{port}/");

        LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Server error 500"));

        routine = client.InvokeSend(req, (ok, _text, err) =>
        {
            Assert.IsFalse(ok);
            Assert.AreEqual(LeaderboardClient.ErrorCode.HttpError, err);
        });
        while (routine.MoveNext()) { }

        Object.DestroyImmediate(go);
    }
}
