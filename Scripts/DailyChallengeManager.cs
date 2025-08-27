using UnityEngine;
using System;

//------------------------------------------------------------------------------
// Updated for buffered state persistence. The previous implementation wrote
// challenge progress to PlayerPrefs every frame, which caused unnecessary disk
// I/O. Progress is now marked dirty when it changes and flushed only after the
// value increases by a threshold or a fixed time interval has elapsed. The
// final state is also saved when the application pauses or quits to avoid data
// loss.
//------------------------------------------------------------------------------

/// <summary>
/// Generates and tracks a single daily challenge. The challenge persists in
/// PlayerPrefs with an expiry timestamp so players receive a new objective
/// each day they launch the game. Goals may involve traveling a distance,
/// collecting coins or using a specific power-up. Progress is monitored
/// through <see cref="GameManager"/> statistics.
/// </summary>
public class DailyChallengeManager : MonoBehaviour
{
    /// <summary>Types of objectives a challenge can generate.</summary>
    public enum ChallengeType { Distance, Coins, PowerUpUse }

    /// <summary>Supported power-up identifiers for power-up challenges.</summary>
    public enum PowerUpType
    {
        Magnet,
        SpeedBoost,
        Shield,
        GravityFlip,
        SlowMotion,
        CoinBonus,
        DoubleJump,
        Invincibility
    }

    [Serializable]
    private class ChallengeState
    {
        public ChallengeType type;    // kind of goal to complete
        public PowerUpType powerUp;   // power-up to use when type is PowerUpUse
        public int target;            // required amount (distance, coins or uses)
        public int progress;          // current progress toward the goal
        public long expires;          // UTC ticks when a new challenge should be generated
        public bool completed;        // true once the reward has been granted
    }

    private const string PrefKey = "DailyChallengeData";
    private const int RewardCoins = 25;                    // coins awarded on completion
    private const string AchComplete = "ACH_DAILY_COMPLETE";

    // Minimum change required before auto-saving to reduce write frequency.
    private static readonly int ProgressSaveThreshold = 5;
    // Seconds between automatic saves when progress is changing slowly.
    private static readonly float SaveInterval = 1f;

    public static DailyChallengeManager Instance { get; private set; }

    private ChallengeState state;

    // Tracks unsaved changes so persistence can be deferred until necessary.
    private bool isDirty;
    // Timestamp of the last flush to PlayerPrefs.
    private float lastSaveTime;
    // Progress value stored during the last save for delta comparisons.
    private int lastSavedProgress;

    /// <summary>
    /// Initializes the singleton instance and loads or creates the current
    /// challenge data.
    /// </summary>
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadOrGenerate();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Updates challenge progress from <see cref="GameManager"/> statistics.
    /// Progress changes are marked dirty and persisted in batches to avoid
    /// expensive per-frame disk writes.
    /// </summary>
    void Update()
    {
        if (state == null || state.completed)
            return;

        if (state.expires <= DateTime.UtcNow.Ticks)
        {
            GenerateChallenge();
            return;
        }

        switch (state.type)
        {
            case ChallengeType.Distance:
                if (GameManager.Instance != null)
                {
                    int newProgress = Mathf.FloorToInt(GameManager.Instance.GetDistance());
                    if (newProgress != state.progress)
                    {
                        state.progress = newProgress;
                        isDirty = true; // mark for deferred save
                    }
                }
                break;
            case ChallengeType.Coins:
                if (GameManager.Instance != null)
                {
                    int newProgress = GameManager.Instance.GetCoins();
                    if (newProgress != state.progress)
                    {
                        state.progress = newProgress;
                        isDirty = true; // mark for deferred save
                    }
                }
                break;
        }

        MaybeFlushState();

        if (state.progress >= state.target)
        {
            CompleteChallenge();
        }
    }

    /// <summary>
    /// Records usage of a power-up so power-up challenges can progress. The
    /// update is buffered and only persisted once thresholds or timers are met.
    /// </summary>
    public void RecordPowerUpUse(PowerUpType type)
    {
        if (state == null || state.completed)
            return;
        if (state.type == ChallengeType.PowerUpUse && state.powerUp == type)
        {
            state.progress++;
            isDirty = true; // track unsaved progress
            if (state.progress >= state.target)
            {
                CompleteChallenge();
            }
            else
            {
                MaybeFlushState();
            }
        }
    }

