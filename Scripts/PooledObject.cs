using UnityEngine;

/// <summary>
/// Simple component used by <see cref="ObjectPool"/> to mark an instance
/// as belonging to a particular pool. Objects with this component are
/// returned to the pool instead of being destroyed.
///
/// <para>
/// In addition to tracking ownership, the component notifies its pool when
/// the GameObject is destroyed. This keeps the pool's internal instance
/// counter accurate even if clients destroy objects manually instead of
/// returning them.
/// </para>
/// </summary>
public class PooledObject : MonoBehaviour
{
    [HideInInspector]
    /// <summary>
    /// Reference back to the <see cref="ObjectPool"/> this instance
    /// belongs to. Assigned automatically when created.
    /// </summary>
    public ObjectPool Pool;

    /// <summary>
    /// When the object is destroyed directly rather than returned to the
    /// pool, inform the pool so it can decrement its internal counter and
    /// clean up any queued references.
    /// </summary>
    void OnDestroy()
    {
        // The Pool reference may be null if the component is placed in the
        // scene independently of a pool. The null-conditional operator
        // prevents a null reference exception in such cases.
        Pool?.OnPooledObjectDestroyed(this);
    }
}
