using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;

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

    [Test]
    public void SaveAndLoad_PersistsData()
    {
        var go = new GameObject("save");
        var save = go.AddComponent<SaveGameManager>();
        save.Coins = 7;
        save.HighScore = 12;
        save.SetUpgradeLevel(UpgradeType.MagnetDuration, 2);
        Object.DestroyImmediate(go);

        // Create a new instance which should load from the file
        var go2 = new GameObject("save2");
        var save2 = go2.AddComponent<SaveGameManager>();

        Assert.AreEqual(7, save2.Coins);
        Assert.AreEqual(12, save2.HighScore);
        Assert.AreEqual(2, save2.GetUpgradeLevel(UpgradeType.MagnetDuration));
        Object.DestroyImmediate(go2);
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
        Object.DestroyImmediate(go);
    }

    [Test]
    public void Save_UsesTemporaryFile()
    {
        // Saving should not leave the temporary file used for atomic writes.
        var go = new GameObject("save");
        var save = go.AddComponent<SaveGameManager>();

        save.Coins = 1; // triggers SaveDataToFile

        string temp = Path.Combine(Application.persistentDataPath, "savegame.json.tmp");
        Assert.IsFalse(File.Exists(temp));
        Object.DestroyImmediate(go);
    }

    [Test]
    public void VolumeValues_ArePersisted()
    {
        var obj = new GameObject("save");
        var save = obj.AddComponent<SaveGameManager>();
        save.MusicVolume = 0.4f;
        save.EffectsVolume = 0.7f;
        Object.DestroyImmediate(obj);

        var obj2 = new GameObject("save2");
        var save2 = obj2.AddComponent<SaveGameManager>();
        Assert.AreEqual(0.4f, save2.MusicVolume, 0.001f);
        Assert.AreEqual(0.7f, save2.EffectsVolume, 0.001f);
        Object.DestroyImmediate(obj2);
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
        Object.DestroyImmediate(go);
    }

    [Test]
    public void VersionField_WrittenToFile()
    {
        var go = new GameObject("save");
        go.AddComponent<SaveGameManager>();
        Object.DestroyImmediate(go);

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
        Object.DestroyImmediate(go);

        var go2 = new GameObject("save2");
        var save2 = go2.AddComponent<SaveGameManager>();
        Assert.AreEqual("es", save2.Language);
        Object.DestroyImmediate(go2);
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

        Object.DestroyImmediate(go);
    }

    [Test]
    public void ChangeSlot_SwitchesSavePath()
    {
        // Write data to the initial slot then change to a new slot and verify
        // values persist separately.
        SaveSlotManager.SetSlot(0);
        var obj = new GameObject("save");
        var mgr = obj.AddComponent<SaveGameManager>();
        mgr.Coins = 2;
        mgr.ChangeSlot(1);
        mgr.Coins = 5;
        Object.DestroyImmediate(obj);

        var obj2 = new GameObject("save2");
        var mgr2 = obj2.AddComponent<SaveGameManager>();
        Assert.AreEqual(5, mgr2.Coins, "Slot 1 should contain updated value");
        mgr2.ChangeSlot(0);
        Assert.AreEqual(2, mgr2.Coins, "Slot 0 should retain original value");
        Object.DestroyImmediate(obj2);
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

        Object.DestroyImmediate(obj);
    }
}
