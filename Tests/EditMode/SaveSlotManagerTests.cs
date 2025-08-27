using NUnit.Framework;
using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Unit tests for <see cref="SaveSlotManager"/> verifying file name validation,
/// directory creation behaviour, and fallback logic when the slot directory
/// cannot be created.
/// </summary>
public class SaveSlotManagerTests
{
    [SetUp]
    public void CleanUp()
    {
        // Reset persistent data and PlayerPrefs to avoid test cross-contamination.
        PlayerPrefs.DeleteAll();
        for (int i = 0; i < SaveSlotManager.MaxSlots; i++)
        {
            string dir = Path.Combine(Application.persistentDataPath, $"slot_{i}");
            if (File.Exists(dir))
            {
                // If a file exists where a directory should be, remove it.
                File.Delete(dir);
            }
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
            }
        }
    }

    [Test]
    public void GetPath_ValidFileName_ReturnsSlotPath()
    {
        // The returned path should include the slot directory when provided a
        // simple file name.
        string result = SaveSlotManager.GetPath("save.json");
        string expected = Path.Combine(Application.persistentDataPath,
            $"slot_{SaveSlotManager.CurrentSlot}", "save.json");
        Assert.AreEqual(expected, result);
    }

    [Test]
    public void GetPath_InvalidFileName_Throws()
    {
        // Path segments should be rejected to avoid traversal attacks.
        Assert.Throws<ArgumentException>(() => SaveSlotManager.GetPath("../save.json"));
        // Empty or whitespace names are similarly invalid.
        Assert.Throws<ArgumentException>(() => SaveSlotManager.GetPath("   "));
    }

    [Test]
    public void GetPath_DirectoryCreationFails_ReturnsFallback()
    {
        // Create a file where the slot directory should be so CreateDirectory
        // throws an IOException, forcing the method to fall back.
        string slotPath = Path.Combine(Application.persistentDataPath,
            $"slot_{SaveSlotManager.CurrentSlot}");
        File.WriteAllText(slotPath, "dummy");

        try
        {
            string result = SaveSlotManager.GetPath("save.json");
            // Without a directory the fallback should be the root persistent path.
            string expected = Path.Combine(Application.persistentDataPath, "save.json");
            Assert.AreEqual(expected, result);
        }
        finally
        {
            // Ensure the temporary file is removed even if the assertion fails.
            File.Delete(slotPath);
        }
    }
}
