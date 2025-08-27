// UIManagerAsyncTests.cs
// -----------------------------------------------------------------------------
// Validates the asynchronous loading behavior introduced in UIManager for the
// MobileUI prefab. These tests ensure that the coroutine-based loading pattern
// correctly instantiates the touch-friendly canvas when available and provides
// a clear warning when the prefab cannot be located.
// -----------------------------------------------------------------------------

using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// EditMode tests exercising the asynchronous mobile canvas loading in
/// <see cref="UIManager"/>.
/// </summary>
public class UIManagerAsyncTests
{
    /// <summary>
    /// Ensures that the coroutine loads the MobileUI prefab and assigns the
    /// resulting canvas to the private field when the asset exists.
    /// </summary>
    [UnityTest]
    public IEnumerator LoadMobileCanvasAsync_InstantiatesPrefab()
    {
        var uiObj = new GameObject("ui");
        var ui = uiObj.AddComponent<UIManager>();

        // Use reflection to obtain the private coroutine responsible for
        // loading the mobile canvas. Invoking it directly lets the test run
        // even though Application.isMobilePlatform is false in the editor.
        var method = typeof(UIManager).GetMethod(
            "LoadMobileCanvasAsync",
            BindingFlags.NonPublic | BindingFlags.Instance);
        var enumerator = (IEnumerator)method.Invoke(ui, null);

        // Execute the coroutine to completion so the asset has time to load.
        while (enumerator.MoveNext())
        {
            yield return enumerator.Current;
        }

        var canvas = (GameObject)typeof(UIManager)
            .GetField("mobileCanvas", BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(ui);
        Assert.IsNotNull(canvas, "Coroutine should instantiate the MobileUI prefab.");

        Object.DestroyImmediate(canvas);
        Object.DestroyImmediate(uiObj);
    }

    /// <summary>
    /// Verifies that attempting to load a missing prefab logs a warning and
    /// leaves the mobileCanvas field unset so the game can continue gracefully.
    /// </summary>
    [UnityTest]
    public IEnumerator LoadMobileCanvasAsync_LogsWarningOnMissingPrefab()
    {
        var uiObj = new GameObject("ui");
        var ui = uiObj.AddComponent<UIManager>();

        // Ensure warnings are emitted even if verbose logging is disabled in
        // future release configurations.
        LoggingHelper.VerboseEnabled = true;
        LogAssert.Expect(LogType.Warning, "UI/DoesNotExist prefab not found in Resources");

        var method = typeof(UIManager).GetMethod(
            "LoadMobileCanvasAsync",
            BindingFlags.NonPublic | BindingFlags.Instance);
        var enumerator = (IEnumerator)method.Invoke(ui, new object[] { "UI/DoesNotExist" });

        while (enumerator.MoveNext())
        {
            yield return enumerator.Current;
        }

        var canvas = (GameObject)typeof(UIManager)
            .GetField("mobileCanvas", BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(ui);
        Assert.IsNull(canvas, "mobileCanvas should remain null when the prefab is missing.");

        Object.DestroyImmediate(uiObj);
    }
}

