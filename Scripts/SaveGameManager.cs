// SaveGameManager.cs
// -----------------------------------------------------------------------------
// For an overview of how this manager fits into the wider system, see docs/ArchitectureOverview.md.
// Provides persistent storage of player progress using a JSON file located
// under Application.persistentDataPath. The design intentionally avoids
// PlayerPrefs for large or structured data so saves can be inspected and
// manually edited if required. Save payloads are wrapped with a SHA-256
// checksum and can optionally be AES encrypted before writing to disk. On
// first run existing PlayerPrefs values are migrated to maintain backward
// compatibility. Saving writes to a temporary file first to reduce the chance
// of corruption if the application quits during a write operation.
// -----------------------------------------------------------------------------

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Collections;
using System.Security.Cryptography; // AES encryption and SHA-256 checksums
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
        // saves can be upgraded in <see cref="LoadDataAsync"/>.
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

    /// <summary>
    /// Wrapper stored on disk which prefixes the serialized save data with a
    /// SHA-256 checksum and flag indicating whether the payload is AES
    /// encrypted. When <see cref="encrypted"/> is false the <see cref="data"/>
    /// field contains the raw <see cref="SaveData"/> object. When true, the
    /// <see cref="payload"/> field holds the base64 encoded cipher text.
    /// </summary>
    [Serializable]
    private class SaveFile
    {
        public string checksum;   // Hex-encoded SHA-256 digest of payload
        public bool encrypted;    // True when payload is AES encrypted
        public SaveData data;     // Plain save data when not encrypted
        public string payload;    // Base64 encoded cipher text when encrypted
    }

    // Version value written to disk alongside <see cref="SaveData"/>.
    private const int CurrentVersion = 4;

    // Toggle controlling whether save payloads are AES encrypted before being
    // written to disk. Disabled by default so files remain human-readable.
    private const bool EncryptSaves = false;

    // Names of the environment variables containing the base64 encoded AES key
    // and IV. These secrets are loaded at runtime so they are not stored in the
    // repository or shipped in plaintext builds.
    private const string KeyEnvVar = "CR_AES_KEY"; // expects 32-byte key
    private const string IvEnvVar = "CR_AES_IV";   // expects 16-byte IV

    // Cached AES key and IV loaded from the secure source. When either value is
    // missing or malformed, encryption is considered unavailable and save data
    // falls back to plaintext even if EncryptSaves is true. This avoids using
    // hard-coded secrets while keeping behaviour predictable for developers who
    // have not provisioned keys.
    private static byte[] encryptionKey;
    private static byte[] encryptionIV;
    private static bool encryptionConfigured;

    /// <summary>
    /// Static constructor eagerly loads AES secrets so calls to the encryption
    /// helpers know whether encryption is possible for this session.
    /// </summary>
    static SaveGameManager()
    {
        LoadEncryptionSecrets();
    }

    private const string CoinsKey = "ShopCoins";       // legacy PlayerPrefs key
    private const string UpgradePrefix = "UpgradeLevel_"; // legacy PlayerPrefs prefix
    private const string HighScoreKey = "HighScore";   // legacy PlayerPrefs key

    private SaveData data = new SaveData();
    private readonly Dictionary<UpgradeType, int> upgradeLevels = new Dictionary<UpgradeType, int>();
    private string savePath;

    // Task representing the asynchronous load of persisted data. This allows
    // callers and tests to await initialization without blocking the main
    // Unity thread during startup.
    private Task loadTask = Task.CompletedTask;

    /// <summary>
    /// Exposes the task for the most recent load operation so external code
    /// can await completion and be sure the manager's data has been populated
    /// before it is accessed.
    /// </summary>
    public Task Initialization => loadTask;

    // Data packet used for queued save operations. Each request stores both the
    // serialized JSON payload and the path it should be written to so that
    // pending saves are not redirected if <see cref="savePath"/> changes
    // mid-flight. An attempt counter tracks how many times a request has been
    // processed so transient failures can be retried without looping
    // indefinitely on permanent errors.
    private readonly struct SaveRequest
    {
        public readonly string Json;    // Serialized SaveData content
        public readonly string Path;    // Destination file path for this request
        public readonly int Attempts;   // Number of write attempts so far

        public SaveRequest(string json, string path, int attempts = 0)
        {
            Json = json;
            Path = path;
            Attempts = attempts;
        }
    }

    /// <summary>
    /// Generates a lowercase hexadecimal SHA-256 checksum for the provided
    /// byte array. Used to detect tampering or corruption of persisted save
    /// payloads.
    /// </summary>
    private static string ComputeChecksum(byte[] bytes)
    {
        using (var sha = SHA256.Create())
        {
            byte[] hash = sha.ComputeHash(bytes);
            return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
        }
    }

    /// <summary>
    /// Attempts to populate <see cref="encryptionKey"/> and <see cref="encryptionIV"/>
    /// from the configured environment variables. When the values are absent or
    /// invalid, a warning is logged and <see cref="encryptionConfigured"/> remains
    /// false so callers can gracefully fall back to plaintext saves.
    /// </summary>
    private static void LoadEncryptionSecrets()
    {
        encryptionConfigured = false;
        try
        {
            string keyB64 = Environment.GetEnvironmentVariable(KeyEnvVar);
            string ivB64 = Environment.GetEnvironmentVariable(IvEnvVar);
            if (!string.IsNullOrEmpty(keyB64) && !string.IsNullOrEmpty(ivB64))
            {
                byte[] keyBytes = Convert.FromBase64String(keyB64);
                byte[] ivBytes = Convert.FromBase64String(ivB64);
                if (keyBytes.Length == 32 && ivBytes.Length == 16)
                {
                    encryptionKey = keyBytes;
                    encryptionIV = ivBytes;
                    encryptionConfigured = true;
                    return;
                }
            }
            LoggingHelper.LogWarning("AES key/IV not found or invalid; save encryption disabled."); // Ensures warning obeys global verbosity.
        }
        catch (Exception ex)
        {
            LoggingHelper.LogWarning("Failed to load AES key/IV; save encryption disabled. " + ex.Message); // Use helper for consistent warning delivery.
        }
    }

    /// <summary>
    /// Encrypts the supplied plain text bytes using AES with the runtime-loaded
    /// key and IV. Caller must ensure <see cref="encryptionConfigured"/> is true
    /// before invoking this helper.
    /// </summary>
    private static byte[] EncryptBytes(byte[] plain)
    {
        using (var aes = Aes.Create())
        {
            aes.Key = encryptionKey;
            aes.IV = encryptionIV;
            using var enc = aes.CreateEncryptor();
            return PerformCryptography(plain, enc);
        }
    }

    /// <summary>
    /// Decrypts AES encrypted bytes using the runtime-loaded key and IV. Caller
    /// must ensure <see cref="encryptionConfigured"/> is true before invoking
    /// this helper.
    /// </summary>
    private static byte[] DecryptBytes(byte[] cipher)
    {
        using (var aes = Aes.Create())
        {
            aes.Key = encryptionKey;
            aes.IV = encryptionIV;
            using var dec = aes.CreateDecryptor();
            return PerformCryptography(cipher, dec);
        }
    }

    /// <summary>
    /// Performs the core cryptographic transformation using a CryptoStream so
    /// both encryption and decryption share the same implementation.
    /// </summary>
    private static byte[] PerformCryptography(byte[] data, ICryptoTransform transform)
    {
        using var ms = new MemoryStream();
        using (var cs = new CryptoStream(ms, transform, CryptoStreamMode.Write))
        {
            cs.Write(data, 0, data.Length);
        }
        return ms.ToArray();
    }

    // Maximum number of times a failed save request will be retried before
    // giving up. This guards against infinite retry loops on persistent
    // failures such as read-only disks.
    private const int MaxSaveAttempts = 3;

    // Fields used for asynchronous, thread-safe file saving. Requests are
    // queued so only one write happens at a time and the main thread remains
    // responsive even on slow disks.
    private readonly object queueLock = new object();
    private readonly Queue<SaveRequest> saveQueue = new Queue<SaveRequest>();
    private Task processingTask = Task.CompletedTask;
    private readonly ConcurrentQueue<Action> completionActions = new ConcurrentQueue<Action>();
    // Maximum time to wait for pending saves during application shutdown or
    // destruction. A short timeout prevents the game from hanging indefinitely
    // if the disk is unresponsive.
    private static readonly TimeSpan ShutdownFlushTimeout = TimeSpan.FromSeconds(2);

    // ---------------------------------------------------------------------
    // Autosave support
    // ---------------------------------------------------------------------
    // Flag indicating whether in-memory data differs from what is persisted on
    // disk. Property setters flip this on and the autosave coroutine clears it
    // once a save completes.
    private bool dataDirty;

    // Timestamp of the last successful write. Used to throttle disk writes so
    // repeated property changes within a short window are batched together.
    private float lastSaveTime;

    // Reference to the running autosave coroutine so multiple instances are not
    // started concurrently.
    private Coroutine autoSaveCoroutine;

    // Minimum number of seconds between successive save operations.
    private const float AutoSaveInterval = 2f;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            // Use the SaveSlotManager so each profile writes to its own file
            savePath = SaveSlotManager.GetPath("savegame.json");
            // Begin loading asynchronously so the main thread remains
            // responsive during startup. The resulting task is exposed via
            // <see cref="Initialization"/> so callers can await completion.
            loadTask = LoadDataAsync();
            // Initialize save timestamp so the first autosave waits the
            // configured interval rather than firing immediately.
            lastSaveTime = Time.realtimeSinceStartup;
            // Subscribe to the global quit event so pending saves can be flushed
            // asynchronously before the process exits.
            Application.quitting += HandleApplicationQuitting;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Unity coroutine automatically invoked after <see cref="Awake"/>. It
    /// waits for the asynchronous load started during Awake to finish without
    /// blocking the main thread. This ensures the manager's data is fully
    /// populated before other systems rely on it.
    /// </summary>
    private IEnumerator Start()
    {
        yield return new WaitUntil(() => loadTask.IsCompleted);
    }

    /// <summary>Current coin balance.</summary>
    public int Coins
    {
        get => data.coins;
        set
        {
            int clamped = Mathf.Max(0, value);
            if (clamped != data.coins)
            {
                data.coins = clamped;
                MarkDataDirty();
            }
        }
    }

    /// <summary>Stored best distance traveled.</summary>
    public int HighScore
    {
        get => data.highScore;
        set
        {
            int clamped = Mathf.Max(0, value);
            if (clamped != data.highScore)
            {
                data.highScore = clamped;
                MarkDataDirty();
            }
        }
    }

    /// <summary>Stored music volume in the range 0–1.</summary>
    public float MusicVolume
    {
        get => data.musicVolume;
        set
        {
            float clamped = Mathf.Clamp01(value);
            if (!Mathf.Approximately(clamped, data.musicVolume))
            {
                data.musicVolume = clamped;
                MarkDataDirty();
            }
        }
    }

    /// <summary>Stored effects volume in the range 0–1.</summary>
    public float EffectsVolume
    {
        get => data.effectsVolume;
        set
        {
            float clamped = Mathf.Clamp01(value);
            if (!Mathf.Approximately(clamped, data.effectsVolume))
            {
                data.effectsVolume = clamped;
                MarkDataDirty();
            }
        }
    }

    /// <summary>Currently selected language code.</summary>
    public string Language
    {
        get => data.language;
        set
        {
            if (!string.IsNullOrEmpty(value) && value != data.language)
            {
                data.language = value;
                MarkDataDirty();
                // Immediately inform the localization system so UI updates
                // reflect the change even before the next save occurs.
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
            if (data.tutorialCompleted != value)
            {
                data.tutorialCompleted = value;
                MarkDataDirty();
            }
        }
    }

    /// <summary>Whether the jump hint has already been shown.</summary>
    public bool JumpTipShown
    {
        get => data.jumpTipShown;
        set
        {
            if (data.jumpTipShown != value)
            {
                data.jumpTipShown = value;
                MarkDataDirty();
            }
        }
    }

    /// <summary>Whether the slide hint has already been shown.</summary>
    public bool SlideTipShown
    {
        get => data.slideTipShown;
        set
        {
            if (data.slideTipShown != value)
            {
                data.slideTipShown = value;
                MarkDataDirty();
            }
        }
    }

    /// <summary>Whether hardcore mode is enabled.</summary>
    public bool HardcoreMode
    {
        get => data.hardcoreMode;
        set
        {
            if (data.hardcoreMode != value)
            {
                data.hardcoreMode = value;
                MarkDataDirty();
            }
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
        int clamped = Mathf.Max(0, level);
        if (!upgradeLevels.TryGetValue(type, out int current) || current != clamped)
        {
            upgradeLevels[type] = clamped;
            MarkDataDirty();
        }
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

        MarkDataDirty();
    }

    /// <summary>
    /// Marks the save data as needing persistence and schedules the autosave
    /// coroutine if it is not already running. Batching saves reduces disk IO
    /// when many values change in quick succession.
    /// </summary>
    private void MarkDataDirty()
    {
        dataDirty = true;
        if (autoSaveCoroutine == null)
        {
            autoSaveCoroutine = StartCoroutine(AutoSaveRoutine());
        }
    }

    /// <summary>
    /// Coroutine that periodically checks for dirty data and writes it to disk.
    /// Saves are throttled so that even rapid property updates result in at most
    /// one write every <see cref="AutoSaveInterval"/> seconds.
    /// </summary>
    private System.Collections.IEnumerator AutoSaveRoutine()
    {
        while (true)
        {
            if (!dataDirty)
            {
                autoSaveCoroutine = null;
                yield break; // No work pending; stop the coroutine
            }

            float elapsed = Time.realtimeSinceStartup - lastSaveTime;
            if (elapsed < AutoSaveInterval)
            {
                // Wait just enough so successive saves are spaced out.
                yield return new WaitForSecondsRealtime(AutoSaveInterval - elapsed);
            }
            else
            {
                yield return null; // Ensure at least one frame passes
            }

            if (dataDirty)
            {
                SaveDataToFile();
            }
        }
    }

    /// <summary>
    /// Reads existing save data from disk or migrates old PlayerPrefs data when
    /// the save file does not yet exist.
    /// </summary>
    private async Task LoadDataAsync()
    {
        if (File.Exists(savePath))
        {
            try
            {
                // Asynchronously read the entire save file so disk IO does not
                // stall the main thread during startup.
                string json = await File.ReadAllTextAsync(savePath);
                SaveFile wrapper = JsonUtility.FromJson<SaveFile>(json);
                SaveData loaded = null;
                if (wrapper != null)
                {
                    if (wrapper.encrypted)
                    {
                        if (!encryptionConfigured)
                            throw new InvalidOperationException("Save file is encrypted but AES key/IV are unavailable");
                        byte[] cipher = Convert.FromBase64String(wrapper.payload ?? string.Empty);
                        string expected = wrapper.checksum ?? string.Empty;
                        string actual = ComputeChecksum(cipher);
                        if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
                            throw new InvalidDataException("Checksum mismatch");
                        byte[] plain = DecryptBytes(cipher);
                        string payloadJson = Encoding.UTF8.GetString(plain);
                        loaded = JsonUtility.FromJson<SaveData>(payloadJson);
                    }
                    else
                    {
                        if (wrapper.data == null)
                            throw new InvalidDataException("Missing payload");
                        string payloadJson = JsonUtility.ToJson(wrapper.data);
                        string actual = ComputeChecksum(Encoding.UTF8.GetBytes(payloadJson));
                        if (!string.Equals(wrapper.checksum, actual, StringComparison.OrdinalIgnoreCase))
                            throw new InvalidDataException("Checksum mismatch");
                        loaded = wrapper.data;
                    }
                }

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
                LoggingHelper.LogWarning("Failed to parse save file, starting fresh: " + ex.Message); // Parse errors routed through helper.
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

        string payloadJson = JsonUtility.ToJson(data);

        // Wrap the payload with a checksum and optional AES encryption so
        // tampering can be detected during load. The checksum is calculated over
        // the exact byte sequence written to disk (encrypted or plain).
        SaveFile wrapper = new SaveFile();
        if (EncryptSaves && encryptionConfigured)
        {
            byte[] plain = Encoding.UTF8.GetBytes(payloadJson);
            byte[] cipher = EncryptBytes(plain);
            wrapper.encrypted = true;
            wrapper.payload = Convert.ToBase64String(cipher);
            wrapper.checksum = ComputeChecksum(cipher);
        }
        else
        {
            if (EncryptSaves && !encryptionConfigured)
            {
                // Documented fallback: when AES secrets are missing the save is
                // written in plaintext so progress is not lost. Developers can
                // configure the required environment variables to enable
                // encryption in their builds.
                LoggingHelper.LogWarning("Save encryption requested but AES key/IV are unavailable; writing plaintext save."); // Warning respects verbosity settings.
            }
            wrapper.encrypted = false;
            wrapper.data = data; // store as object for human readability
            wrapper.checksum = ComputeChecksum(Encoding.UTF8.GetBytes(payloadJson));
        }

        string json = JsonUtility.ToJson(wrapper);

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

        // Mark current state as clean and record the time so future autosaves
        // can throttle excessive writes.
        dataDirty = false;
        lastSaveTime = Time.realtimeSinceStartup;
    }

    /// <summary>
    /// Background loop that processes queued save requests one at a time. Each
    /// JSON payload is written to disk using async <see cref="FileStream"/>
    /// APIs. Failures mark the data as dirty and requests are retried up to
    /// <see cref="MaxSaveAttempts"/> times so transient issues do not result in
    /// lost progress. Any errors are enqueued so they can be reported on the
    /// main thread during <see cref="Update"/>.
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
                // Attempt the disk write. A successful write means the in-memory
                // state now matches what is on disk, so the dirty flag can be
                // cleared.
                await WriteFileAsync(tempPath, request.Path, request.Json);
                dataDirty = false;
            }
            catch (IOException ex)
            {
                // Mark data as dirty so the autosave coroutine schedules
                // another write attempt. The failed request is re-enqueued up to
                // <see cref="MaxSaveAttempts"/> times so transient IO issues
                // have additional chances to succeed.
                dataDirty = true;
                completionActions.Enqueue(() => LoggingHelper.LogWarning($"Failed to write save file: {ex.Message}")); // Enqueued warning uses helper on main thread.

                if (request.Attempts + 1 < MaxSaveAttempts)
                {
                    lock (queueLock)
                    {
                        saveQueue.Enqueue(new SaveRequest(request.Json, request.Path, request.Attempts + 1));
                    }
                }
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    try
                    {
                        File.Delete(tempPath);
                    }
                    catch (Exception ex)
                    {
                        // Surface cleanup problems on the main thread so they
                        // are not silently ignored when running off-thread.
                        completionActions.Enqueue(() => LoggingHelper.LogWarning($"Failed to delete temporary save file '{tempPath}': {ex.Message}")); // Logged via helper when cleanup fails.
                    }
                }
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
    /// Flushes any queued save requests asynchronously and waits for completion
    /// up to <paramref name="timeout"/>. The latest in-memory data is enqueued
    /// before waiting so the final state is persisted. Any timeout is logged as
    /// a warning to aid debugging slow or failing disks.
    /// </summary>
    /// <param name="timeout">Maximum duration to wait for pending writes.</param>
    private async Task FlushPendingSavesAsync(TimeSpan timeout)
    {
        // Ensure the most recent data is queued for saving.
        if (dataDirty)
        {
            SaveDataToFile();
        }

        Task toWait;
        lock (queueLock)
        {
            toWait = processingTask;
        }

        if (toWait != null && !toWait.IsCompleted)
        {
            try
            {
                // Task.WaitAsync is unavailable on .NET Standard 2.1, so we
                // replicate its timeout behaviour using Task.WhenAny. The
                // delay task serves as the timeout; whichever completes first
                // determines whether we succeeded or timed out.
                Task completed = await Task.WhenAny(toWait, Task.Delay(timeout));

                if (completed != toWait)
                {
                    // The save did not finish within the allotted time.
                    throw new TimeoutException();
                }

                // If we reach this point the save task completed before the
                // timeout. Await again to propagate any exceptions.
                await toWait;
            }
            catch (TimeoutException)
            {
                // Surface the timeout via LoggingHelper so callers are aware
                // that data may not have been persisted.
                LoggingHelper.LogWarning($"SaveGameManager flush timed out after {timeout.TotalSeconds:F1}s; data may be lost.");
            }
        }

        // Execute any completion callbacks generated by the background thread
        // so warnings surface even during shutdown.
        while (completionActions.TryDequeue(out var action))
        {
            action?.Invoke();
        }
    }

    /// <summary>
    /// Triggered via <see cref="Application.quitting"/> to start an asynchronous
    /// flush without blocking Unity's shutdown sequence.
    /// </summary>
    private void HandleApplicationQuitting()
    {
        // Fire-and-forget so quitting continues immediately. Any timeout is
        // reported by <see cref="FlushPendingSavesAsync"/>.
        _ = FlushPendingSavesAsync(ShutdownFlushTimeout);
    }

    /// <summary>
    /// Executes any completion callbacks that were queued by background save
    /// operations. This method runs on the Unity main thread and therefore may
    /// safely interact with Unity APIs such as <see cref="LoggingHelper.Log"/>.
    /// </summary>
    void Update()
    {
        while (completionActions.TryDequeue(out var action))
        {
            action?.Invoke();
        }
    }

    /// <summary>
    /// Initiates an asynchronous flush of pending save operations before the
    /// manager is destroyed. The flush runs in the background so destruction
    /// returns immediately. A warning is logged if the flush fails to complete
    /// within <see cref="ShutdownFlushTimeout"/>.
    /// </summary>
    void OnDestroy()
    {
        // Begin the flush without awaiting so this method returns promptly and
        // does not stall the Unity shutdown sequence.
        var flushTask = FlushPendingSavesAsync(Timeout.InfiniteTimeSpan);

        // Monitor the flush task on a background thread. If it fails to finish
        // within the allotted timeout, surface a warning so potential data loss
        // is visible to developers and testers.
        _ = Task.Run(async () =>
        {
            Task completed = await Task.WhenAny(flushTask, Task.Delay(ShutdownFlushTimeout));
            if (completed != flushTask)
            {
                LoggingHelper.LogWarning(
                    $"SaveGameManager flush during OnDestroy exceeded {ShutdownFlushTimeout.TotalSeconds:F1}s; data may be lost.");
            }
        });

        // Remove event subscription to avoid callbacks to a destroyed instance.
        Application.quitting -= HandleApplicationQuitting;

        // Execute remaining completion actions so queued warnings surface even
        // if the manager is torn down without going through the quit path.
        while (completionActions.TryDequeue(out var action))
        {
            action?.Invoke();
        }
    }

    /// <summary>
    /// Asynchronously switches to a different save slot at runtime. Any pending
    /// save operations for the current profile are flushed before the slot is
    /// changed to ensure data persists to the correct location. Callers should
    /// await the returned task to guarantee the new slot is fully loaded.
    /// </summary>
    /// <param name="slot">Index of the slot to activate.</param>
    /// <returns>A task that completes once the slot has been switched and data
    /// from the new slot has been loaded.</returns>
    public async Task ChangeSlot(int slot)
    {
        if (slot < 0 || slot >= SaveSlotManager.MaxSlots)
        {
            // Validate input early to provide clear debugging information.
            throw new ArgumentOutOfRangeException(nameof(slot), "Invalid save slot index");
        }

        if (slot == SaveSlotManager.CurrentSlot)
        {
            return; // already using this slot, nothing to do
        }

        // Flush any queued saves for the current slot. Using an infinite
        // timeout mirrors the previous blocking behaviour but now allows
        // callers to await rather than stall the main thread.
        await FlushPendingSavesAsync(Timeout.InfiniteTimeSpan);

        // Redirect file paths to the new slot and load its data asynchronously
        // so the main thread remains responsive during the potentially slow
        // disk read.
        SaveSlotManager.SetSlot(slot);
        savePath = SaveSlotManager.GetPath("savegame.json");
        loadTask = LoadDataAsync();
        await loadTask;
    }
}
