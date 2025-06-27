using NUnit.Framework;
using UnityEngine;
using System.IO;

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
        File.Delete(Path.Combine(Application.persistentDataPath, "savegame.json"));
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
}
