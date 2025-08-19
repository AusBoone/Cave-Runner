using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

// LocalizationManager is used to fetch translated strings for fallback labels.
// This revision reads the local-player label and entry format from the
// localization tables so leaderboard text updates when the language changes.
//
// Modification summary: default service URL is now empty and network operations
// are guarded by a runtime HTTPS check to prevent accidental insecure traffic.
// Additional revisions introduce request timeouts, automatic retries with
// exponential backoff, and richer error reporting so callers can distinguish
// between client and server failures. The latest update exposes explicit
// success flags from upload and download operations so the UI can present
// meaningful error messages when network communication fails.

/// <summary>
/// Client for a simple REST-based leaderboard service used when Steamworks
/// is unavailable. The service exposes two endpoints:
/// <code>/scores</code> (GET) returns a JSON array of score objects and
/// <code>/scores</code> (POST) accepts a single score entry. Responses use
/// the format:
/// <pre>{"name":"Player","score":123}</pre>
/// All requests are sent using <see cref="UnityWebRequest"/> and any network
/// failures result in the caller receiving the local high score instead.
/// <para>
/// <b>Security:</b> the leaderboard service must be hosted on a secure HTTPS
/// endpoint. An empty or non-HTTPS <see cref="serviceUrl"/> will cause all
/// network operations to be skipped at runtime.
/// </para>
/// </summary>
public class LeaderboardClient : MonoBehaviour
{
    /// <summary>Global singleton instance.</summary>
    public static LeaderboardClient Instance { get; private set; }

    [Tooltip("Base URL of the leaderboard service (must be HTTPS)")]
    public string serviceUrl = "";

    [Tooltip("Name used when uploading scores. Defaults to 'Player'.")]
    public string playerName = "Player";

    // Timeout in seconds applied to all leaderboard HTTP requests. Chosen to be
    // short enough for responsive UI while still tolerating minor network
    // hiccups.
    private const int RequestTimeoutSeconds = 10;

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
    /// A callback reports whether the upload ultimately succeeded so callers
    /// can react to network failures.
    /// </summary>
    /// <param name="score">Score value to submit.</param>
    /// <param name="onComplete">Optional callback receiving a success flag.</param>
    public IEnumerator UploadScore(int score, System.Action<bool> onComplete = null)
    {
        // Ensure serviceUrl is configured and uses HTTPS before attempting
        // to communicate with the leaderboard. Failing to validate here would
        // allow insecure plaintext traffic or null requests.
        if (!IsServiceUrlSecure())
        {
            onComplete?.Invoke(false);
            yield break;
        }

        string url = serviceUrl.TrimEnd('/') + "/scores";
        string json = JsonUtility.ToJson(new ScoreEntry { name = playerName, score = score });

        // Retry the upload a few times with exponential backoff to handle
        // transient network failures such as dropped connections or timeouts.
        const int maxRetries = 3;
        int attempt = 0;
        bool success = false;
        while (attempt < maxRetries && !success)
        {
            using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
            {
                byte[] body = System.Text.Encoding.UTF8.GetBytes(json);
                req.uploadHandler = new UploadHandlerRaw(body);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.timeout = RequestTimeoutSeconds; // Seconds before the request automatically times out.

                yield return SendWebRequest(req, (ok, _unused) => success = ok);
            }

            // Exponential backoff: 1s, 2s, 4s delays between attempts.
            if (!success && ++attempt < maxRetries)
            {
                float delay = Mathf.Pow(2, attempt);
                Debug.LogWarning($"Retrying score upload in {delay:F0}s (attempt {attempt + 1}/{maxRetries})");
                yield return new WaitForSeconds(delay);
            }
        }

        if (!success)
        {
            Debug.LogWarning("Failed to upload score to leaderboard");
        }
        // Notify caller of the final outcome. This enables UI elements to
        // display error messages or retry options.
        onComplete?.Invoke(success);
    }

    /// <summary>
    /// Retrieves the top scores from the service. If the request fails or
    /// returns invalid data, the local high score from
    /// <see cref="SaveGameManager"/> is provided instead. The callback also
    /// receives a success flag indicating whether remote communication
    /// succeeded so callers can present error messages.
    /// </summary>
    /// <param name="callback">Invoked with the retrieved scores and a success flag.</param>
    public virtual IEnumerator GetTopScores(System.Action<List<ScoreEntry>, bool> callback)
    {
        List<ScoreEntry> result = null; // Final list of scores to return
        bool success = false;           // Tracks whether the remote request succeeded

        // Validate serviceUrl to enforce secure communication. If invalid,
        // immediately return the local high score without issuing any web
        // requests.
        if (IsServiceUrlSecure())
        {
            string url = serviceUrl.TrimEnd('/') + "/scores";
            const int maxRetries = 3;
            int attempt = 0;
            string text = null;
            while (attempt < maxRetries && !success)
            {
                using (UnityWebRequest req = UnityWebRequest.Get(url))
                {
                    req.downloadHandler = new DownloadHandlerBuffer();
                    req.timeout = RequestTimeoutSeconds; // Seconds before the request automatically times out.
                    yield return SendWebRequest(req, (ok, data) => { success = ok; text = data; });
                }

                if (!success && ++attempt < maxRetries)
                {
                    float delay = Mathf.Pow(2, attempt);
                    Debug.LogWarning($"Retrying score download in {delay:F0}s (attempt {attempt + 1}/{maxRetries})");
                    yield return new WaitForSeconds(delay);
                }
            }

            if (success)
            {
                result = ParseScores(text);
            }
        }

        // Fallback when the serviceUrl is invalid, the request fails, or the
        // response is empty/invalid. In these scenarios the operation is
        // considered unsuccessful even though a list is still returned.
        if (result == null || result.Count == 0)
        {
            int local = SaveGameManager.Instance != null ? SaveGameManager.Instance.HighScore : 0;
            string name = LocalizationManager.Get("leaderboard_local_player");
            result = new List<ScoreEntry> { new ScoreEntry { name = name, score = local } };
            success = false;
        }

        // Callback receives both the scores to display and whether they came
        // from the remote service (true) or a local fallback (false).
        callback?.Invoke(result, success);
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
            else
            {
                long code = req.responseCode;
                if (code >= 400 && code < 500)
                {
                    Debug.LogWarning($"Client error {code} during leaderboard request: {req.error}");
                }
                else if (code >= 500)
                {
                    Debug.LogWarning($"Server error {code} during leaderboard request: {req.error}");
                }
                else
                {
                    Debug.LogWarning("Leaderboard request failed: " + req.error);
                }
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

    /// <summary>
    /// Validates that <see cref="serviceUrl"/> is a non-empty HTTPS URL.
    /// Enforcing HTTPS prevents accidental transmission of sensitive data over
    /// insecure channels and ensures that developers explicitly configure a
    /// secure endpoint in builds.
    /// </summary>
    /// <returns>True when <see cref="serviceUrl"/> is a valid HTTPS URL.</returns>
    private bool IsServiceUrlSecure()
    {
        // Reject missing URLs so the game cannot attempt to communicate with
        // an undefined service endpoint.
        if (string.IsNullOrWhiteSpace(serviceUrl))
        {
            Debug.LogError("Leaderboard serviceUrl must be a HTTPS URL but is empty.");
            return false;
        }

        // Only allow HTTPS to avoid insecure HTTP traffic.
        if (!serviceUrl.TrimStart().StartsWith("https://"))
        {
            Debug.LogError("Leaderboard serviceUrl must use HTTPS: " + serviceUrl);
            return false;
        }

        return true;
    }
}