    /// <summary>
    /// Returns a human readable description of the current challenge.
    /// </summary>
    public string GetChallengeText()
    {
        if (state == null)
            return string.Empty;
        switch (state.type)
        {
            case ChallengeType.Distance:
                return $"Travel {state.target}m";
            case ChallengeType.Coins:
                return $"Collect {state.target} coins";
            case ChallengeType.PowerUpUse:
                return $"Use {state.powerUp} {state.target} times";
            default:
                return string.Empty;
        }
    }

    /// <summary>Current progress value for UI display.</summary>
    public int GetProgress() => state?.progress ?? 0;
    /// <summary>Goal value for UI display.</summary>
    public int GetTarget() => state?.target ?? 0;
    /// <summary>Whether the player has finished today's challenge.</summary>
    public bool IsCompleted() => state?.completed ?? false;

    // Loads the challenge from PlayerPrefs or creates a new one when missing or expired.
    private void LoadOrGenerate()
    {
        if (PlayerPrefs.HasKey(PrefKey))
        {
            string json = PlayerPrefs.GetString(PrefKey);
            state = JsonUtility.FromJson<ChallengeState>(json);
            if (state == null || state.expires <= DateTime.UtcNow.Ticks)
            {
                GenerateChallenge();
            }
            else
            {
                // Initialize tracking fields for existing saved progress.
                lastSavedProgress = state.progress;
                lastSaveTime = Time.time;
                isDirty = false;
            }
        }
        else
        {
            GenerateChallenge();
        }
    }

    // Creates a new random challenge and saves it to PlayerPrefs.
    private void GenerateChallenge()
    {
        state = new ChallengeState();
        state.type = (ChallengeType)UnityEngine.Random.Range(0, Enum.GetNames(typeof(ChallengeType)).Length);
        state.target = state.type switch
        {
            ChallengeType.Distance => UnityEngine.Random.Range(500, 2000),
            ChallengeType.Coins => UnityEngine.Random.Range(10, 100),
            ChallengeType.PowerUpUse => UnityEngine.Random.Range(1, 3),
            _ => 0
        };
        state.powerUp = (PowerUpType)UnityEngine.Random.Range(0, Enum.GetNames(typeof(PowerUpType)).Length);
        state.progress = 0;
        state.completed = false;
        state.expires = DateTime.UtcNow.AddDays(1).Ticks;
        SaveState();
    }

    // Saves progress only when enough change has accumulated or the interval elapsed.
    private void MaybeFlushState()
    {
        if (!isDirty || state == null)
            return; // nothing to persist

        // Flush when progress jumps significantly or after the timeout.
        if (Mathf.Abs(state.progress - lastSavedProgress) >= ProgressSaveThreshold ||
            Time.time - lastSaveTime >= SaveInterval)
        {
            SaveState();
        }
    }

    // Writes the current state to PlayerPrefs and clears the dirty flag so
    // future saves can be throttled.
    private void SaveState()
    {
        if (state == null)
            return;

        string json = JsonUtility.ToJson(state);
        PlayerPrefs.SetString(PrefKey, json);
        PlayerPrefs.Save();

        // Track save metadata so future writes can be throttled.
        lastSavedProgress = state.progress;
        lastSaveTime = Time.time;
        isDirty = false;
    }

    // Grants the reward and flags the challenge as complete.
    private void CompleteChallenge()
    {
        state.completed = true;
        SaveState();
        if (ShopManager.Instance != null)
        {
            ShopManager.Instance.AddCoins(RewardCoins);
        }
        if (SteamManager.Instance != null)
        {
            SteamManager.Instance.UnlockAchievement(AchComplete);
        }
    }

    // Persist the challenge whenever the application quits so progress is not
    // lost on shutdown. Only writes when there are unsaved changes.
    void OnApplicationQuit()
    {
        // Persist unsaved progress when shutting down.
        if (isDirty)
            SaveState();
    }

    // Mobile platforms may pause the app without quitting. Any pending progress
    // is flushed when entering the background.
    void OnApplicationPause(bool paused)
    {
        if (paused && isDirty)
        {
            // Entering background: ensure current progress is stored.
            SaveState();
        }
    }
}
