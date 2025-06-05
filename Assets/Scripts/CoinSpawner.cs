using UnityEngine;

public class CoinSpawner : MonoBehaviour
{
    public GameObject[] coinPrefabs;
    public float spawnInterval = 1.5f;
    // Curve to control how coin spawn rate scales with distance.
    public AnimationCurve spawnRateCurve = AnimationCurve.Linear(0f, 1f, 100f, 1f);
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
        float difficulty = 1f;
        if (GameManager.Instance != null)
        {
            difficulty = Mathf.Max(0.1f, spawnRateCurve.Evaluate(GameManager.Instance.GetDistance()));
        }
        if (timer <= 0f)
        {
            SpawnCoin();
            timer = spawnInterval / difficulty;
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
