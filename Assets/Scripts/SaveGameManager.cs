// SaveGameManager.cs
// -----------------------------------------------------------------------------
// Provides persistent storage of player progress using a JSON file located
// under Application.persistentDataPath. The design intentionally avoids
// PlayerPrefs for large or structured data so saves can be inspected and
// manually edited if required. On first run existing PlayerPrefs values are
// migrated to maintain backward compatibility. Saving writes to a temporary
// file first to reduce the chance of corruption if the application quits
// during a write operation.
//
// 2025 bug fix: loading no longer throws when the "upgrades" array is missing
// from a legacy save file. The loader now checks for null before iterating so
// older saves continue to load correctly.
// 2026 update: SaveDataToFile now catches IO exceptions and cleans up temporary
// files to prevent crashes when the disk is unwritable.
// 2027 update: corrupt or unreadable saves trigger a reset to default values,
// ensuring players are not stuck with invalid data.
// 2028 update: SaveDataToFile performs asynchronous writes via a background
// task and request queue so slow disks do not stall gameplay.
// 2029 update: save requests now capture the target path to avoid cross-slot
// writes and ChangeSlot waits for pending saves before switching profiles.
// -----------------------------------------------------------------------------

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;
using UnityEngine;

/// <summary>
/// Singleton responsible for loading and saving player progress such as coins,
/// purchased upgrades and the high score. Data is serialized to JSON so it can
/// be easily inspected and modified between versions.
/// </summary>
public class SaveGameManager : MonoBehaviour
{
    public static SaveGameManager Instance { get; private set; }

    /// <summary>Serializable key/value pair representing an upgrade level.</summary>
    [Serializable]
    private struct UpgradeEntry
    {
        public string type;    // name of the UpgradeType enum value
        public int level;      // purchased level count
    }

    /// <summary>Container for all persistent data saved to disk.</summary>
    [Serializable]
    private class SaveData
    {
        // Bump <see cref="CurrentVersion"/> when fields are added so older
        // saves can be upgraded in <see cref="LoadData"/>.
        public int version;
        public int coins;
        public int highScore;
        public float musicVolume = 1f;   // range 0-1
        public float effectsVolume = 1f; // range 0-1
        public string language = "en";   // language code
        public bool tutorialCompleted;   // has the intro tutorial been shown
        public bool jumpTipShown;        // has the jump tip been displayed
        public bool slideTipShown;       // has the slide tip been displayed
        public bool hardcoreMode;        // optional hardcore mode toggle
        public List<UpgradeEntry> upgrades = new List<UpgradeEntry>();
    }

    // Version value written to disk alongside <see cref="SaveData"/>.
    private const int CurrentVersion = 4;

    private const string CoinsKey = "ShopCoins";       // legacy PlayerPrefs key
    private const string UpgradePrefix = "UpgradeLevel_"; // legacy PlayerPrefs prefix
    private const string HighScoreKey = "HighScore";   // legacy PlayerPrefs key

    private SaveData data = new SaveData();
    private readonly Dictionary<UpgradeType, int> upgradeLevels = new Dictionary<UpgradeType, int>();
    private string savePath;

    // Data packet used for queued save operations. Each request stores both the
    // serialized JSON payload and the path it should be written to so that
    // pending saves are not redirected if <see cref="savePath"/> changes
    // mid-flight.
    private readonly struct SaveRequest
    {
        public readonly string Json; // Serialized SaveData content
        public readonly string Path; // Destination file path for this request

        public SaveRequest(string json, string path)
        {
            Json = json;
            Path = path;
        }
    }

    // Fields used for asynchronous, thread-safe file saving. Requests are
    // queued so only one write happens at a time and the main thread remains
    // responsive even on slow disks.
    private readonly object queueLock = new object();
    private readonly Queue<SaveRequest> saveQueue = new Queue<SaveRequest>();
    private Task processingTask = Task.CompletedTask;
    private readonly ConcurrentQueue<Action> completionActions = new ConcurrentQueue<Action>();

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            // Use the SaveSlotManager so each profile writes to its own file
            savePath = SaveSlotManager.GetPath("savegame.json");
            LoadData();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>Current coin balance.</summary>
    public int Coins
    {
        get => data.coins;
        set
        {
            data.coins = Mathf.Max(0, value);
            SaveDataToFile();
        }
    }

