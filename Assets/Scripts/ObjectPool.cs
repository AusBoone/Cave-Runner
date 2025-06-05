using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Simple object pooling component that reuses inactive instances of a
/// prefab. Pools help avoid expensive Instantiate/Destroy calls during
/// gameplay.
/// </summary>
public class ObjectPool : MonoBehaviour
{
    public GameObject prefab;
    public int initialSize = 5;

    private Queue<PooledObject> objects = new Queue<PooledObject>();

    // Delay initialization until Start so prefab can be assigned by spawners
    // before the pool creates its initial objects.
    /// <summary>
    /// Instantiates a set number of objects at start so the pool has
    /// instances ready for immediate use.
    /// </summary>
    void Start()
    {
        if (prefab == null) return;
        for (int i = 0; i < initialSize; i++)
        {
            CreateNew();
        }
    }

    /// <summary>
    /// Creates a new pooled instance of the prefab and adds it to the
    /// internal queue.
    /// </summary>
    PooledObject CreateNew()
    {
        // Instantiate a new object and parent it under the pool so the
        // hierarchy stays clean in the editor.
        GameObject obj = Instantiate(prefab, transform);
        obj.SetActive(false);
        PooledObject po = obj.AddComponent<PooledObject>();
        po.Pool = this;
        objects.Enqueue(po);
        return po;
    }

    /// <summary>
    /// Retrieves an instance from the pool, expanding it if necessary.
    /// The object is positioned, activated and returned.
    /// </summary>
    public GameObject GetObject(Vector3 position, Quaternion rotation)
    {
        if (prefab == null) return null;
        if (objects.Count == 0)
        {
            CreateNew();
        }
        PooledObject po = objects.Dequeue();
        GameObject obj = po.gameObject;
        obj.transform.SetPositionAndRotation(position, rotation);
        obj.SetActive(true);
        return obj;
    }

    /// <summary>
    /// Deactivates an object and puts it back into the pool for later
    /// reuse.
    /// </summary>
    public void ReturnObject(GameObject obj)
    {
        // Disable the object and parent it back under this pool so
        // inactive instances remain organized.
        obj.SetActive(false);
        obj.transform.SetParent(transform);
        PooledObject po = obj.GetComponent<PooledObject>();
        if (po != null)
        {
            objects.Enqueue(po);
        }
        else
        {
            Destroy(obj);
        }
    }
}
