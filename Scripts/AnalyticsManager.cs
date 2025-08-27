using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

/// <summary>
/// Collects basic run statistics and optionally sends them to a remote
/// analytics endpoint. Data is stored locally using PlayerPrefs and
/// transmitted after each run or when the application quits. To prevent
/// unbounded growth, the manager retains only a configurable number of the
/// most recent runs (default 100), discarding the oldest entries when the cap
/// is exceeded. When <see cref="remoteEndpoint"/> is left blank, no data leaves
/// the player's machine. Upload operations run in a background coroutine and
/// fire <see cref="UploadProgress"/> and <see cref="UploadFinished"/> events so
/// UI can reflect status.
/// </summary>
public class AnalyticsManager : MonoBehaviour
{
    public static AnalyticsManager Instance { get; private set; }

    [Tooltip("Optional URL to POST aggregated run data as JSON. Leave blank to keep data local.")]
    public string remoteEndpoint;

    [Tooltip("Maximum seconds to wait when sending data on quit.")]
    public float sendTimeout = 3f;

    [Tooltip("Number of retry attempts when a send fails (0 disables retries).")]
    public int maxRetries = 0;

    [Tooltip("Base backoff in seconds for retries; each attempt doubles the wait.")]
    public float retryBackoff = 2f;

    /// <summary>
    /// Maximum number of recent runs to retain. Older entries are discarded when
    /// this cap is exceeded to keep memory and PlayerPrefs usage bounded.
    /// </summary>
    [Tooltip("Maximum number of recent runs to retain for analytics.")]
    public int maxStoredRuns = 100;

    // Flag prevents multiple simultaneous send operations
    private bool isSending;

    /// <summary>
    /// Interface abstracting UnityWebRequest so retry logic can be unit tested
    /// with mocked implementations.
    /// </summary>
    public interface IWebRequest
    {
        float UploadProgress { get; }
        bool IsDone { get; }
        UnityWebRequest.Result Result { get; }
        string Error { get; }
        IEnumerator Send();
    }

    /// <summary>
    /// Raised repeatedly while a web request is uploading. Provides the
    /// current progress value from 0..1 so UI can display a spinner or bar.
    /// </summary>
    public event System.Action<float> UploadProgress;

    /// <summary>
    /// Event fired when an upload finishes. The boolean argument is true on
    /// success. UI elements can hide progress indicators at this point.
    /// </summary>
    public event System.Action<bool> UploadFinished;

    private Coroutine uploadRoutine;

    /// <summary>
    /// Creates a concrete web request for posting analytics. Subclasses in tests
    /// return mocked objects so results can be simulated without network access.
    /// </summary>
    protected virtual IWebRequest CreateWebRequest(string url, byte[] body)
    {
        return new UnityWebRequestWrapper(url, body);
    }

    /// <summary>
    /// Adapter that exposes UnityWebRequest through the IWebRequest interface.
    /// </summary>
    private class UnityWebRequestWrapper : IWebRequest
    {
        private readonly UnityWebRequest request;

        public UnityWebRequestWrapper(string url, byte[] body)
        {
            request = new UnityWebRequest(url, "POST");
            request.uploadHandler = new UploadHandlerRaw(body);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
        }

        public float UploadProgress => request.uploadProgress;
        public bool IsDone => request.isDone;
        public UnityWebRequest.Result Result => request.result;
        public string Error => request.error;

        public IEnumerator Send()
        {
            yield return request.SendWebRequest();
        }
    }

    /// <summary>
    /// Creates a yield instruction used to wait between retry attempts. The
    /// default implementation returns a simple WaitForSeconds but tests can
    /// override this to avoid real delays.
    /// </summary>
    protected virtual YieldInstruction RetryDelay(float seconds)
    {
        return new WaitForSeconds(seconds);
    }

    private List<RunData> runs = new List<RunData>();