    /// <summary>Stored best distance traveled.</summary>
    public int HighScore
    {
        get => data.highScore;
        set
        {
            data.highScore = Mathf.Max(0, value);
            SaveDataToFile();
        }
    }

    /// <summary>Stored music volume in the range 0–1.</summary>
    public float MusicVolume
    {
        get => data.musicVolume;
        set
        {
            data.musicVolume = Mathf.Clamp01(value);
            SaveDataToFile();
        }
    }

    /// <summary>Stored effects volume in the range 0–1.</summary>
    public float EffectsVolume
    {
        get => data.effectsVolume;
        set
        {
            data.effectsVolume = Mathf.Clamp01(value);
            SaveDataToFile();
        }
    }

    /// <summary>Currently selected language code.</summary>
    public string Language
    {
        get => data.language;
        set
        {
            if (!string.IsNullOrEmpty(value))
            {
                data.language = value;
                SaveDataToFile();
                LocalizationManager.SetLanguage(value);
            }
        }
    }

    /// <summary>True once the introductory tutorial has been completed.</summary>
    public bool TutorialCompleted
    {
        get => data.tutorialCompleted;
        set
        {
            data.tutorialCompleted = value;
            SaveDataToFile();
        }
    }

    /// <summary>Whether the jump hint has already been shown.</summary>
    public bool JumpTipShown
    {
        get => data.jumpTipShown;
        set
        {
            data.jumpTipShown = value;
            SaveDataToFile();
        }
    }

    /// <summary>Whether the slide hint has already been shown.</summary>
    public bool SlideTipShown
    {
        get => data.slideTipShown;
        set
        {
            data.slideTipShown = value;
            SaveDataToFile();
        }
    }

    /// <summary>Whether hardcore mode is enabled.</summary>
    public bool HardcoreMode
    {
        get => data.hardcoreMode;
        set
        {
            data.hardcoreMode = value;
            SaveDataToFile();
        }
    }

    /// <summary>Returns the purchased level count for an upgrade.</summary>
    public int GetUpgradeLevel(UpgradeType type)
    {
        upgradeLevels.TryGetValue(type, out int level);
        return level;
    }

    /// <summary>Sets the level count for an upgrade and persists it.</summary>
    public void SetUpgradeLevel(UpgradeType type, int level)
    {
        upgradeLevels[type] = Mathf.Max(0, level);
        SaveDataToFile();
    }

    /// <summary>
    /// Updates multiple upgrade levels in a single operation. The provided
    /// dictionary may contain any subset of <see cref="UpgradeType"/> values.
    /// A single file write occurs after all levels are updated.
    /// </summary>
    /// <param name="levels">Mapping of upgrade types to their desired levels.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="levels"/> is null.</exception>
    public void UpdateUpgradeLevels(Dictionary<UpgradeType, int> levels)
    {
        if (levels == null)
            throw new ArgumentNullException(nameof(levels));

        foreach (var kvp in levels)
        {
            upgradeLevels[kvp.Key] = Mathf.Max(0, kvp.Value);
        }

        SaveDataToFile();
    }

