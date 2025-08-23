using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Collections;

/// <summary>
/// Tests covering the JSON serialization and migration behaviour of
/// <see cref="SaveGameManager"/>.
/// </summary>
public class SaveGameManagerTests
{
    [SetUp]
    public void CleanUp()
    {
        // Ensure a clean file and PlayerPrefs state before each test
        PlayerPrefs.DeleteAll();
        for (int i = 0; i < SaveSlotManager.MaxSlots; i++)
        {
            string path = SaveSlotManager.GetPath("savegame.json").Replace($"slot_{SaveSlotManager.CurrentSlot}", $"slot_{i}");
            if (File.Exists(path))
                File.Delete(path);
            string dir = Path.GetDirectoryName(path);
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
        }
    }

    /// <summary>
    /// Creates a <see cref="SaveGameManager"/> or subclass and blocks until its
    /// asynchronous initialization has completed. Tests rely on this helper so
    /// they operate on fully loaded data despite the production code loading
    /// in the background.
    /// </summary>
    private static T CreateManager<T>(string name) where T : SaveGameManager
    {
        var go = new GameObject(name);
        var mgr = go.AddComponent<T>();
        mgr.Initialization.GetAwaiter().GetResult();
        return mgr;
    }

    /// <summary>
    /// Flushes any pending asynchronous save operation for the provided manager
    /// and destroys its GameObject. Tests use this helper to ensure data reaches
    /// disk before a new <see cref="SaveGameManager"/> is instantiated.
    /// </summary>
    private static void FlushAndDestroy(SaveGameManager mgr)
    {
        MethodInfo method = typeof(SaveGameManager).GetMethod("FlushPendingSavesAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        var task = (Task)method.Invoke(mgr, new object[] { TimeSpan.FromSeconds(2) });
        task.GetAwaiter().GetResult();
        Object.DestroyImmediate(mgr.gameObject);
    }

    /// <summary>
    /// Ensures that valid AES key and IV environment variables are parsed and
    /// flagged as available so optional save encryption can proceed.
    /// </summary>
    [Test]
    public void LoadEncryptionSecrets_FromEnvironment_Succeeds()
    {
        string key = Convert.ToBase64String(new byte[32]);
        string iv = Convert.ToBase64String(new byte[16]);
        Environment.SetEnvironmentVariable("CR_AES_KEY", key);
        Environment.SetEnvironmentVariable("CR_AES_IV", iv);

        MethodInfo load = typeof(SaveGameManager).GetMethod("LoadEncryptionSecrets", BindingFlags.NonPublic | BindingFlags.Static);
        load.Invoke(null, null);

        FieldInfo configured = typeof(SaveGameManager).GetField("encryptionConfigured", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsTrue((bool)configured.GetValue(null), "Valid secrets should enable encryption");

        // Cleanup so subsequent tests observe the default unconfigured state.
        Environment.SetEnvironmentVariable("CR_AES_KEY", null);
        Environment.SetEnvironmentVariable("CR_AES_IV", null);
    }

    /// <summary>
    /// When AES key and IV are absent, the manager should disable encryption and
    /// operate without throwing so save operations still succeed.
    /// </summary>
    [Test]
    public void LoadEncryptionSecrets_MissingVariables_DisablesEncryption()
    {
        Environment.SetEnvironmentVariable("CR_AES_KEY", null);
        Environment.SetEnvironmentVariable("CR_AES_IV", null);

        MethodInfo load = typeof(SaveGameManager).GetMethod("LoadEncryptionSecrets", BindingFlags.NonPublic | BindingFlags.Static);
        load.Invoke(null, null);

        FieldInfo configured = typeof(SaveGameManager).GetField("encryptionConfigured", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsFalse((bool)configured.GetValue(null), "Missing secrets should leave encryption disabled");

        // Ensure environment variables remain cleared for other tests.
        Environment.SetEnvironmentVariable("CR_AES_KEY", null);
        Environment.SetEnvironmentVariable("CR_AES_IV", null);
    }

    [Test]
    public void SaveAndLoad_PersistsData()
    {
        var save = CreateManager<SaveGameManager>("save");
        save.Coins = 7;
        save.HighScore = 12;
        save.SetUpgradeLevel(UpgradeType.MagnetDuration, 2);
        // Ensure asynchronous save completes before creating a new instance.
        FlushAndDestroy(save);

        // Create a new instance which should load from the file
        var save2 = CreateManager<SaveGameManager>("save2");

        Assert.AreEqual(7, save2.Coins);
        Assert.AreEqual(12, save2.HighScore);
        Assert.AreEqual(2, save2.GetUpgradeLevel(UpgradeType.MagnetDuration));
        FlushAndDestroy(save2);
    }

    [Test]
    public void Migration_LoadsPlayerPrefs()
    {
        // Prepare legacy PlayerPrefs data
        PlayerPrefs.SetInt("ShopCoins", 3);
        PlayerPrefs.SetInt("HighScore", 4);
        PlayerPrefs.SetInt("UpgradeLevel_MagnetDuration", 1);
        PlayerPrefs.Save();

        var save = CreateManager<SaveGameManager>("save");

        Assert.AreEqual(3, save.Coins);
        Assert.AreEqual(4, save.HighScore);
        Assert.AreEqual(1, save.GetUpgradeLevel(UpgradeType.MagnetDuration));
        // File should now exist after migration
        Assert.IsTrue(File.Exists(Path.Combine(Application.persistentDataPath, "savegame.json")));
        FlushAndDestroy(save);
    }

    [Test]
    public void Save_UsesTemporaryFile()
    {
        // Saving should not leave the temporary file used for atomic writes.
        var save = CreateManager<SaveGameManager>("save");

        save.Coins = 1; // triggers SaveDataToFile

        // Wait for asynchronous saving to finish before checking for cleanup.
        FlushAndDestroy(save);

        string temp = Path.Combine(Application.persistentDataPath, "savegame.json.tmp");
        Assert.IsFalse(File.Exists(temp));
    }

    [Test]
    public void TempFileDeletionFailure_LogsWarning()
    {
        // Simulate a locked temporary file so the background cleanup step
        // cannot remove it. The manager should enqueue a warning for the main
        // thread rather than silently swallowing the issue.
        var save = CreateManager<LockedTempSaveGameManager>("save");
        save.Coins = 1; // triggers SaveDataToFile and locks the temp file

        // Expect the queued warning to surface once pending operations flush.
        LogAssert.Expect(LogType.Warning, new Regex("Failed to delete temporary save file"));

        // Flush pending saves using reflection so the test can verify the
        // queued log without destroying the manager prematurely.
        MethodInfo method = typeof(SaveGameManager).GetMethod(
            "FlushPendingSavesAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        var task = (Task)method.Invoke(save, new object[] { TimeSpan.FromSeconds(2) });
        task.GetAwaiter().GetResult();

        string tempPath = Path.Combine(Application.persistentDataPath, "savegame.json.tmp");
        Assert.IsTrue(File.Exists(tempPath), "Temp file should remain when deletion fails");

        // Release the intentional lock so later tests can clean up the file.
        save.ReleaseLock();
        File.Delete(tempPath);
        Object.DestroyImmediate(save.gameObject);
    }

    [Test]
    public void VolumeValues_ArePersisted()
    {
        var save = CreateManager<SaveGameManager>("save");
        save.MusicVolume = 0.4f;
        save.EffectsVolume = 0.7f;
        FlushAndDestroy(save);

        var save2 = CreateManager<SaveGameManager>("save2");
        Assert.AreEqual(0.4f, save2.MusicVolume, 0.001f);
        Assert.AreEqual(0.7f, save2.EffectsVolume, 0.001f);
        FlushAndDestroy(save2);
    }

    [Test]
    public void OldSave_DefaultsToFullVolume()
    {
        // Write an old-format save file without volume fields
        string path = Path.Combine(Application.persistentDataPath, "savegame.json");
        File.WriteAllText(path, "{\"coins\":1,\"highScore\":2,\"upgrades\":[]}");

        var save = CreateManager<SaveGameManager>("save");

        Assert.AreEqual(1f, save.MusicVolume);
        Assert.AreEqual(1f, save.EffectsVolume);
        FlushAndDestroy(save);
    }

    [Test]
    public void VersionField_WrittenToFile()
    {
        var save = CreateManager<SaveGameManager>("save");
        FlushAndDestroy(save);

        string path = Path.Combine(Application.persistentDataPath, "savegame.json");
        string json = File.ReadAllText(path);
        Assert.IsTrue(json.Contains("\"version\""));
    }

    [Test]
    public void LanguageValue_IsPersisted()
    {
        // Save a language preference and ensure it loads correctly on next startup
        var save = CreateManager<SaveGameManager>("save");
        save.Language = "es";
        FlushAndDestroy(save);

        var save2 = CreateManager<SaveGameManager>("save2");
        Assert.AreEqual("es", save2.Language);
        FlushAndDestroy(save2);
    }

    [Test]
    public void MissingUpgrades_DoesNotThrow()
    {
        // Simulate an old save file without the upgrades list to ensure
        // LoadData handles null gracefully after the recent bug fix.
        string path = Path.Combine(Application.persistentDataPath, "savegame.json");
        File.WriteAllText(path, "{\"coins\":2,\"highScore\":3}");

        var save = CreateManager<SaveGameManager>("save");

        Assert.AreEqual(2, save.Coins);
        Assert.AreEqual(3, save.HighScore);
        Assert.AreEqual(0, save.GetUpgradeLevel(UpgradeType.MagnetDuration));

        FlushAndDestroy(save);
    }

    [Test]
    public async Task ChangeSlot_SwitchesSavePath()
    {
        // Write data to the initial slot then change to a new slot and verify
        // values persist separately.
        SaveSlotManager.SetSlot(0);
        var mgr = CreateManager<SaveGameManager>("save");
        mgr.Coins = 2;
        await mgr.ChangeSlot(1);
        mgr.Coins = 5;
        FlushAndDestroy(mgr);

        var mgr2 = CreateManager<SaveGameManager>("save2");
        Assert.AreEqual(5, mgr2.Coins, "Slot 1 should contain updated value");
        await mgr2.ChangeSlot(0);
        Assert.AreEqual(2, mgr2.Coins, "Slot 0 should retain original value");
        FlushAndDestroy(mgr2);
    }

    /// <summary>
    /// Switching slots while a save is still pending should wait for the write
    /// to complete so data ends up in the original slot rather than the new
    /// one. Uses <see cref="SlowSaveGameManager"/> to simulate sluggish IO.
    /// </summary>
    [Test]
    public async Task ChangeSlot_WaitsForPendingWrites()
    {
        // Begin in slot 0 and queue a save that will complete slowly.
        SaveSlotManager.SetSlot(0);
        var mgr = CreateManager<SlowSaveGameManager>("save");
        mgr.Coins = 1; // queue save for slot 0

        // Immediately switch to slot 1. ChangeSlot should block until the first
        // save finishes so the data lands in slot 0.
        await mgr.ChangeSlot(1);
        mgr.Coins = 2; // save to slot 1
        FlushAndDestroy(mgr);

        // Verify slot 0 contains the first value and slot 1 the second.
        SaveSlotManager.SetSlot(0);
        var mgr0 = CreateManager<SaveGameManager>("check0");
        Assert.AreEqual(1, mgr0.Coins, "Slot 0 should persist initial value");
        FlushAndDestroy(mgr0);

        SaveSlotManager.SetSlot(1);
        var mgr1 = CreateManager<SaveGameManager>("check1");
        Assert.AreEqual(2, mgr1.Coins, "Slot 1 should contain updated value");
        FlushAndDestroy(mgr1);
    }

    /// <summary>
    /// Saving to an invalid path should log a warning rather than throwing an
    /// exception so the game can continue running.
    /// </summary>
    [Test]
    public void SaveDataToFile_InvalidPath_LogsWarning()
    {
        var mgr = CreateManager<SaveGameManager>("save");

        FieldInfo pathField = typeof(SaveGameManager).GetField("savePath", BindingFlags.NonPublic | BindingFlags.Instance);
        pathField.SetValue(mgr, Path.Combine(Application.dataPath, "no_such_dir", "save.json"));

        LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Failed to write save file"));

        MethodInfo method = typeof(SaveGameManager).GetMethod("SaveDataToFile", BindingFlags.NonPublic | BindingFlags.Instance);
        method.Invoke(mgr, null);

        FlushAndDestroy(mgr);
    }

    /// <summary>
    /// When disk writes repeatedly fail the manager should mark its data as
    /// dirty and attempt the operation multiple times so transient issues do not
    /// permanently prevent saving.
    /// </summary>
    [Test]
    public void ProcessQueueAsync_Failure_SetsDirtyAndRetries()
    {
        var mgr = CreateManager<AlwaysFailingSaveGameManager>("fail");

        // Queue a save operation which will consistently throw.
        MethodInfo method = typeof(SaveGameManager).GetMethod("SaveDataToFile", BindingFlags.NonPublic | BindingFlags.Instance);
        method.Invoke(mgr, null);

        // Wait for the background processing task to exhaust its retry attempts.
        FieldInfo taskField = typeof(SaveGameManager).GetField("processingTask", BindingFlags.NonPublic | BindingFlags.Instance);
        var task = (Task)taskField.GetValue(mgr);
        task.GetAwaiter().GetResult();

        // The manager should now consider its data dirty because no write
        // succeeded.
        FieldInfo dirtyField = typeof(SaveGameManager).GetField("dataDirty", BindingFlags.NonPublic | BindingFlags.Instance);
        bool dirty = (bool)dirtyField.GetValue(mgr);
        Assert.IsTrue(dirty, "Failed save should mark data dirty to trigger retry.");

        // Verify that the save was attempted multiple times to implement a
        // simple retry mechanism.
        Assert.Greater(mgr.Calls, 1, "Save operation should be retried at least once.");

        Object.DestroyImmediate(mgr.gameObject);
    }

    /// <summary>
    /// When the save file contains invalid JSON the manager should discard it and
    /// create a new save using default values. The default language must also be
    /// reapplied so text remains readable.
    /// </summary>
    [Test]
    public void LoadData_InvalidJson_CreatesDefaultSave()
    {
        // Force localization into a non-default state to ensure the reset path
        // invokes LocalizationManager.SetLanguage.
        LocalizationManager.SetLanguage("es");

        // Write intentionally malformed JSON to simulate a corrupt save file.
        string path = Path.Combine(Application.persistentDataPath, "savegame.json");
        File.WriteAllText(path, "{ invalid json }");

        // Loading should detect the bad file and regenerate a default save.
        var mgr = CreateManager<SaveGameManager>("save");

        // All persistent values should revert to their defaults.
        Assert.AreEqual(0, mgr.Coins, "Coins should reset when save is invalid");
        Assert.AreEqual(0, mgr.HighScore, "High score should reset when save is invalid");
        Assert.AreEqual("en", mgr.Language, "Language property should revert to default");
        Assert.AreEqual("en", LocalizationManager.CurrentLanguage, "LocalizationManager should apply default language");

        // File should now contain valid JSON describing the default save state.
        string json = File.ReadAllText(path);
        StringAssert.Contains("\"version\"", json);

        FlushAndDestroy(mgr);
    }

    /// <summary>
    /// Subclass that intentionally keeps a handle open on the temporary file so
    /// <see cref="ProcessQueueAsync"/> cannot delete it. Used to verify that
    /// deletion failures are reported back to the main thread.
    /// </summary>
    private class LockedTempSaveGameManager : SaveGameManager
    {
        private FileStream heldStream;

        protected override async Task WriteFileAsync(string tempPath, string finalPath, string json)
        {
            await base.WriteFileAsync(tempPath, finalPath, json);
            // Open the temp file without sharing so later deletion attempts
            // throw, simulating a locked file on disk.
            heldStream = new FileStream(tempPath, FileMode.Open, FileAccess.Read, FileShare.None);
        }

        public void ReleaseLock()
        {
            heldStream?.Dispose();
        }
    }

    // Spy subclass used to count save operations
    private class SaveGameManagerSpy : SaveGameManager
    {
        public int Calls { get; private set; }
        protected override void SaveDataToFile()
        {
            Calls++;
            base.SaveDataToFile();
        }
    }

    /// <summary>
    /// Subclass whose writes always fail. Used to verify that the manager marks
    /// data as dirty and retries when disk operations throw.
    /// </summary>
    private class AlwaysFailingSaveGameManager : SaveGameManager
    {
        public int Calls { get; private set; }

        protected override Task WriteFileAsync(string tempPath, string finalPath, string json)
        {
            Calls++;
            throw new IOException("Simulated write failure");
        }
    }

    /// <summary>
    /// Subclass used to simulate a slow disk by delaying the asynchronous write.
    /// </summary>
    private class SlowSaveGameManager : SaveGameManager
    {
        protected override async Task WriteFileAsync(string tempPath, string finalPath, string json)
        {
            await Task.Delay(200); // emulate sluggish IO
            await base.WriteFileAsync(tempPath, finalPath, json);
        }
    }

    /// <summary>
    /// Updating multiple upgrade levels in quick succession should still result
    /// in only one disk write thanks to the autosave batching logic.
    /// </summary>
    [UnityTest]
    public IEnumerator UpdateUpgradeLevels_BatchesWrites()
    {
        var mgr = CreateManager<SaveGameManagerSpy>("save");

        var dict1 = new Dictionary<UpgradeType, int>
        {
            { UpgradeType.MagnetDuration, 1 },
        };

        var dict2 = new Dictionary<UpgradeType, int>
        {
            { UpgradeType.SpeedBoostDuration, 2 },
        };

        mgr.UpdateUpgradeLevels(dict1);
        mgr.UpdateUpgradeLevels(dict2); // second call before autosave fires

        // Wait slightly longer than the autosave interval so any pending save
        // has time to execute.
        float interval = (float)typeof(SaveGameManager).GetField("AutoSaveInterval", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        yield return new WaitForSecondsRealtime(interval + 0.1f);

        Assert.AreEqual(1, mgr.Calls, "Autosave should consolidate rapid changes into one write");

        FlushAndDestroy(mgr);
    }

    /// <summary>
    /// Setting properties should mark data dirty without immediately saving; an
    /// autosave should occur after the configured interval.
    /// </summary>
    [UnityTest]
    public IEnumerator PropertySetters_TriggerDelayedSave()
    {
        var mgr = CreateManager<SaveGameManagerSpy>("save");

        mgr.Coins = 5; // mark data dirty

        // Save should not fire immediately.
        Assert.AreEqual(0, mgr.Calls, "Setter should not write instantly");

        float interval = (float)typeof(SaveGameManager).GetField("AutoSaveInterval", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        yield return new WaitForSecondsRealtime(interval + 0.1f);

        Assert.AreEqual(1, mgr.Calls, "Autosave should persist data after delay");

        FlushAndDestroy(mgr);
    }

    /// <summary>
    /// Verifies that saving does not block the main thread even if the disk is
    /// extremely slow.
    /// </summary>
    [Test]
    public void SlowDisk_DoesNotBlockMainThread()
    {
        var mgr = CreateManager<SlowSaveGameManager>("save");

        var timer = Stopwatch.StartNew();
        mgr.Coins = 3; // triggers asynchronous save
        timer.Stop();

        Assert.Less(timer.ElapsedMilliseconds, 100, "Setter should return before slow write completes");

        FlushAndDestroy(mgr);
    }

    /// <summary>
    /// Invoking the quit handler should not block the main thread yet still
    /// persist pending data before a new manager loads it.
    /// </summary>
    [Test]
    public void QuitHandler_FlushesWithoutBlocking()
    {
        var mgr = CreateManager<SlowSaveGameManager>("save");
        mgr.Coins = 9; // queue save

        // Reflectively invoke the private handler used by Application.quitting.
        MethodInfo quit = typeof(SaveGameManager).GetMethod("HandleApplicationQuitting", BindingFlags.NonPublic | BindingFlags.Instance);
        var timer = Stopwatch.StartNew();
        quit.Invoke(mgr, null); // should return immediately
        timer.Stop();
        Assert.Less(timer.ElapsedMilliseconds, 100, "Quit handler should return quickly");

        // Allow the asynchronous save to finish so data reaches disk.
        Task.Delay(300).Wait();

        var mgr2 = CreateManager<SaveGameManager>("check");
        Assert.AreEqual(9, mgr2.Coins, "Data should persist after quit handler");

        FlushAndDestroy(mgr2);
        FlushAndDestroy(mgr);
    }

    /// <summary>
    /// Destroying the manager should trigger an asynchronous flush and return
    /// immediately, even when disk writes are slow.
    /// </summary>
    [Test]
    public void OnDestroy_DoesNotBlockOnSlowFlush()
    {
        var mgr = CreateManager<SlowSaveGameManager>("save");
        mgr.Coins = 5; // queue save

        var timer = Stopwatch.StartNew();
        Object.DestroyImmediate(mgr.gameObject); // invokes OnDestroy synchronously
        timer.Stop();
        Assert.Less(timer.ElapsedMilliseconds, 100,
            "OnDestroy should return promptly without waiting for slow flush");

        // Allow the asynchronous flush to complete before verifying the result.
        Task.Delay(300).Wait();

        var mgr2 = CreateManager<SaveGameManager>("check");
        Assert.AreEqual(5, mgr2.Coins, "Data should persist after OnDestroy");

        FlushAndDestroy(mgr2);
    }

    /// <summary>
    /// Verifies that <see cref="SaveGameManager.FlushPendingSavesAsync"/>
    /// respects the provided timeout by returning promptly and logging a
    /// warning when pending writes exceed the allotted duration.
    /// </summary>
    [Test]
    public void FlushPendingSavesAsync_TimesOut()
    {
        var mgr = CreateManager<SaveGameManager>("save");

        // Set up a long-running processing task to simulate a save that stalls.
        var delayTask = Task.Delay(TimeSpan.FromSeconds(5));
        FieldInfo lockField = typeof(SaveGameManager).GetField("queueLock", BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo procField = typeof(SaveGameManager).GetField("processingTask", BindingFlags.NonPublic | BindingFlags.Instance);
        object qLock = lockField.GetValue(mgr);
        lock (qLock)
        {
            procField.SetValue(mgr, delayTask);
        }

        // Expect a warning indicating the timeout occurred.
        LogAssert.Expect(LogType.Warning, new Regex("SaveGameManager flush timed out"));

        // Invoke the flush with a very short timeout and ensure it returns
        // quickly rather than waiting the full delay duration.
        MethodInfo flush = typeof(SaveGameManager).GetMethod("FlushPendingSavesAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        var timer = Stopwatch.StartNew();
        var task = (Task)flush.Invoke(mgr, new object[] { TimeSpan.FromMilliseconds(100) });
        task.GetAwaiter().GetResult();
        timer.Stop();
        Assert.Less(timer.ElapsedMilliseconds, 500, "Flush should respect timeout and return promptly.");

        // The simulated save should still be running because the flush timed out.
        Assert.IsFalse(delayTask.IsCompleted, "Stalled save task should continue running after timeout.");

        Object.DestroyImmediate(mgr.gameObject);
    }

    /// <summary>
    /// Ensures <see cref="SaveGameManager.FlushPendingSavesAsync"/> waits for the
    /// processing task when it completes within the timeout and that no warning
    /// is emitted in this success case.
    /// </summary>
    [Test]
    public void FlushPendingSavesAsync_CompletesBeforeTimeout()
    {
        var mgr = CreateManager<SaveGameManager>("save");

        // Simulate a short pending save operation.
        var delayTask = Task.Delay(50);
        FieldInfo lockField = typeof(SaveGameManager).GetField("queueLock", BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo procField = typeof(SaveGameManager).GetField("processingTask", BindingFlags.NonPublic | BindingFlags.Instance);
        object qLock = lockField.GetValue(mgr);
        lock (qLock)
        {
            procField.SetValue(mgr, delayTask);
        }

        MethodInfo flush = typeof(SaveGameManager).GetMethod("FlushPendingSavesAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        var timer = Stopwatch.StartNew();
        var task = (Task)flush.Invoke(mgr, new object[] { TimeSpan.FromSeconds(2) });
        task.GetAwaiter().GetResult();
        timer.Stop();

        // The call should wait at least as long as the delay to ensure the
        // processing task completed rather than timing out.
        Assert.GreaterOrEqual(timer.ElapsedMilliseconds, 50, "Flush should wait for the save task to finish.");
        Assert.IsTrue(delayTask.IsCompleted, "Processing task should complete within timeout.");

        // No warning should be produced in the successful path.
        LogAssert.NoUnexpectedReceived();

        Object.DestroyImmediate(mgr.gameObject);
    }

    /// <summary>
    /// Modifying the payload without updating the stored checksum should be
    /// detected and result in a reset to default values rather than loading the
    /// tampered data.
    /// </summary>
    [Test]
    public void LoadData_TamperedPayload_ResetsToDefaults()
    {
        var save = CreateManager<SaveGameManager>("save");
        save.Coins = 5;
        FlushAndDestroy(save);

        // Alter the on-disk JSON so the coins value no longer matches the
        // checksum. The manager should detect this during load and start fresh.
        string path = Path.Combine(Application.persistentDataPath, "savegame.json");
        string json = File.ReadAllText(path);
        File.WriteAllText(path, json.Replace("\"coins\":5", "\"coins\":999"));

        var mgr = CreateManager<SaveGameManager>("check");
        Assert.AreEqual(0, mgr.Coins, "Tampered file should trigger reset");
        FlushAndDestroy(mgr);
    }

    /// <summary>
    /// If the checksum itself is modified the mismatch should also be detected
    /// and trigger a reset to defaults, protecting against manual tampering.
    /// </summary>
    [Test]
    public void LoadData_TamperedChecksum_ResetsToDefaults()
    {
        var save = CreateManager<SaveGameManager>("save");
        save.Coins = 7;
        FlushAndDestroy(save);

        string path = Path.Combine(Application.persistentDataPath, "savegame.json");
        string json = File.ReadAllText(path);
        // Replace the checksum with an invalid value while keeping payload intact.
        json = Regex.Replace(json, "\"checksum\":\"[^\"]+\"", "\"checksum\":\"deadbeef\"");
        File.WriteAllText(path, json);

        var mgr = CreateManager<SaveGameManager>("check");
        Assert.AreEqual(0, mgr.Coins, "Checksum mismatch should reset save");
        FlushAndDestroy(mgr);
    }
}
