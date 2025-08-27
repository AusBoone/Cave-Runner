using NUnit.Framework;
using UnityEngine;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.IO; // Used to create temporary directories for upload validation tests
using UnityEngine.TestTools; // Provides LogAssert and UnityTest support

/// <summary>
/// Unit tests covering early exit behaviour of <see cref="WorkshopManager"/>.
/// Steamworks calls are not invoked; instead the private initialization flag is
/// toggled via reflection to simulate different states. 2025 update: added
/// validation tests ensuring <see cref="WorkshopManager.UploadItem"/> rejects
/// invalid input values and reports failure via callback.
/// </summary>
public class WorkshopManagerTests
{
#if UNITY_STANDALONE
    /// <summary>
    /// When Steamworks is unavailable the download method should immediately
    /// return an empty list via the callback.
    /// </summary>
    [Test]
    public void DownloadSubscribedItems_NotInitialized_ReturnsEmpty()
    {
        var go = new GameObject("wm");
        var wm = go.AddComponent<WorkshopManager>();
        FieldInfo init = typeof(WorkshopManager).GetField("initialized", BindingFlags.NonPublic | BindingFlags.Instance);
        init.SetValue(wm, false);

        List<string> result = null;
        wm.DownloadSubscribedItems(paths => result = paths);

        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.Count);
        Object.DestroyImmediate(go);
    }

    /// <summary>
    /// UploadItem should report failure through the callback when Steamworks
    /// has not been initialized.
    /// </summary>
    [Test]
    public void UploadItem_NotInitialized_ReturnsFalse()
    {
        var go = new GameObject("wm");
        var wm = go.AddComponent<WorkshopManager>();
        FieldInfo init = typeof(WorkshopManager).GetField("initialized", BindingFlags.NonPublic | BindingFlags.Instance);
        init.SetValue(wm, false);

        bool success = true;
        wm.UploadItem("folder", "preview.png", "title", "desc", s => success = s);

        Assert.IsFalse(success);
        Object.DestroyImmediate(go);
    }

    /// <summary>
    /// UploadItem should immediately fail when provided a null or empty folder
    /// path, as Steam requires a valid directory to publish content from.
    /// </summary>
    [TestCase(null)]
    [TestCase("")]
    public void UploadItem_InvalidFolderPath_ReturnsFalse(string badPath)
    {
        var go = new GameObject("wm");
        var wm = go.AddComponent<WorkshopManager>();
        FieldInfo init = typeof(WorkshopManager).GetField("initialized", BindingFlags.NonPublic | BindingFlags.Instance);
        init.SetValue(wm, true); // Force initialization so validation runs.

        bool success = true;
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("folderPath"));
        wm.UploadItem(badPath, "preview.png", "title", "desc", s => success = s);

        Assert.IsFalse(success);
        Object.DestroyImmediate(go);
    }

    /// <summary>
    /// A non-existent directory should also trigger a failure callback because
    /// Steam cannot upload missing content.
    /// </summary>
    [Test]
    public void UploadItem_NonexistentFolder_ReturnsFalse()
    {
        var go = new GameObject("wm");
        var wm = go.AddComponent<WorkshopManager>();
        FieldInfo init = typeof(WorkshopManager).GetField("initialized", BindingFlags.NonPublic | BindingFlags.Instance);
        init.SetValue(wm, true);

        string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()); // Path that does not exist
        bool success = true;
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("folderPath"));
        wm.UploadItem(path, "preview.png", "title", "desc", s => success = s);

        Assert.IsFalse(success);
        Object.DestroyImmediate(go);
    }

    /// <summary>
    /// Missing or empty titles should result in immediate failure so creators
    /// know the item needs a proper name before upload.
    /// </summary>
    [TestCase(null)]
    [TestCase("")]
    public void UploadItem_InvalidTitle_ReturnsFalse(string badTitle)
    {
        var go = new GameObject("wm");
        var wm = go.AddComponent<WorkshopManager>();
        FieldInfo init = typeof(WorkshopManager).GetField("initialized", BindingFlags.NonPublic | BindingFlags.Instance);
        init.SetValue(wm, true);

        string dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName())).FullName;
        bool success = true;
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("title"));
        wm.UploadItem(dir, "preview.png", badTitle, "desc", s => success = s);

        Assert.IsFalse(success);
        Directory.Delete(dir);
        Object.DestroyImmediate(go);
    }

    /// <summary>
    /// Missing or empty descriptions are also invalid and should fail fast to
    /// prevent accidental uploads with no summary text.
    /// </summary>
    [TestCase(null)]
    [TestCase("")]
    public void UploadItem_InvalidDescription_ReturnsFalse(string badDesc)
    {
        var go = new GameObject("wm");
        var wm = go.AddComponent<WorkshopManager>();
        FieldInfo init = typeof(WorkshopManager).GetField("initialized", BindingFlags.NonPublic | BindingFlags.Instance);
        init.SetValue(wm, true);

        string dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName())).FullName;
        bool success = true;
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("description"));
        wm.UploadItem(dir, "preview.png", "title", badDesc, s => success = s);

        Assert.IsFalse(success);
        Directory.Delete(dir);
        Object.DestroyImmediate(go);
    }

    /// <summary>
    /// The singleton instance should be assigned on Awake and cleared when the
    /// component is destroyed.
    /// </summary>
    [Test]
    public void SingletonLifecycle_AwakeAndDestroy()
    {
        var go = new GameObject("wm");
       var wm = go.AddComponent<WorkshopManager>();
        Assert.AreEqual(wm, WorkshopManager.Instance);
        Object.DestroyImmediate(go);
        Assert.IsNull(WorkshopManager.Instance);
    }

    /// <summary>
    /// Loading a workshop asset should toggle the network spinner so players
    /// know that online content is being fetched.
    /// </summary>
    [UnityTest]
    public IEnumerator LoadAddressableRoutine_TogglesNetworkSpinner()
    {
        // Prepare UI manager and spinner object.
        var uiObj = new GameObject("ui");
        var ui = uiObj.AddComponent<UIManager>();
        ui.networkSpinner = new GameObject("spinner");

        var go = new GameObject("wm");
        var wm = go.AddComponent<WorkshopManager>();

        // Use reflection to invoke the private coroutine with an invalid key.
        MethodInfo method = typeof(WorkshopManager).GetMethod("LoadAddressableRoutine", BindingFlags.NonPublic | BindingFlags.Instance);
        MethodInfo generic = method.MakeGenericMethod(typeof(GameObject));

        // Invalid address triggers an error log; capture it to keep test quiet.
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("(Failed|Exception)"));

        var routine = (IEnumerator)generic.Invoke(wm, new object[] { "00000000000000000000000000000000", null });

        Assert.IsFalse(ui.networkSpinner.activeSelf);
        Assert.IsTrue(routine.MoveNext());
        Assert.IsTrue(ui.networkSpinner.activeSelf);

        while (routine.MoveNext())
        {
            yield return routine.Current;
        }

        Assert.IsFalse(ui.networkSpinner.activeSelf);

        Object.DestroyImmediate(go);
        Object.DestroyImmediate(ui.networkSpinner);
        Object.DestroyImmediate(uiObj);
    }
#else
    [Test]
    public void WorkshopManager_IgnoredOnNonStandalone()
    {
        Assert.Pass("Steamworks not available");
    }
#endif
}
