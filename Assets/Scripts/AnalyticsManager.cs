using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

/// <summary>
/// Enhanced in 2024 to gracefully handle network failures when posting
/// analytics data. Requests are now wrapped in try/catch blocks and can
/// optionally retry with exponential backoff so statistics are not lost
/// during temporary outages.
/// </summary>

/// <summary>
/// Collects basic run statistics and optionally sends them to a remote
/// analytics endpoint. Data is stored locally using PlayerPrefs and
/// transmitted after each run or when the application quits. When
/// <see cref="remoteEndpoint"/> is left blank, no data leaves the
/// player's machine.
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

    // Flag prevents multiple simultaneous send operations
    private bool isSending;

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
            if (runs.Count > 0)
            {
                StartCoroutine(SendData());
            }
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
        SaveLocal();
        StartCoroutine(SendData());
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
            }
        }
    }

    /// <summary>
    /// Posts the run list to the remote endpoint if one is specified.
    /// On success the local data is cleared.
    /// </summary>
    private IEnumerator SendData()
    {
        if (runs.Count == 0 || isSending)
        {
            yield break;
        }

        isSending = true;
        string json = JsonUtility.ToJson(new RunCollection { runs = runs.ToArray() });
        if (string.IsNullOrEmpty(remoteEndpoint))
        {
            Debug.Log("Analytics data: " + json);
            isSending = false;
            yield break;
        }

        int attempt = 0;
        while (true)
        {
            using (UnityWebRequest req = new UnityWebRequest(remoteEndpoint, "POST"))
            {
                byte[] body = System.Text.Encoding.UTF8.GetBytes(json);
                req.uploadHandler = new UploadHandlerRaw(body);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");

                try
                {
                    yield return req.SendWebRequest();
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning("Analytics request threw an exception: " + ex.Message);
                    // treat as failure below
                }

                if (req.result == UnityWebRequest.Result.Success)
                {
                    runs.Clear();
                    PlayerPrefs.DeleteKey("AnalyticsData");
                    isSending = false;
                    yield break;
                }
                else
                {
                    Debug.LogWarning($"Failed to send analytics attempt {attempt + 1}: {req.error}");
                }
            }

            if (attempt++ >= maxRetries)
            {
                Debug.LogWarning("Giving up on analytics send until next attempt");
                isSending = false;
                yield break;
            }

            float wait = retryBackoff * Mathf.Pow(2f, attempt - 1);
            yield return RetryDelay(wait);
        }
    }

    /// <summary>
    /// Immediately posts the run list and blocks until the request completes.
    /// Used when quitting so data isn't lost if the application closes quickly.
    /// </summary>

    private IEnumerator SendDataBlocking()
    {
        if (runs.Count == 0 || isSending)
        {
            yield break;
        }

        isSending = true;
        string json = JsonUtility.ToJson(new RunCollection { runs = runs.ToArray() });
        if (string.IsNullOrEmpty(remoteEndpoint))
        {
            Debug.Log("Analytics data: " + json);
            isSending = false;
            yield break;
        }

        int attempt = 0;
        while (true)
        {
            using (UnityWebRequest req = new UnityWebRequest(remoteEndpoint, "POST"))
            {
                byte[] body = System.Text.Encoding.UTF8.GetBytes(json);
                req.uploadHandler = new UploadHandlerRaw(body);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");

                UnityWebRequestAsyncOperation op = null;
                try
                {
                    op = req.SendWebRequest();
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning("Analytics request threw an exception: " + ex.Message);
                }

                Stopwatch sw = Stopwatch.StartNew();
                while (op != null && !op.isDone && sw.Elapsed.TotalSeconds < sendTimeout)
                {
                    Thread.Sleep(10);
                    yield return null;
                }

                if (op != null && !op.isDone)
                {
                    req.Abort();
                    Debug.LogWarning("Analytics send timed out");
                }

                if (op != null && req.result == UnityWebRequest.Result.Success)
                {
                    runs.Clear();
                    PlayerPrefs.DeleteKey("AnalyticsData");
                    isSending = false;
                    yield break;
                }
                else if (op != null)
                {
                    Debug.LogWarning($"Failed to send analytics attempt {attempt + 1}: {req.error}");
                }
            }

            if (attempt++ >= maxRetries)
            {
                Debug.LogWarning("Giving up on analytics send until next attempt");
                isSending = false;
                yield break;
            }

            float wait = retryBackoff * Mathf.Pow(2f, attempt - 1);
            Stopwatch delay = Stopwatch.StartNew();
            while (delay.Elapsed.TotalSeconds < wait)
            {
                Thread.Sleep(10);
                yield return null;
            }
        }
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
