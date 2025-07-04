using NUnit.Framework;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

/// <summary>
/// Tests for the AnalyticsManager ensuring failed network requests
/// keep data queued locally and that the retry system triggers.
/// </summary>
public class AnalyticsManagerTests
{
    private class TestAnalyticsManager : AnalyticsManager
    {
        public int delayCalls;
        protected override YieldInstruction RetryDelay(float seconds)
        {
            delayCalls++;
            return null; // skip real waiting in tests
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
        am.remoteEndpoint = "http://invalid.invalid"; // unreachable
        am.maxRetries = 0;

        am.LogRun(5f, 10, true);

        var routine = am.SendData();
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
        am.remoteEndpoint = "http://invalid.invalid"; // unreachable
        am.maxRetries = 1;
        am.retryBackoff = 0f; // remove delay

        am.LogRun(1f, 1, false);

        var routine = am.SendData();
        int iterations = 0;
        while (routine.MoveNext() && iterations < 10) { iterations++; }

        Assert.GreaterOrEqual(am.delayCalls, 1);
        var field = typeof(AnalyticsManager).GetField("runs", BindingFlags.NonPublic | BindingFlags.Instance);
        var list = (IList)field.GetValue(am);
        Assert.AreEqual(1, list.Count);
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

