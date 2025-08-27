// LoggingHelper.cs
// -----------------------------------------------------------------------------
// Centralized wrapper around Unity's Debug class. The helper gates verbose
// logging behind a single toggle so development builds can emit rich trace
// information while production builds remain silent. Error logging always
// occurs so critical issues surface during play.
// -----------------------------------------------------------------------------

using UnityEngine;

/// <summary>
/// Provides a single point of control for logging throughout the project.
/// Verbose output can be disabled globally for release builds by clearing
/// <see cref="VerboseEnabled"/>. Error messages always log regardless of the
/// flag. Usage example:
/// <code>
/// LoggingHelper.Log("Loaded profile");
/// LoggingHelper.LogWarning("Missing texture");
/// LoggingHelper.LogError("Save failed");
/// </code>
/// </summary>
public static class LoggingHelper
{
    /// <summary>
    /// Indicates whether standard and warning logs should be emitted. Defaults
    /// to true inside the Unity Editor so developers automatically see messages
    /// during iteration, but can be toggled off at runtime by tests or build
    /// scripts. Release builds initialize this to false to avoid console noise.
    /// </summary>
    public static bool VerboseEnabled =
#if UNITY_EDITOR
        true;
#else
        false;
#endif

    /// <summary>
    /// Emits an informational message when <see cref="VerboseEnabled"/> is true.
    /// No exception is thrown if the message is null; nothing logs instead.
    /// </summary>
    /// <param name="message">Content to send to the Unity console.</param>
    public static void Log(string message)
    {
        // Guard against unnecessary string formatting when logs are disabled.
        if (VerboseEnabled && message != null)
        {
            Debug.Log(message);
        }
    }

    /// <summary>
    /// Emits a warning message when <see cref="VerboseEnabled"/> is true.
    /// </summary>
    /// <param name="message">Content to send to the Unity console.</param>
    public static void LogWarning(string message)
    {
        // Similar check to <see cref="Log"/> to avoid overhead in release.
        if (VerboseEnabled && message != null)
        {
            Debug.LogWarning(message);
        }
    }

    /// <summary>
    /// Emits an error message regardless of the verbose flag. Errors are always
    /// important for diagnosing issues so they are never suppressed.
    /// </summary>
    /// <param name="message">Content to send to the Unity console.</param>
    public static void LogError(string message)
    {
        if (message != null)
        {
            Debug.LogError(message);
        }
    }
}

