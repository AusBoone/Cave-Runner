using NUnit.Framework;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;
using System.Collections;

/// <summary>
/// Integration-style tests exercising <see cref="LeaderboardClient.SendWebRequest"/>
/// against real <see cref="UnityWebRequest"/> instances to verify that specific
/// failure modes surface the correct error codes and that
/// <see cref="UIManager.ShowLeaderboardError"/> converts those codes into
/// readable messages for the player.
/// </summary>
public class LeaderboardClientIntegrationTests
{
    /// <summary>
    /// Helper client exposing the protected <see cref="LeaderboardClient.SendWebRequest"/>
    /// so tests can drive it directly.
    /// </summary>
    private class PublicClient : LeaderboardClient
    {
        public IEnumerator Invoke(UnityWebRequest req, System.Action<bool, string, LeaderboardClient.ErrorCode> cb)
        {
            return SendWebRequest(req, cb);
        }
    }

    /// <summary>
    /// Certificate handler that deliberately fails validation to simulate TLS
    /// problems such as expired or self‑signed certificates.
    /// </summary>
    private class RejectingCertificateHandler : CertificateHandler
    {
        protected override bool ValidateCertificate(byte[] certificateData)
        {
            return false;
        }
    }

    [Test]
    public void SendWebRequest_ReportsCertificateError()
    {
        var go = new GameObject("lbCert");
        var client = go.AddComponent<PublicClient>();
        var req = UnityWebRequest.Get("https://www.example.com");
        req.certificateHandler = new RejectingCertificateHandler();

        LeaderboardClient.ErrorCode err = LeaderboardClient.ErrorCode.None;
        var routine = client.Invoke(req, (ok, _text, code) => err = code);
        while (routine.MoveNext()) { }
        Assert.AreEqual(LeaderboardClient.ErrorCode.CertificateError, err);

        // Verify UI displays the expected message for this error category.
        var uiObj = new GameObject("ui");
        var ui = uiObj.AddComponent<UIManager>();
        var textObj = new GameObject("txt");
        var text = textObj.AddComponent<TextMeshProUGUI>();
        ui.leaderboardText = text;
        ui.ShowLeaderboardError(err);
        Assert.AreEqual("Certificate validation failed.", text.text);

        Object.DestroyImmediate(textObj);
        Object.DestroyImmediate(uiObj);
        Object.DestroyImmediate(go);
    }

    [Test]
    public void SendWebRequest_ReportsTimeoutError()
    {
        var go = new GameObject("lbTimeout");
        var client = go.AddComponent<PublicClient>();
        // Use an unroutable IP to trigger a timeout quickly.
        var req = UnityWebRequest.Get("http://10.255.255.1");
        req.timeout = 1; // seconds

        LeaderboardClient.ErrorCode err = LeaderboardClient.ErrorCode.None;
        var routine = client.Invoke(req, (ok, _text, code) => err = code);
        while (routine.MoveNext()) { }
        Assert.AreEqual(LeaderboardClient.ErrorCode.Timeout, err);

        // UI should present a friendly timeout message.
        var uiObj = new GameObject("ui2");
        var ui = uiObj.AddComponent<UIManager>();
        var textObj = new GameObject("txt2");
        var text = textObj.AddComponent<TextMeshProUGUI>();
        ui.leaderboardText = text;
        ui.ShowLeaderboardError(err);
        Assert.AreEqual("Request timed out.", text.text);

        Object.DestroyImmediate(textObj);
        Object.DestroyImmediate(uiObj);
        Object.DestroyImmediate(go);
    }
}
