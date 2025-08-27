using NUnit.Framework;
using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine.TestTools; // For LogAssert to verify logging

/// <summary>
/// Tests for the AnalyticsManager ensuring failed network requests
/// keep data queued locally and that the retry system triggers.
/// </summary>
public class AnalyticsManagerTests
{
    private class MockRequest : AnalyticsManager.IWebRequest
    {
        private readonly Queue<UnityWebRequest.Result> results;
        public int sendCount;

        public MockRequest(IEnumerable<UnityWebRequest.Result> results)
        {
            this.results = new Queue<UnityWebRequest.Result>(results);
        }

        public float UploadProgress => 1f;
        public bool IsDone => true;
        public UnityWebRequest.Result Result { get; private set; }
        public string Error => Result == UnityWebRequest.Result.Success ? null : "error";

        public IEnumerator Send()
        {
            sendCount++;
            Result = results.Dequeue();
            yield break;
        }
    }

    private class TestAnalyticsManager : AnalyticsManager
    {
        public int delayCalls;
        public Queue<UnityWebRequest.Result> mockResults = new Queue<UnityWebRequest.Result>();

        protected override YieldInstruction RetryDelay(float seconds)
        {
            delayCalls++;
            return null; // skip real waiting in tests
        }

        protected override IWebRequest CreateWebRequest(string url, byte[] body)
        {
            return new MockRequest(mockResults);
        }
    }

    /// <summary>
    /// Analytics manager subclass that exposes a deterministic web request
    /// yielding once to simulate an asynchronous upload for spinner testing.
    /// </summary>
    private class SpinnerAnalyticsManager : AnalyticsManager
    {
        public Queue<UnityWebRequest.Result> mockResults = new Queue<UnityWebRequest.Result>();

        protected override IWebRequest CreateWebRequest(string url, byte[] body)
        {
            return new SpinnerRequest(mockResults);
        }

        protected override YieldInstruction RetryDelay(float seconds)
        {
            return null; // skip waits in tests
        }

        private class SpinnerRequest : IWebRequest
        {
            private readonly Queue<UnityWebRequest.Result> results;
            public SpinnerRequest(Queue<UnityWebRequest.Result> results)
            {
                this.results = results;
            }

            public float UploadProgress => 1f;
            public bool IsDone => true;
            public UnityWebRequest.Result Result { get; private set; }
            public string Error => null;

            public IEnumerator Send()
            {
                yield return null; // simulate network delay
                Result = results.Dequeue();
            }
        }
    }

    [SetUp]
    public void ClearPrefs()
    {
        PlayerPrefs.DeleteAll();
    }

    [Test]
    public void FailedSend_KeepsRunData()
    {
        var go = new GameObject("am");
        var am = go.AddComponent<TestAnalyticsManager>();
        am.remoteEndpoint = "http://example.com";
        am.maxRetries = 0;
        am.mockResults.Enqueue(UnityWebRequest.Result.ProtocolError); // fail once

        // Expect a warning log about the failed send through LoggingHelper.
        LogAssert.Expect(LogType.Warning, "Failed to send analytics attempt 1: error");

        am.LogRun(5f, 10, true);

        var method = typeof(AnalyticsManager).GetMethod("UploadLoop", BindingFlags.NonPublic | BindingFlags.Instance);
        var routine = (IEnumerator)method.Invoke(am, null);
        while (routine.MoveNext()) { }

        var field = typeof(AnalyticsManager).GetField("runs", BindingFlags.NonPublic | BindingFlags.Instance);
        var list = (List<object>)field.GetValue(am);
        Assert.AreEqual(1, list.Count);
        Object.DestroyImmediate(go);
    }

