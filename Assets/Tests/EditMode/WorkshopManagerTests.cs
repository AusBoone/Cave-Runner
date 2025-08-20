using NUnit.Framework;
using UnityEngine;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.TestTools; // Provides LogAssert and UnityTest support

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
