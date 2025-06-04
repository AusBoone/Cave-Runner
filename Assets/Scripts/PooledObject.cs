using UnityEngine;

// Attached to objects managed by ObjectPool so they can be returned
// instead of destroyed when they leave the screen.
public class PooledObject : MonoBehaviour
{
    [HideInInspector]
    public ObjectPool Pool;
}
