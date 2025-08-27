using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

// LocalizationManager is used to fetch translated strings for fallback labels.
// This revision reads the local-player label and entry format from the
// localization tables so leaderboard text updates when the language changes.

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
    /// Enumerates specific error categories surfaced from HTTP requests. Using
    /// explicit codes instead of booleans allows UI elements to present more
    /// descriptive messages to the player and enables tests to verify that the
    /// correct failure mode was detected.
    /// </summary>
    public enum ErrorCode
    {
        None = 0,
        NetworkError,
        HttpError,
        CertificateError,
        Timeout,
        Unknown
    }

    /// <summary>
    /// Uploads <paramref name="score"/> associated with <see cref="playerName"/>.
    /// The request body is JSON of the form {"name":"Player","score":100}.
    /// A callback reports whether the upload ultimately succeeded so callers
    /// can react to network failures.
    /// </summary>
    /// <param name="score">Score value to submit.</param>
    /// <param name="onComplete">Optional callback receiving a success flag.</param>
    public IEnumerator UploadScore(int score, System.Action<bool, ErrorCode> onComplete = null)
    {
        // Ensure serviceUrl is configured and uses HTTPS before attempting
        // to communicate with the leaderboard. Failing to validate here would
        // allow insecure plaintext traffic or null requests.
        if (!IsServiceUrlSecure())
        {
            // Treat missing or insecure URLs as a network error so callers can
            // display a consistent failure message to the player.
            onComplete?.Invoke(false, ErrorCode.NetworkError);
            yield break;
        }

        // Show a visual indicator so players know a network operation is in progress.
        UIManager.Instance?.ShowNetworkSpinner();

        string url = serviceUrl.TrimEnd('/') + "/scores";
        string json = JsonUtility.ToJson(new ScoreEntry { name = playerName, score = score });

        // Retry the upload a few times with exponential backoff to handle
        // transient network failures such as dropped connections or timeouts.
        const int maxRetries = 3;
        int attempt = 0;
        bool success = false;
        ErrorCode lastError = ErrorCode.None; // Tracks final error for UI reporting
        while (attempt < maxRetries && !success)
        {
            using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
            {
                byte[] body = System.Text.Encoding.UTF8.GetBytes(json);
                req.uploadHandler = new UploadHandlerRaw(body);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.timeout = RequestTimeoutSeconds; // Seconds before the request automatically times out.

                // Send the request and capture both the success flag and any
                // associated error code so callers can surface a meaningful
                // message to the player.
                yield return SendWebRequest(req, (ok, _unused, code) => { success = ok; lastError = code; });
            }

            // Exponential backoff: 1s, 2s, 4s delays between attempts.
            if (!success && ++attempt < maxRetries)
            {
                float delay = Mathf.Pow(2, attempt);
                LoggingHelper.LogWarning($"Retrying score upload in {delay:F0}s (attempt {attempt + 1}/{maxRetries})"); // Use helper so retry info respects verbose gating.
                yield return new WaitForSeconds(delay);
            }
        }

        if (!success)
        {
            LoggingHelper.LogWarning("Failed to upload score to leaderboard"); // Central helper ensures message gating.
            // Surface the specific error to the UI so a localized message can
            // be displayed instead of silently failing.
            UIManager.Instance?.ShowLeaderboardError(lastError);
        }

        // Notify caller of the final outcome along with the error code. This
        // enables external UI to present retry prompts or other feedback.
        onComplete?.Invoke(success, lastError);

        // Hide the network activity spinner now that the request has finished.
        UIManager.Instance?.HideNetworkSpinner();
    }

    /// <summary>
    /// Retrieves the top scores from the service. If the request fails or
    /// returns invalid data, the local high score from
    /// <see cref="SaveGameManager"/> is provided instead. The callback also
    /// receives a success flag indicating whether remote communication
    /// succeeded so callers can present error messages.
    /// </summary>
    /// <param name="callback">Invoked with the retrieved scores and a success flag.</param>
    public virtual IEnumerator GetTopScores(System.Action<List<ScoreEntry>, bool, ErrorCode> callback)
    {
        List<ScoreEntry> result = null; // Final list of scores to return
        bool success = false;           // Tracks whether the remote request succeeded
        ErrorCode error = ErrorCode.None; // Detailed error category surfaced to UI

        // Validate serviceUrl to enforce secure communication. If invalid,
        // immediately return the local high score without issuing any web
        // requests.
        if (IsServiceUrlSecure())
        {
            // Network request will occur, so show the spinner to inform the user.
            UIManager.Instance?.ShowNetworkSpinner();

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
                    // Capture both success and any error code for the UI.
                    yield return SendWebRequest(req, (ok, data, code) => { success = ok; text = data; error = code; });
                }

                if (!success && ++attempt < maxRetries)
                {
                    float delay = Mathf.Pow(2, attempt);
                    LoggingHelper.LogWarning($"Retrying score download in {delay:F0}s (attempt {attempt + 1}/{maxRetries})"); // Consistent logging for retries.
                    yield return new WaitForSeconds(delay);
                }
            }

            if (success)
            {
                result = ParseScores(text);
            }

            // Hide the spinner once the request finishes regardless of outcome.
            UIManager.Instance?.HideNetworkSpinner();
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
            if (error == ErrorCode.None)
            {
                // If no explicit error was recorded, categorize the fallback as
                // a generic network issue so the UI can communicate that
                // scores were not retrieved from the server.
                error = ErrorCode.Unknown;
            }
        }

        // Callback receives both the scores to display and whether they came
        // from the remote service (true) or a local fallback (false).
        callback?.Invoke(result, success, error);
    }

    /// <summary>
    /// Issues the web request and invokes the callback with the outcome.
    /// Tests override this method to provide fake responses.
    /// </summary>
    protected virtual IEnumerator SendWebRequest(UnityWebRequest req, System.Action<bool, string, ErrorCode> callback)
    {
        string text = null;              // Response body when successful
        bool success = false;            // True when the request completes without errors
        ErrorCode code = ErrorCode.None; // Categorized error returned to the caller

        try
        {
            yield return req.SendWebRequest();

#if UNITY_2020_2_OR_NEWER
            // Modern Unity versions expose a consolidated result enum so we
            // inspect it first. Older versions fall back to the legacy
            // isNetworkError/isHttpError properties handled below.
            switch (req.result)
            {
                case UnityWebRequest.Result.Success:
                    text = req.downloadHandler.text;
                    success = true;
                    break;
                case UnityWebRequest.Result.ConnectionError:
                    // Distinguish timeouts and certificate problems where possible.
                    if (req.error != null && req.error.ToLower().Contains("timed out"))
                    {
                        code = ErrorCode.Timeout;
                    }
                    else if (req.error != null && req.error.ToLower().Contains("certificate"))
                    {
                        code = ErrorCode.CertificateError;
                    }
                    else
                    {
                        code = ErrorCode.NetworkError;
                    }
                    break;
                case UnityWebRequest.Result.ProtocolError:
                    code = ErrorCode.HttpError;
                    break;
                default:
                    code = ErrorCode.Unknown;
                    break;
            }
#else
            if (req.isNetworkError)
            {
                if (req.error != null && req.error.ToLower().Contains("timed out"))
                {
                    code = ErrorCode.Timeout;
                }
                else if (req.error != null && req.error.ToLower().Contains("certificate"))
                {
                    code = ErrorCode.CertificateError;
                }
                else
                {
                    code = ErrorCode.NetworkError;
                }
            }
            else if (req.isHttpError)
            {
                code = ErrorCode.HttpError;
            }
            else
            {
                text = req.downloadHandler.text;
                success = true;
            }
#endif

            // Provide additional log context for HTTP failures so developers can
            // differentiate client versus server-side issues during debugging.
            if (!success)
            {
                long status = req.responseCode;
                if (code == ErrorCode.HttpError)
                {
                    if (status >= 400 && status < 500)
                    {
                        LoggingHelper.LogWarning($"Client error {status} during leaderboard request: {req.error}"); // Client-side HTTP issue surfaced.
                    }
                    else if (status >= 500)
                    {
                        LoggingHelper.LogWarning($"Server error {status} during leaderboard request: {req.error}"); // Server responded with error status.
                    }
                    else
                    {
                        LoggingHelper.LogWarning("Leaderboard request failed: " + req.error); // Network failure logged via helper.
                    }
                }
                else
                {
                    LoggingHelper.LogWarning("Leaderboard request failed: " + req.error); // Fallback when response code unavailable.
                }
            }
        }
        catch (System.Exception ex)
        {
            code = ErrorCode.NetworkError;
            LoggingHelper.LogWarning("Leaderboard request failed: " + ex.Message); // Exception from request pipeline.
        }

        callback?.Invoke(success, text, code);
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
            LoggingHelper.LogWarning("Failed to parse leaderboard JSON: " + ex.Message); // Parsing error surfaced.
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
            LoggingHelper.LogError("Leaderboard serviceUrl must be a HTTPS URL but is empty."); // Use helper for critical misconfiguration.
            return false;
        }

        // Only allow HTTPS to avoid insecure HTTP traffic.
        if (!serviceUrl.TrimStart().StartsWith("https://"))
        {
            LoggingHelper.LogError("Leaderboard serviceUrl must use HTTPS: " + serviceUrl); // Force secure connections.
            return false;
        }

        return true;
    }
}

