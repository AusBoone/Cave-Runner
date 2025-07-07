using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Client for a simple REST-based leaderboard service used when Steamworks
/// is unavailable. The service exposes two endpoints:
/// <code>/scores</code> (GET) returns a JSON array of score objects and
/// <code>/scores</code> (POST) accepts a single score entry. Responses use
/// the format:
/// <pre>{"name":"Player","score":123}</pre>
/// All requests are sent using <see cref="UnityWebRequest"/> and any network
/// failures result in the caller receiving the local high score instead.
/// </summary>
public class LeaderboardClient : MonoBehaviour
{
    /// <summary>Global singleton instance.</summary>
    public static LeaderboardClient Instance { get; private set; }

    [Tooltip("Base URL of the leaderboard service e.g. https://example.com/api")]
    public string serviceUrl = "http://localhost:8080";

    [Tooltip("Name used when uploading scores. Defaults to 'Player'.")]
    public string playerName = "Player";

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// Represents a single leaderboard entry.
    /// </summary>
    [System.Serializable]
    public struct ScoreEntry
    {
        public string name;
        public int score;
    }

    [System.Serializable]
    private class ScoreList
    {
        public ScoreEntry[] scores;
    }

    /// <summary>
    /// Uploads <paramref name="score"/> associated with <see cref="playerName"/>.
    /// The request body is JSON of the form {"name":"Player","score":100}.
    /// </summary>
    public IEnumerator UploadScore(int score)
    {
        string url = serviceUrl.TrimEnd('/') + "/scores";
        string json = JsonUtility.ToJson(new ScoreEntry { name = playerName, score = score });

        using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
        {
            byte[] body = System.Text.Encoding.UTF8.GetBytes(json);
            req.uploadHandler = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            bool success = false;
            yield return SendWebRequest(req, (ok, _unused) => success = ok);
            if (!success)
            {
                Debug.LogWarning("Failed to upload score to leaderboard");
            }
        }
    }

    /// <summary>
    /// Retrieves the top scores from the service. If the request fails or
    /// returns invalid data, the local high score from
    /// <see cref="SaveGameManager"/> is provided instead.
    /// </summary>
    public IEnumerator GetTopScores(System.Action<List<ScoreEntry>> callback)
    {
        string url = serviceUrl.TrimEnd('/') + "/scores";
        List<ScoreEntry> result = null;

        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            req.downloadHandler = new DownloadHandlerBuffer();
            bool success = false;
            string text = null;
            yield return SendWebRequest(req, (ok, data) => { success = ok; text = data; });
            if (success)
            {
                result = ParseScores(text);
            }
        }

        if (result == null || result.Count == 0)
        {
            int local = SaveGameManager.Instance != null ? SaveGameManager.Instance.HighScore : 0;
            result = new List<ScoreEntry> { new ScoreEntry { name = "Local", score = local } };
        }
        callback?.Invoke(result);
    }

    /// <summary>
    /// Issues the web request and invokes the callback with the outcome.
    /// Tests override this method to provide fake responses.
    /// </summary>
    protected virtual IEnumerator SendWebRequest(UnityWebRequest req, System.Action<bool, string> callback)
    {
        string text = null;
        bool success = false;
        try
        {
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success)
            {
                text = req.downloadHandler.text;
                success = true;
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("Leaderboard request failed: " + ex.Message);
        }
        callback?.Invoke(success, text);
    }

    // Converts a raw JSON array into a list of score entries.
    private List<ScoreEntry> ParseScores(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return new List<ScoreEntry>();
        }
        try
        {
            // JsonUtility cannot parse a bare array so wrap it in an object
            string wrapped = "{\"scores\":" + json + "}";
            ScoreList list = JsonUtility.FromJson<ScoreList>(wrapped);
            if (list != null && list.scores != null)
            {
                return new List<ScoreEntry>(list.scores);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("Failed to parse leaderboard JSON: " + ex.Message);
        }
        return new List<ScoreEntry>();
    }
}

