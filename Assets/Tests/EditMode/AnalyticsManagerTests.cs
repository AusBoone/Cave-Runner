using NUnit.Framework;
using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

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
}

