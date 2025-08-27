using NUnit.Framework;
using UnityEngine;
using System.Reflection;
using System.IO;

/// <summary>
/// EditMode tests for <see cref="TutorialManager"/> verifying that the tutorial
/// displays on the first run, progresses through panels, and is skipped on
/// subsequent runs after completion.
/// </summary>
public class TutorialManagerTests
{
    [SetUp]
    public void ClearPrefsAndTime()
    {
        // Ensure each test starts with a clean PlayerPrefs state and normal time.
        PlayerPrefs.DeleteAll();
        Time.timeScale = 1f;
        for (int i = 0; i < SaveSlotManager.MaxSlots; i++)
        {
            string dir = Path.Combine(Application.persistentDataPath, $"slot_{i}");
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
            }
        }
    }

    [Test]
    public void Start_FirstRun_ShowsFirstPanel()
    {
        // Create manager with two tutorial panels.
        var obj = new GameObject("tm");
        var tm = obj.AddComponent<TutorialManager>();
        var p1 = new GameObject("p1");
        var p2 = new GameObject("p2");
        p1.SetActive(false);
        p2.SetActive(false);
        tm.tutorialPanels = new[] { p1, p2 };

        // Invoke the private Start method just like Unity would.
        typeof(TutorialManager).GetMethod("Start", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(tm, null);

        // The tutorial should pause time and display only the first panel.
        Assert.AreEqual(0f, Time.timeScale, 0.001f);
        Assert.IsTrue(p1.activeSelf);
        Assert.IsFalse(p2.activeSelf);

        Object.DestroyImmediate(obj);
        Object.DestroyImmediate(p1);
        Object.DestroyImmediate(p2);
    }

    [Test]
    public void Next_AdvancesAndCompletesTutorial()
    {
        // Begin tutorial manually so Next() can be exercised directly.
        var obj = new GameObject("tm");
        var tm = obj.AddComponent<TutorialManager>();
        var p1 = new GameObject("p1");
        var p2 = new GameObject("p2");
        tm.tutorialPanels = new[] { p1, p2 };

        tm.BeginTutorial();
        tm.Next();

        // After the first Next() call the second panel should show.
        Assert.IsFalse(p1.activeSelf);
        Assert.IsTrue(p2.activeSelf);

        tm.Next();

        // Final Next() ends the tutorial, saving completion and deactivating the object.
        Assert.AreEqual(1, PlayerPrefs.GetInt("TutorialSeen"));
        Assert.AreEqual(1f, Time.timeScale, 0.001f);
        Assert.IsFalse(obj.activeSelf);

        Object.DestroyImmediate(obj);
        Object.DestroyImmediate(p1);
        Object.DestroyImmediate(p2);
    }

    [Test]
    public void Start_WhenTutorialSeen_SkipsImmediately()
    {
        // Simulate a prior completion flag stored in PlayerPrefs.
        PlayerPrefs.SetInt("TutorialSeen", 1);
        PlayerPrefs.Save();

        var obj = new GameObject("tm");
        var tm = obj.AddComponent<TutorialManager>();
        var panel = new GameObject("panel");
        tm.tutorialPanels = new[] { panel };

        typeof(TutorialManager).GetMethod("Start", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(tm, null);

        // Manager should disable itself and never show the panel.
        Assert.IsFalse(obj.activeSelf);
        Assert.IsFalse(panel.activeSelf);
        Assert.AreEqual(1f, Time.timeScale, 0.001f);

        Object.DestroyImmediate(obj);
        Object.DestroyImmediate(panel);
    }

    [Test]
    public void Tutorial_TriggersPerSaveSlot()
    {
        // Slot 0 should show the tutorial the first time.
        SaveSlotManager.SetSlot(0);
        var saveObj = new GameObject("save0");
        saveObj.AddComponent<SaveGameManager>();

        var obj = new GameObject("tm");
        var tm = obj.AddComponent<TutorialManager>();
        var panel = new GameObject("panel");
        tm.tutorialPanels = new[] { panel };

        typeof(TutorialManager).GetMethod("Start", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(tm, null);
        Assert.IsTrue(panel.activeSelf);
        tm.Next();
        Object.DestroyImmediate(obj);
        Object.DestroyImmediate(panel);
        Object.DestroyImmediate(saveObj);

        // Same slot should skip on next load.
        SaveSlotManager.SetSlot(0);
        var saveObj2 = new GameObject("save1");
        saveObj2.AddComponent<SaveGameManager>();
        var obj2 = new GameObject("tm2");
        var tm2 = obj2.AddComponent<TutorialManager>();
        var panel2 = new GameObject("panel2");
        tm2.tutorialPanels = new[] { panel2 };
        typeof(TutorialManager).GetMethod("Start", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(tm2, null);
        Assert.IsFalse(obj2.activeSelf);
        Object.DestroyImmediate(obj2);
        Object.DestroyImmediate(panel2);
        Object.DestroyImmediate(saveObj2);

        // Slot 1 is a fresh profile so tutorial should show again.
        SaveSlotManager.SetSlot(1);
        var saveObj3 = new GameObject("save2");
        saveObj3.AddComponent<SaveGameManager>();
        var obj3 = new GameObject("tm3");
        var tm3 = obj3.AddComponent<TutorialManager>();
        var panel3 = new GameObject("panel3");
        tm3.tutorialPanels = new[] { panel3 };
        typeof(TutorialManager).GetMethod("Start", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(tm3, null);
        Assert.IsTrue(panel3.activeSelf);
        Object.DestroyImmediate(obj3);
        Object.DestroyImmediate(panel3);
        Object.DestroyImmediate(saveObj3);
    }

    [Test]
    public void ContextTips_ShowOnlyOnce()
    {
        var saveObj = new GameObject("save");
        saveObj.AddComponent<SaveGameManager>();

        var obj = new GameObject("tm");
        var tm = obj.AddComponent<TutorialManager>();
        var jump = new GameObject("jump");
        var slide = new GameObject("slide");
        jump.SetActive(false);
        slide.SetActive(false);
        tm.jumpTipPanel = jump;
        tm.slideTipPanel = slide;

        tm.RegisterJump();
        Assert.IsTrue(jump.activeSelf);
        tm.CloseTip(jump);
        tm.RegisterJump();
        Assert.IsFalse(jump.activeSelf);

        tm.RegisterSlide();
        Assert.IsTrue(slide.activeSelf);
        tm.CloseTip(slide);
        tm.RegisterSlide();
        Assert.IsFalse(slide.activeSelf);

        Object.DestroyImmediate(obj);
        Object.DestroyImmediate(jump);
        Object.DestroyImmediate(slide);
        Object.DestroyImmediate(saveObj);
    }
}