    /// <summary>
    /// Ensures <see cref="runs"/> does not exceed <see cref="maxStoredRuns"/>.
    /// Oldest entries are removed first. Throws if the configured cap is less
    /// than one to avoid silent misconfiguration.
    /// </summary>
    private void EnforceRunLimit()
    {
        if (maxStoredRuns < 1)
        {
            throw new System.ArgumentOutOfRangeException(
                nameof(maxStoredRuns),
                "Maximum stored runs must be at least one.");
        }

        while (runs.Count > maxStoredRuns)
        {
            runs.RemoveAt(0);
        }
    }

    /// <summary>
    /// Calculates the average distance of the most recent runs.
    /// Returns zero when no history is available or <paramref name="count"/>
    /// is less than one.
    /// </summary>
    /// <param name="count">Number of latest runs to average.</param>
    public float GetAverageDistance(int count)
    {
        if (count <= 0 || runs.Count == 0)
            return 0f;

        int take = Mathf.Min(count, runs.Count);
        float total = 0f;
        for (int i = runs.Count - take; i < runs.Count; i++)
        {
            total += runs[i].distance;
        }
        return total / take;
    }

    [System.Serializable]
    private struct RunData
    {
        public float distance;
        public int coins;
        public bool death;

        public RunData(float distance, int coins, bool death)
        {
            this.distance = distance;
            this.coins = coins;
            this.death = death;
        }
    }

    /// <summary>
    /// Initializes the singleton instance and loads any cached run data.
    /// Immediately attempts to send data if it exists.
    /// </summary>
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadLocal();
            BeginUploadsIfNeeded();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Records the results of a completed run and immediately attempts to
    /// send the updated aggregate data.
    /// </summary>
    public void LogRun(float distance, int coins, bool death)
    {
        runs.Add(new RunData(distance, coins, death));
        EnforceRunLimit(); // keep history within configured cap
        SaveLocal();
        BeginUploadsIfNeeded();
    }

