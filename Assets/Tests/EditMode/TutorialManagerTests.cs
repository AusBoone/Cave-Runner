using NUnit.Framework;
using UnityEngine;
using System.Reflection;

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
}
