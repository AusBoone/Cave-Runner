using NUnit.Framework;
using UnityEngine;
using System.Reflection;
using System.Collections.Generic;

/// <summary>
/// Unit tests covering early exit behaviour of <see cref="WorkshopManager"/>.
/// Steamworks calls are not invoked; instead the private initialization flag is
/// toggled via reflection to simulate different states.
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
#else
    [Test]
    public void WorkshopManager_IgnoredOnNonStandalone()
    {
        Assert.Pass("Steamworks not available");
    }
#endif
}
