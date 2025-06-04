using UnityEngine;
using System.Collections.Generic;

// Simple object pooling component that reuses inactive objects.
public class ObjectPool : MonoBehaviour
{
    public GameObject prefab;
    public int initialSize = 5;

    private Queue<PooledObject> objects = new Queue<PooledObject>();

    void Awake()
    {
        if (prefab == null) return;
        for (int i = 0; i < initialSize; i++)
        {
            CreateNew();
        }
    }

    PooledObject CreateNew()
    {
        GameObject obj = Instantiate(prefab);
        obj.SetActive(false);
        PooledObject po = obj.AddComponent<PooledObject>();
        po.Pool = this;
        objects.Enqueue(po);
        return po;
    }

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

    public void ReturnObject(GameObject obj)
    {
        obj.SetActive(false);
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
