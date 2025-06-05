using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;

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

    void Awake()
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
    /// Posts the run list to the remote endpoint if one is specified.
    /// On success the local data is cleared.
    /// </summary>
    private IEnumerator SendData()
    {
        if (runs.Count == 0)
        {
            yield break;
        }

        string json = JsonUtility.ToJson(new RunCollection { runs = runs.ToArray() });
        if (string.IsNullOrEmpty(remoteEndpoint))
        {
            Debug.Log("Analytics data: " + json);
            yield break;
        }

        using (UnityWebRequest req = new UnityWebRequest(remoteEndpoint, "POST"))
        {
            byte[] body = System.Text.Encoding.UTF8.GetBytes(json);
            req.uploadHandler = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success)
            {
                runs.Clear();
                PlayerPrefs.DeleteKey("AnalyticsData");
            }
            else
            {
                Debug.LogWarning("Failed to send analytics: " + req.error);
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
            StartCoroutine(SendData());
        }
    }
}
