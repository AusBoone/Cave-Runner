using UnityEngine;

/// <summary>
/// Smoothly follows a target transform using Vector3.SmoothDamp. The
/// camera maintains the specified offset relative to the target.
/// </summary>
public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public float smoothTime = 0.3f;
    public Vector3 offset = new Vector3(0f, 0f, -10f);

    private Vector3 velocity = Vector3.zero;

    /// <summary>
    /// Called after Update so camera movement occurs after the player
    /// has moved. Smoothly interpolates toward the target position.
    /// </summary>
    void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        Vector3 targetPosition = target.position + offset;
        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref velocity, smoothTime);
    }
}
