using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Collections;

/// <summary>
/// Tests for <see cref="CameraShake"/> verifying that invoking
/// <see cref="CameraShake.Shake"/> temporarily displaces the camera and then
/// restores it to its original position once the effect duration elapses.
/// </summary>
public class CameraShakeTests
{
    /// <summary>
    /// Confirms the shake effect modifies <see cref="Transform.localPosition"/>
    /// while active and resets back to the cached starting position afterward.
    /// </summary>
    [UnityTest]
    public IEnumerator Shake_DisplacesAndResets()
    {
        // Prepare a camera object with the shake component.
        var cam = new GameObject("camera");
        var shake = cam.AddComponent<CameraShake>();
        Vector3 start = cam.transform.localPosition; // cache initial position

        // Begin shaking for a brief moment.
        shake.Shake(0.05f, 1f);

        // After one frame the camera should have moved away from the start.
        yield return null;
        Assert.AreNotEqual(start, cam.transform.localPosition,
            "Camera did not move during shake");

        // Wait until the shake duration expires; position should reset.
        yield return new WaitForSeconds(0.06f);
        Assert.That(cam.transform.localPosition, Is.EqualTo(start),
            "Camera failed to reset to original position after shake");

        Object.DestroyImmediate(cam);
    }
}