    [Test]
    public void Retries_OccurWhenEnabled()
    {
        var go = new GameObject("am");
        var am = go.AddComponent<TestAnalyticsManager>();
        am.remoteEndpoint = "http://example.com";
        am.maxRetries = 2;
        am.retryBackoff = 0f; // remove delay
        am.mockResults.Enqueue(UnityWebRequest.Result.ProtocolError);
        am.mockResults.Enqueue(UnityWebRequest.Result.Success);

        am.LogRun(1f, 1, false);

        var method = typeof(AnalyticsManager).GetMethod("UploadLoop", BindingFlags.NonPublic | BindingFlags.Instance);
        var routine = (IEnumerator)method.Invoke(am, null);
        while (routine.MoveNext()) { }

        Assert.GreaterOrEqual(am.delayCalls, 1); // retry occurred
        var field = typeof(AnalyticsManager).GetField("runs", BindingFlags.NonPublic | BindingFlags.Instance);
        var list = (IList)field.GetValue(am);
        Assert.AreEqual(0, list.Count); // cleared after success
        Object.DestroyImmediate(go);
    }

    [Test]
    public void GetAverageDistance_ReturnsAverageOfSubset()
    {
        var go = new GameObject("am");
        var am = go.AddComponent<AnalyticsManager>();
        am.LogRun(100f, 0, true);
        am.LogRun(200f, 0, true);
        am.LogRun(300f, 0, true);

        float avg = am.GetAverageDistance(2); // use last two runs
        Assert.AreEqual(250f, avg);
        Object.DestroyImmediate(go);
    }

    [Test]
    public void GetAverageDistance_ReturnsZeroWithNoRuns()
    {
        var go = new GameObject("am");
        var am = go.AddComponent<AnalyticsManager>();

        Assert.AreEqual(0f, am.GetAverageDistance(5));
        Object.DestroyImmediate(go);
    }

    [Test]
    public void RunHistory_DoesNotExceedConfiguredCap()
    {
        // Verify that adding more runs than the configured limit discards
        // the oldest entries so the list never grows unbounded.
        var go = new GameObject("am");
        var am = go.AddComponent<AnalyticsManager>();
        am.maxStoredRuns = 3; // small limit for test clarity

        // Log five runs; only the last three should remain after trimming.
        for (int i = 1; i <= 5; i++)
        {
            am.LogRun(i, 0, false);
        }

        // Access private run list via reflection to inspect internal state.
        var field = typeof(AnalyticsManager).GetField("runs", BindingFlags.NonPublic | BindingFlags.Instance);
        var list = (System.Collections.IList)field.GetValue(am);

        Assert.AreEqual(3, list.Count, "Run list exceeded configured cap");

        // Oldest entry should be distance 3 after trimming 1 and 2.
        var firstRun = list[0];
        var distanceField = firstRun.GetType().GetField("distance", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        Assert.AreEqual(3f, (float)distanceField.GetValue(firstRun));

        Object.DestroyImmediate(go);
    }

    /// <summary>
    /// The network spinner should reflect analytics upload activity so players
    /// are aware when data is being transmitted.
    /// </summary>
    [Test]
    public void UploadLoop_TogglesNetworkSpinner()
    {
        // Setup UI manager and spinner object.
        var uiObj = new GameObject("ui");
        var ui = uiObj.AddComponent<UIManager>();
        ui.networkSpinner = new GameObject("spinner");

        // Configure analytics manager with a single run and a mock request.
        var go = new GameObject("amSpin");
        var am = go.AddComponent<SpinnerAnalyticsManager>();
        am.remoteEndpoint = "http://example.com"; // non-empty to trigger send
        am.mockResults.Enqueue(UnityWebRequest.Result.Success);
        am.LogRun(1f, 0, false);

        var method = typeof(AnalyticsManager).GetMethod("UploadLoop", BindingFlags.NonPublic | BindingFlags.Instance);
        var routine = (IEnumerator)method.Invoke(am, null);

        // Spinner becomes visible on first iteration of the coroutine.
        Assert.IsFalse(ui.networkSpinner.activeSelf);
        Assert.IsTrue(routine.MoveNext());
        Assert.IsTrue(ui.networkSpinner.activeSelf);

        while (routine.MoveNext()) { }
        Assert.IsFalse(ui.networkSpinner.activeSelf);

        Object.DestroyImmediate(go);
        Object.DestroyImmediate(ui.networkSpinner);
        Object.DestroyImmediate(uiObj);
    }
}

