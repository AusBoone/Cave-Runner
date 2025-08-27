using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Collections;

/// <summary>
/// Unit tests for <see cref="CameraFollow"/> ensuring the component smoothly
/// tracks its target and handles missing targets gracefully. These tests
/// simulate a few frames of execution by yielding control back to Unity so
/// <c>LateUpdate</c> executes just as it would during play mode.
/// </summary>
public class CameraFollowTests
{
    /// <summary>
    /// Verifies that the camera's transform moves toward the target over
    /// successive frames. A small <see cref="CameraFollow.smoothTime"/> value is
    /// used so the camera converges rapidly during the test.
    /// </summary>
    [UnityTest]
    public IEnumerator LateUpdate_MovesTowardTarget()
    {
        // Create a target object positioned five units along the X axis.
        var target = new GameObject("target").transform;
        target.position = new Vector3(5f, 0f, 0f);

        // Attach CameraFollow to a new camera at the origin.
        var cam = new GameObject("camera");
        var follow = cam.AddComponent<CameraFollow>();
        follow.target = target;
        follow.smoothTime = 0.01f; // minimal smoothing for a quick test
        follow.offset = Vector3.zero; // keep positions equal for simpler asserts

        // Allow several frames for LateUpdate to run and move the camera.
        for (int i = 0; i < 10; i++)
        {
            yield return null; // wait a frame so CameraFollow executes
        }

        // The camera should now be extremely close to the target position.
        Assert.That(cam.transform.position.x, Is.GreaterThan(4.9f),
            "Camera should have approached the target along X");

        Object.DestroyImmediate(cam);
        Object.DestroyImmediate(target.gameObject);
    }

    /// <summary>
    /// Ensures the component exits early when no target is assigned. The
    /// camera's position should remain unchanged after an update cycle.
    /// </summary>
    [UnityTest]
    public IEnumerator LateUpdate_NoTarget_NoMovement()
    {
        var cam = new GameObject("camera");
        cam.transform.position = new Vector3(1f, 2f, 3f); // arbitrary start
        var follow = cam.AddComponent<CameraFollow>();
        follow.target = null; // explicitly leave target unset
        follow.offset = Vector3.zero;
        follow.smoothTime = 0.01f;

        Vector3 before = cam.transform.position;
        yield return null; // run a frame; LateUpdate should early-out
        Assert.That(cam.transform.position, Is.EqualTo(before),
            "Camera moved despite missing target");

        Object.DestroyImmediate(cam);
    }
}

