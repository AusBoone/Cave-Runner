using UnityEngine;

/// <summary>
/// Simple component used by <see cref="ObjectPool"/> to mark an instance
/// as belonging to a particular pool. Objects with this component are
/// returned to the pool instead of being destroyed.
/// </summary>
public class PooledObject : MonoBehaviour
{
    [HideInInspector]
    /// <summary>
    /// Reference back to the <see cref="ObjectPool"/> this instance
    /// belongs to. Assigned automatically when created.
    /// </summary>
    public ObjectPool Pool;
}
