using UnityEngine;

public class ObstacleSpawner : MonoBehaviour
{
    public GameObject[] groundObstacles;
    public GameObject[] ceilingObstacles;
    public float spawnInterval = 2f;
    public float spawnX = 10f;
    public float groundY = -3.5f;
    public float ceilingY = 3.5f;
    public bool usePooling = true;

    private System.Collections.Generic.Dictionary<GameObject, ObjectPool> pools = new System.Collections.Generic.Dictionary<GameObject, ObjectPool>();

    private float timer;

    void Start()
    {
        if (usePooling)
        {
            foreach (GameObject prefab in groundObstacles)
            {
                CreatePool(prefab);
            }
            foreach (GameObject prefab in ceilingObstacles)
            {
                CreatePool(prefab);
            }
        }
    }

    void Update()
    {
        if (GameManager.Instance == null || !GameManager.Instance.IsRunning())
        {
            return;
        }
        timer -= Time.deltaTime;
        float difficulty = 1f;
        if (GameManager.Instance != null)
        {
            difficulty += GameManager.Instance.GetDistance() / 200f;
        }
        if (timer <= 0f)
        {
            Spawn();
            timer = spawnInterval / difficulty;
        }
    }

    void Spawn()
    {
        bool fromCeiling = Random.value > 0.5f;
        GameObject[] prefabs = fromCeiling ? ceilingObstacles : groundObstacles;
        if (prefabs.Length == 0) return;
        GameObject prefab = prefabs[Random.Range(0, prefabs.Length)];
        Vector3 pos = new Vector3(spawnX, fromCeiling ? ceilingY : groundY, 0f);
        if (usePooling && pools.TryGetValue(prefab, out ObjectPool pool))
        {
            pool.GetObject(pos, Quaternion.identity);
        }
        else
        {
            Instantiate(prefab, pos, Quaternion.identity);
        }
    }

    void CreatePool(GameObject prefab)
    {
        if (prefab == null || pools.ContainsKey(prefab)) return;
        GameObject obj = new GameObject(prefab.name + "_Pool");
        obj.transform.SetParent(transform);
        ObjectPool pool = obj.AddComponent<ObjectPool>();
        pool.prefab = prefab;
        pools[prefab] = pool;
    }
}
