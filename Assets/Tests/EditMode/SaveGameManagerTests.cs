using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Diagnostics;

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

    [Test]
    public void SaveAndLoad_PersistsData()
    {
        var go = new GameObject("save");
        var save = go.AddComponent<SaveGameManager>();
        save.Coins = 7;
        save.HighScore = 12;
        save.SetUpgradeLevel(UpgradeType.MagnetDuration, 2);
        // Ensure asynchronous save completes before creating a new instance.
        FlushAndDestroy(save);

        // Create a new instance which should load from the file
        var go2 = new GameObject("save2");
        var save2 = go2.AddComponent<SaveGameManager>();

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

        var go = new GameObject("save");
        var save = go.AddComponent<SaveGameManager>();

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
        var go = new GameObject("save");
        var save = go.AddComponent<SaveGameManager>();

        save.Coins = 1; // triggers SaveDataToFile

        // Wait for asynchronous saving to finish before checking for cleanup.
        FlushAndDestroy(save);

        string temp = Path.Combine(Application.persistentDataPath, "savegame.json.tmp");
        Assert.IsFalse(File.Exists(temp));
    }

    [Test]
    public void VolumeValues_ArePersisted()
    {
        var obj = new GameObject("save");
        var save = obj.AddComponent<SaveGameManager>();
        save.MusicVolume = 0.4f;
        save.EffectsVolume = 0.7f;
        FlushAndDestroy(save);

        var obj2 = new GameObject("save2");
        var save2 = obj2.AddComponent<SaveGameManager>();
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

        var go = new GameObject("save");
        var save = go.AddComponent<SaveGameManager>();

        Assert.AreEqual(1f, save.MusicVolume);
        Assert.AreEqual(1f, save.EffectsVolume);
        FlushAndDestroy(save);
    }

    [Test]
    public void VersionField_WrittenToFile()
    {
        var go = new GameObject("save");
        var save = go.AddComponent<SaveGameManager>();
        FlushAndDestroy(save);

        string path = Path.Combine(Application.persistentDataPath, "savegame.json");
        string json = File.ReadAllText(path);
        Assert.IsTrue(json.Contains("\"version\""));
    }

    [Test]
    public void LanguageValue_IsPersisted()
    {
        // Save a language preference and ensure it loads correctly on next startup
        var go = new GameObject("save");
        var save = go.AddComponent<SaveGameManager>();
        save.Language = "es";
        FlushAndDestroy(save);

        var go2 = new GameObject("save2");
        var save2 = go2.AddComponent<SaveGameManager>();
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

        var go = new GameObject("save");
        var save = go.AddComponent<SaveGameManager>();

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
        var obj = new GameObject("save");
        var mgr = obj.AddComponent<SaveGameManager>();
        mgr.Coins = 2;
        await mgr.ChangeSlot(1);
        mgr.Coins = 5;
        FlushAndDestroy(mgr);

        var obj2 = new GameObject("save2");
        var mgr2 = obj2.AddComponent<SaveGameManager>();
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
        var obj = new GameObject("save");
        var mgr = obj.AddComponent<SlowSaveGameManager>();
        mgr.Coins = 1; // queue save for slot 0

        // Immediately switch to slot 1. ChangeSlot should block until the first
        // save finishes so the data lands in slot 0.
        await mgr.ChangeSlot(1);
        mgr.Coins = 2; // save to slot 1
        FlushAndDestroy(mgr);

        // Verify slot 0 contains the first value and slot 1 the second.
        SaveSlotManager.SetSlot(0);
        var check0 = new GameObject("check0");
        var mgr0 = check0.AddComponent<SaveGameManager>();
        Assert.AreEqual(1, mgr0.Coins, "Slot 0 should persist initial value");
        FlushAndDestroy(mgr0);

        SaveSlotManager.SetSlot(1);
        var check1 = new GameObject("check1");
        var mgr1 = check1.AddComponent<SaveGameManager>();
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
        var obj = new GameObject("save");
        var mgr = obj.AddComponent<SaveGameManager>();

        FieldInfo pathField = typeof(SaveGameManager).GetField("savePath", BindingFlags.NonPublic | BindingFlags.Instance);
        pathField.SetValue(mgr, Path.Combine(Application.dataPath, "no_such_dir", "save.json"));

        LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Failed to write save file"));

        MethodInfo method = typeof(SaveGameManager).GetMethod("SaveDataToFile", BindingFlags.NonPublic | BindingFlags.Instance);
        method.Invoke(mgr, null);

        FlushAndDestroy(mgr);
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
        var go = new GameObject("save");
        var mgr = go.AddComponent<SaveGameManager>();

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
    /// Updating multiple upgrade levels should result in a single save to disk
    /// regardless of how many entries are modified.
    /// </summary>
    [Test]
    public void UpdateUpgradeLevels_BatchesWrites()
    {
        var obj = new GameObject("save");
        var mgr = obj.AddComponent<SaveGameManagerSpy>();

        var dict = new Dictionary<UpgradeType, int>
        {
            { UpgradeType.MagnetDuration, 1 },
            { UpgradeType.SpeedBoostDuration, 2 }
        };

        mgr.UpdateUpgradeLevels(dict);

        Assert.AreEqual(1, mgr.Calls);

        FlushAndDestroy(mgr);
    }

    /// <summary>
    /// Verifies that saving does not block the main thread even if the disk is
    /// extremely slow.
    /// </summary>
    [Test]
    public void SlowDisk_DoesNotBlockMainThread()
    {
        var obj = new GameObject("save");
        var mgr = obj.AddComponent<SlowSaveGameManager>();

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
        var obj = new GameObject("save");
        var mgr = obj.AddComponent<SlowSaveGameManager>();
        mgr.Coins = 9; // queue save

        // Reflectively invoke the private handler used by Application.quitting.
        MethodInfo quit = typeof(SaveGameManager).GetMethod("HandleApplicationQuitting", BindingFlags.NonPublic | BindingFlags.Instance);
        var timer = Stopwatch.StartNew();
        quit.Invoke(mgr, null); // should return immediately
        timer.Stop();
        Assert.Less(timer.ElapsedMilliseconds, 100, "Quit handler should return quickly");

        // Allow the asynchronous save to finish so data reaches disk.
        Task.Delay(300).Wait();

        var obj2 = new GameObject("check");
        var mgr2 = obj2.AddComponent<SaveGameManager>();
        Assert.AreEqual(9, mgr2.Coins, "Data should persist after quit handler");

        FlushAndDestroy(mgr2);
        FlushAndDestroy(mgr);
    }
}
