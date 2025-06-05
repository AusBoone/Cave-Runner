using UnityEngine;

/// <summary>
/// Continuously rotates an obstacle for added challenge.
/// </summary>
public class RotatingHazard : MonoBehaviour
{
    public float rotationSpeed = 180f; // degrees per second

    void Update()
    {
        transform.Rotate(Vector3.forward, rotationSpeed * Time.deltaTime);
    }
}
