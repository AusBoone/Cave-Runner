// LoggingHelperTests.cs
// -----------------------------------------------------------------------------
// Validates the behavior of the centralized LoggingHelper utility. The tests
// ensure that verbose messages honor the global toggle while error logs always
// surface. Run via the Unity Test Runner in edit mode.
// -----------------------------------------------------------------------------

using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Unit tests confirming that <see cref="LoggingHelper"/> correctly routes
/// messages to Unity's console based on the configured verbosity. These tests
/// guard against regressions where verbose output might leak into production
/// builds or critical errors might be inadvertently suppressed.
/// </summary>
public class LoggingHelperTests
{
    /// <summary>
    /// Reset verbosity to on before each test so cases can individually toggle
    /// it as needed without cross-test interference.
    /// </summary>
    [SetUp]
    public void EnableVerboseByDefault()
    {
        LoggingHelper.VerboseEnabled = true;
    }

    /// <summary>
    /// When verbose logging is enabled, informational messages should appear in
    /// the Unity console. The LogAssert helper verifies that the expected entry
    /// is emitted.
    /// </summary>
    [Test]
    public void Log_EmitsMessage_WhenVerboseEnabled()
    {
        // Expect a simple log and then invoke the helper.
        LogAssert.Expect(LogType.Log, "test message");
        LoggingHelper.Log("test message");
    }

    /// <summary>
    /// Disabling the verbose flag should prevent standard logs from appearing.
    /// NoUnexpectedReceived ensures the console remains clean after invoking
    /// the helper.
    /// </summary>
    [Test]
    public void Log_DoesNotEmit_WhenVerboseDisabled()
    {
        LoggingHelper.VerboseEnabled = false;
        LoggingHelper.Log("should be silent");
        LogAssert.NoUnexpectedReceived();
    }

    /// <summary>
    /// Error logs must always surface regardless of verbosity so players and
    /// developers are alerted to critical issues.
    /// </summary>
    [Test]
    public void LogError_AlwaysEmits()
    {
        LoggingHelper.VerboseEnabled = false;
        LogAssert.Expect(LogType.Error, "important problem");
        LoggingHelper.LogError("important problem");
    }
}

