using UnityEngine;
using System;

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

    public static DailyChallengeManager Instance { get; private set; }

    private ChallengeState state;

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
    /// Checks for completion each frame by comparing GameManager metrics
    /// against the active challenge.
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
                    state.progress = Mathf.FloorToInt(GameManager.Instance.GetDistance());
                    SaveState();
                }
                break;
            case ChallengeType.Coins:
                if (GameManager.Instance != null)
                {
                    state.progress = GameManager.Instance.GetCoins();
                    SaveState();
                }
                break;
        }

        if (state.progress >= state.target)
        {
            CompleteChallenge();
        }
    }

    /// <summary>
    /// Records usage of a power-up so power-up challenges can progress.
    /// </summary>
    public void RecordPowerUpUse(PowerUpType type)
    {
        if (state == null || state.completed)
            return;
        if (state.type == ChallengeType.PowerUpUse && state.powerUp == type)
        {
            state.progress++;
            if (state.progress >= state.target)
            {
                CompleteChallenge();
            }
            else
            {
                SaveState();
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

    // Writes the current state to PlayerPrefs for persistence across sessions.
    private void SaveState()
    {
        if (state == null) return;
        string json = JsonUtility.ToJson(state);
        PlayerPrefs.SetString(PrefKey, json);
        PlayerPrefs.Save();
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
    // lost on shutdown.
    void OnApplicationQuit()
    {
        SaveState();
    }

    // Mobile platforms may pause the app without quitting. Save progress when
    // entering the background.
    void OnApplicationPause(bool paused)
    {
        if (paused)
        {
            SaveState();
        }
    }
}
