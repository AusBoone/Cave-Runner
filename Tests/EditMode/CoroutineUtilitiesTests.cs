using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Tests exercising <see cref="CoroutineUtilities"/> helper methods. The
/// focus is verifying that <see cref="CoroutineUtilities.WhenAll"/> properly
/// awaits multiple coroutines and continues execution even if one routine
/// raises an exception.
/// </summary>
public class CoroutineUtilitiesTests
{
    /// <summary>
    /// Simple behaviour used solely to own started coroutines within the tests.
    /// </summary>
    private class Runner : MonoBehaviour { }

    /// <summary>
    /// Validates that <see cref="CoroutineUtilities.WhenAll"/> completes once
    /// every supplied coroutine finishes and that exceptions thrown by one do
    /// not prevent the others from running to completion.
    /// </summary>
    [UnityTest]
    public IEnumerator WhenAll_CompletesDespiteExceptions()
    {
        var obj = new GameObject("runner");
        var runner = obj.AddComponent<Runner>();

        bool aDone = false, bDone = false, cDone = false;

        // Short routine that completes after a single frame.
        IEnumerator A()
        {
            yield return null;
            aDone = true;
        }

        // Routine that throws after a frame to test exception handling.
        IEnumerator B()
        {
            yield return null;
            bDone = true;
            throw new System.Exception("boom");
        }

        // Another successful routine for good measure.
        IEnumerator C()
        {
            yield return null;
            cDone = true;
        }

        var routines = new List<IEnumerator> { A(), B(), C() };

        // Execute WhenAll and wait for completion.
        yield return CoroutineUtilities.WhenAll(runner, routines);

        // All routines should have run to completion despite one throwing.
        Assert.IsTrue(aDone && bDone && cDone,
            "Not all coroutines finished execution");

        Object.DestroyImmediate(obj);
    }
}

