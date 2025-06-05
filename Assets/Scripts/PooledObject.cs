using UnityEngine;

/// <summary>
/// Simple component used by <see cref="ObjectPool"/> to mark an instance
/// as belonging to a particular pool. Objects with this component are
/// returned to the pool instead of being destroyed.
/// </summary>
public class PooledObject : MonoBehaviour
{
    [HideInInspector]
    public ObjectPool Pool;
}
