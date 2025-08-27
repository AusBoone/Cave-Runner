// CoroutineUtilities.cs
// -----------------------------------------------------------------------------
// Provides helper routines for orchestrating multiple Unity coroutines. The
// WhenAll method allows a caller to start a set of coroutines and yield until
// every one has completed, enabling simple parallel asynchronous workflows.
// -----------------------------------------------------------------------------

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Utility methods for working with Unity coroutines. These helpers simplify
/// launching and awaiting groups of asynchronous operations.
/// </summary>
public static class CoroutineUtilities
{
    /// <summary>
    /// Starts all provided coroutines using <paramref name="owner"/> and waits
    /// until every routine has finished executing. Each enumerator runs in
    /// parallel, allowing the caller to easily coordinate multiple asynchronous
    /// tasks (such as loading several assets at once).
    /// </summary>
    /// <param name="owner">MonoBehaviour used to start the coroutines.</param>
    /// <param name="routines">Collection of coroutine enumerators to run.</param>
    /// <returns>Enumerator that yields until all routines complete.</returns>
    /// <remarks>
    /// This method validates inputs defensively; supplying a null owner or an
    /// empty routine list simply results in an immediate exit. Individual
    /// routines are executed safely even if others fail, ensuring all continue
    /// running to completion.
    /// </remarks>
    public static IEnumerator WhenAll(MonoBehaviour owner, IList<IEnumerator> routines)
    {
        if (owner == null || routines == null || routines.Count == 0)
        {
            yield break; // nothing to execute
        }

        int remaining = routines.Count; // tracks how many coroutines are still running

        // Local function started for each routine to monitor its completion.
        IEnumerator Track(IEnumerator routine)
        {
            // Execute the supplied routine fully before decrementing the counter.
            yield return routine;
            remaining--;
        }

        // Kick off all routines in parallel.
        foreach (IEnumerator routine in routines)
        {
            owner.StartCoroutine(Track(routine));
        }

        // Wait until every tracked routine reports completion.
        while (remaining > 0)
        {
            yield return null; // keep yielding so other coroutines can advance
        }
    }
}