    /// <summary>
    /// Serializes the run list to PlayerPrefs for local persistence.
    /// </summary>
    private void SaveLocal()
    {
        string json = JsonUtility.ToJson(new RunCollection { runs = runs.ToArray() });
        PlayerPrefs.SetString("AnalyticsData", json);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Restores any locally persisted run data from a previous session.
    /// </summary>
    private void LoadLocal()
    {
        if (PlayerPrefs.HasKey("AnalyticsData"))
        {
            string json = PlayerPrefs.GetString("AnalyticsData");
            RunCollection col = JsonUtility.FromJson<RunCollection>(json);
            if (col != null && col.runs != null)
            {
                runs = new List<RunData>(col.runs);
                EnforceRunLimit(); // trim any excess from previous sessions
            }
        }
    }

    /// <summary>
    /// Starts the persistent upload coroutine if there is data waiting and no
    /// current upload is active.
    /// </summary>
    private void BeginUploadsIfNeeded()
    {
        if (runs.Count > 0 && uploadRoutine == null)
        {
            uploadRoutine = StartCoroutine(UploadLoop());
        }
    }

    /// <summary>
    /// Coroutine that continuously attempts to upload analytics in the
    /// background. Data is kept locally until a request succeeds or retries are
    /// exhausted. Progress events fire so UI can show upload status.
    /// </summary>
    private IEnumerator UploadLoop()
    {
        if (runs.Count == 0 || isSending)
            yield break;

        isSending = true;
        int attempt = 0;

        // Display spinner so players know analytics are being transmitted.
        UIManager.Instance?.ShowNetworkSpinner();

        while (runs.Count > 0)
        {
            // Skip sending when offline and wait before retrying.
            if (Application.internetReachability == NetworkReachability.NotReachable)
            {
                float offlineWait = retryBackoff * Mathf.Pow(2f, attempt);
                yield return RetryDelay(offlineWait);
                attempt++;
                continue;
            }

            string json = JsonUtility.ToJson(new RunCollection { runs = runs.ToArray() });
            if (string.IsNullOrEmpty(remoteEndpoint))
            {
                // Output collected analytics data during development for
                // troubleshooting but omit in production builds.
                LoggingHelper.Log("Analytics data: " + json);
                runs.Clear();
                PlayerPrefs.DeleteKey("AnalyticsData");
                break;
            }

            IWebRequest req = CreateWebRequest(remoteEndpoint, System.Text.Encoding.UTF8.GetBytes(json));
            UploadProgress?.Invoke(0f);

            IEnumerator send = req.Send();
            while (send.MoveNext())
            {
                UploadProgress?.Invoke(req.UploadProgress);
                yield return send.Current;
            }
            UploadProgress?.Invoke(1f);

            if (req.Result == UnityWebRequest.Result.Success)
            {
                runs.Clear();
                PlayerPrefs.DeleteKey("AnalyticsData");
                attempt = 0; // reset on success
            }
            else
            {
                // Warnings surface transient network issues without spamming
                // release builds.
                LoggingHelper.LogWarning($"Failed to send analytics attempt {attempt + 1}: {req.Error}");
                if (attempt++ >= maxRetries)
                {
                    LoggingHelper.LogWarning("Giving up on analytics send until next attempt");
                    break;
                }

                float wait = retryBackoff * Mathf.Pow(2f, attempt - 1);
                yield return RetryDelay(wait);
            }
        }

        // Hide spinner once all upload attempts conclude.
        UIManager.Instance?.HideNetworkSpinner();

        isSending = false;
        uploadRoutine = null;
        UploadFinished?.Invoke(runs.Count == 0);
    }

    /// <summary>
    /// Immediately posts the run list and blocks until the request completes.
    /// Used when quitting so data isn't lost if the application closes quickly.
    /// </summary>

    private IEnumerator SendDataBlocking()
    {
        if (runs.Count == 0 || isSending)
            yield break;

        isSending = true;
        int attempt = 0;

        // Display spinner to communicate the blocking network activity.
        UIManager.Instance?.ShowNetworkSpinner();

        while (runs.Count > 0)
        {
            if (Application.internetReachability == NetworkReachability.NotReachable)
            {
                float wait = retryBackoff * Mathf.Pow(2f, attempt++);
                Stopwatch delay = Stopwatch.StartNew();
                while (delay.Elapsed.TotalSeconds < wait)
                {
                    Thread.Sleep(10);
                    yield return null;
                }
                continue;
            }

            string json = JsonUtility.ToJson(new RunCollection { runs = runs.ToArray() });
            if (string.IsNullOrEmpty(remoteEndpoint))
            {
                LoggingHelper.Log("Analytics data: " + json);
                runs.Clear();
                PlayerPrefs.DeleteKey("AnalyticsData");
                break;
            }

            IWebRequest req = CreateWebRequest(remoteEndpoint, System.Text.Encoding.UTF8.GetBytes(json));
            IEnumerator send = req.Send();
            Stopwatch sw = Stopwatch.StartNew();
            while (send.MoveNext() && sw.Elapsed.TotalSeconds < sendTimeout)
            {
                Thread.Sleep(10);
                yield return null;
            }

            if (!req.IsDone)
            {
                LoggingHelper.LogWarning("Analytics send timed out");
            }

            if (req.Result == UnityWebRequest.Result.Success)
            {
                runs.Clear();
                PlayerPrefs.DeleteKey("AnalyticsData");
                attempt = 0;
            }
            else
            {
                LoggingHelper.LogWarning($"Failed to send analytics attempt {attempt + 1}: {req.Error}");
                if (attempt++ >= maxRetries)
                    break;
            }
        }

        // Ensure the spinner is hidden once the blocking send finishes.
        UIManager.Instance?.HideNetworkSpinner();

        isSending = false;
        UploadFinished?.Invoke(runs.Count == 0);
    }

    [System.Serializable]
    private class RunCollection
    {
        public RunData[] runs;
    }

    /// <summary>
    /// When the application quits, attempt to send any remaining data.
    /// </summary>
    void OnApplicationQuit()
    {
        if (runs.Count > 0)
        {
            // Persist to disk first in case the request fails
            SaveLocal();
            var routine = SendDataBlocking();
            while (routine.MoveNext()) { }
        }
    }

    /// <summary>
    /// Clears the global instance reference when destroyed.
    /// </summary>
    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