    /// <summary>
    /// Reads existing save data from disk or migrates old PlayerPrefs data when
    /// the save file does not yet exist.
    /// </summary>
    private void LoadData()
    {
        if (File.Exists(savePath))
        {
            try
            {
                string json = File.ReadAllText(savePath);
                SaveData loaded = JsonUtility.FromJson<SaveData>(json);
                if (loaded != null)
                {
                    // Handle missing fields by checking the save version.
                    if (loaded.version < 1)
                    {
                        loaded.musicVolume = 1f;
                        loaded.effectsVolume = 1f;
                    }
                    if (loaded.version < 2)
                    {
                        loaded.language = "en";
                    }
                    if (loaded.version < 3)
                    {
                        loaded.tutorialCompleted = false;
                        loaded.jumpTipShown = false;
                        loaded.slideTipShown = false;
                    }
                    if (loaded.version < 4)
                    {
                        loaded.hardcoreMode = false;
                    }

                    data.coins = loaded.coins;
                    data.highScore = loaded.highScore;
                    data.musicVolume = Mathf.Clamp01(loaded.musicVolume);
                    data.effectsVolume = Mathf.Clamp01(loaded.effectsVolume);
                    data.language = loaded.language ?? "en";
                    data.tutorialCompleted = loaded.tutorialCompleted;
                    data.jumpTipShown = loaded.jumpTipShown;
                    data.slideTipShown = loaded.slideTipShown;
                    data.hardcoreMode = loaded.hardcoreMode;
                    LocalizationManager.SetLanguage(data.language);
                    
                    upgradeLevels.Clear();
                    if (loaded.upgrades != null)
                    {
                        foreach (var entry in loaded.upgrades)
                        {
                            if (Enum.TryParse(entry.type, out UpgradeType type))
                            {
                                upgradeLevels[type] = entry.level;
                            }
                        }
                    }
                    return;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Failed to parse save file, starting fresh: " + ex.Message);
            }
            // Parsing failed or produced null; fall back to a clean slate.
            ResetToDefaultSave();
        }
        else
        {
            // No save file found; migrate values stored in PlayerPrefs if
            // present. This maintains compatibility with versions prior to the
            // JSON-based system.
            data.coins = PlayerPrefs.GetInt(CoinsKey, 0);
            data.highScore = PlayerPrefs.GetInt(HighScoreKey, 0);
            data.musicVolume = 1f;
            data.effectsVolume = 1f;
            data.language = "en";
            data.tutorialCompleted = PlayerPrefs.GetInt("TutorialSeen", 0) == 1;
            data.jumpTipShown = false;
            data.slideTipShown = false;
            data.hardcoreMode = false;
            foreach (UpgradeType type in Enum.GetValues(typeof(UpgradeType)))
            {
                int level = PlayerPrefs.GetInt(UpgradePrefix + type, 0);
                if (level > 0)
                {
                    upgradeLevels[type] = level;
                }
            }
            // Persist the newly created data so future loads skip migration.
            SaveDataToFile();
            LocalizationManager.SetLanguage(data.language);
        }
    }

    /// <summary>
    /// Resets all persisted values to their factory defaults, reapplies the
    /// default language and immediately writes the clean state to disk. Used when
    /// a save file exists but cannot be parsed.
    /// </summary>
    private void ResetToDefaultSave()
    {
        // Replace existing data with a fresh instance containing default field
        // values defined in <see cref="SaveData"/>.
        data = new SaveData();
        // Any previously cached upgrade levels are cleared so no stale values
        // remain after recovery from a corrupt file.
        upgradeLevels.Clear();
        // Apply the default language before persisting so both runtime state and
        // the save file reflect the reset value.
        LocalizationManager.SetLanguage(data.language);
        // Persist defaults so future loads start from a valid JSON file.
        SaveDataToFile();
    }

    /// <summary>
    /// Serializes the current <see cref="SaveData"/> and queues it for an
    /// asynchronous disk write. Using a queue prevents multiple concurrent
    /// writes and keeps the main Unity thread responsive. This method returns
    /// immediately; completion or errors are marshalled back to the main thread
    /// via <see cref="completionActions"/>.
    /// </summary>
    protected virtual void SaveDataToFile()
    {
        data.version = CurrentVersion;
        data.musicVolume = Mathf.Clamp01(data.musicVolume);
        data.effectsVolume = Mathf.Clamp01(data.effectsVolume);
        if (string.IsNullOrEmpty(data.language))
        {
            data.language = "en";
        }
        data.upgrades.Clear();
        foreach (var kvp in upgradeLevels)
        {
            data.upgrades.Add(new UpgradeEntry { type = kvp.Key.ToString(), level = kvp.Value });
        }

        string json = JsonUtility.ToJson(data);

        // Queue the JSON and its target path for asynchronous saving. Storing
        // the path with the payload ensures pending writes are not redirected if
        // <see cref="savePath"/> changes before the background task runs. If no
        // background task is active, start one using Task.Run so file IO occurs
        // off the main thread and gameplay remains responsive on slow disks.
        lock (queueLock)
        {
            saveQueue.Enqueue(new SaveRequest(json, savePath));
            if (processingTask == null || processingTask.IsCompleted)
            {
                processingTask = Task.Run(ProcessQueueAsync);
            }
        }
    }

    /// <summary>
    /// Background loop that processes queued save requests one at a time. Each
    /// JSON payload is written to disk using async <see cref="FileStream"/>
    /// APIs. Any errors are enqueued so they can be reported on the main thread
    /// during <see cref="Update"/>.
    /// </summary>
    private async Task ProcessQueueAsync()
    {
        while (true)
        {
            SaveRequest request;
            lock (queueLock)
            {
                if (saveQueue.Count == 0)
                    return; // no more work
                request = saveQueue.Dequeue();
            }

            // Use the path captured with the request so writes target the
            // correct slot even if ChangeSlot has modified savePath since the
            // request was queued.
            string tempPath = request.Path + ".tmp";
            try
            {
                await WriteFileAsync(tempPath, request.Path, request.Json);
            }
            catch (IOException ex)
            {
                // Errors are marshalled back to the main thread so callers are
                // informed without touching Unity APIs off-thread.
                completionActions.Enqueue(() => Debug.LogWarning($"Failed to write save file: {ex.Message}"));
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }
    }

    /// <summary>
    /// Performs the actual disk IO using asynchronous <see cref="FileStream"/>
    /// operations. Separated into its own method so tests can override the
    /// behaviour and simulate slow disks or failures.
    /// </summary>
    /// <param name="tempPath">Path to a temporary file used for atomic writes.</param>
    /// <param name="finalPath">Destination path of the save file.</param>
    /// <param name="json">Serialized save data.</param>
    protected virtual async Task WriteFileAsync(string tempPath, string finalPath, string json)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        using (var tempStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
        {
            await tempStream.WriteAsync(bytes, 0, bytes.Length);
        }

        using (var sourceStream = new FileStream(tempPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true))
        using (var destStream = new FileStream(finalPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
        {
            await sourceStream.CopyToAsync(destStream);
        }
    }

    /// <summary>
    /// Executes any completion callbacks that were queued by background save
    /// operations. This method runs on the Unity main thread and therefore may
    /// safely interact with Unity APIs such as <see cref="Debug.Log"/>.
    /// </summary>
    void Update()
    {
        while (completionActions.TryDequeue(out var action))
        {
            action?.Invoke();
        }
    }

    /// <summary>
    /// Ensures all queued save operations finish before the manager is
    /// destroyed, preventing data loss during shutdown or scene unload.
    /// </summary>
    void OnDestroy()
    {
        // Block until any outstanding background save completes so no data is
        // lost on shutdown. Afterwards flush any queued completion actions so
        // warnings are still surfaced even if Update was never called again.
        processingTask?.Wait();
        while (completionActions.TryDequeue(out var action))
        {
            action?.Invoke();
        }
    }

    /// <summary>
    /// Switches to a different save slot at runtime. The current data is
    /// written to disk before swapping directories and reloading from the new
    /// slot. Throws when an invalid index is provided.
    /// </summary>
    /// <param name="slot">Index of the slot to activate.</param>
    public void ChangeSlot(int slot)
    {
        if (slot < 0 || slot >= SaveSlotManager.MaxSlots)
            throw new ArgumentOutOfRangeException(nameof(slot), "Invalid save slot index");

        if (slot == SaveSlotManager.CurrentSlot)
            return; // already using this slot

        // Persist current state before redirecting file paths.
        SaveDataToFile();

        // Ensure all queued writes complete so the previous slot is fully saved
        // before switching. Waiting outside the lock avoids deadlocks while
        // still guaranteeing the queue is drained.
        Task toWait;
        lock (queueLock)
        {
            toWait = processingTask;
        }
        toWait?.Wait();

        SaveSlotManager.SetSlot(slot);
        savePath = SaveSlotManager.GetPath("savegame.json");
        LoadData();
    }
}
