using UnityEngine;

/// <summary>
/// Simple camera shake effect used for feedback when the coin combo increases.
/// Attach this component to the main camera and invoke <see cref="Shake"/> to
/// apply a temporary randomized displacement.
/// </summary>
public class CameraShake : MonoBehaviour
{
    private Vector3 originalPos;     // cached starting position
    private float shakeTimer;        // remaining time to shake
    private float shakeMagnitude;    // intensity of the shake

    void Awake()
    {
        originalPos = transform.localPosition;
    }

    /// <summary>
    /// Begins shaking the camera for <paramref name="duration"/> seconds using
    /// the provided <paramref name="magnitude"/>. Subsequent calls will
    /// restart the timer using the new values.
    /// </summary>
    public void Shake(float duration, float magnitude)
    {
        shakeTimer = duration;
        shakeMagnitude = magnitude;
    }

    void LateUpdate()
    {
        if (shakeTimer > 0f)
        {
            // Displace camera by a random offset each frame
            transform.localPosition = originalPos + Random.insideUnitSphere * shakeMagnitude;
            shakeTimer -= Time.deltaTime;
            if (shakeTimer <= 0f)
            {
                transform.localPosition = originalPos;
            }
        }
    }
}
