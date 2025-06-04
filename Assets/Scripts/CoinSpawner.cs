using UnityEngine;

public class CoinSpawner : MonoBehaviour
{
    public GameObject[] coinPrefabs;
    public float spawnInterval = 1.5f;
    public float spawnX = 10f;
    public float minY = -3f;
    public float maxY = 3f;
    public bool usePooling = true;

    private System.Collections.Generic.Dictionary<GameObject, ObjectPool> pools = new System.Collections.Generic.Dictionary<GameObject, ObjectPool>();

    private float timer;

    void Start()
    {
        if (usePooling)
        {
            foreach (GameObject prefab in coinPrefabs)
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
        if (timer <= 0f)
        {
            SpawnCoin();
            timer = spawnInterval;
        }
    }

    void SpawnCoin()
    {
        if (coinPrefabs.Length == 0) return;
        GameObject prefab = coinPrefabs[Random.Range(0, coinPrefabs.Length)];
        Vector3 pos = new Vector3(spawnX, Random.Range(minY, maxY), 0f);
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
