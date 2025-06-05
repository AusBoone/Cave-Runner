using UnityEngine;

/// <summary>
/// Periodically spawns ground or ceiling obstacles that the player must
/// avoid. Spawn timing accelerates as the run progresses and objects can
/// be pooled for efficiency.
/// </summary>
public class ObstacleSpawner : MonoBehaviour
{
    public GameObject[] groundObstacles;
    public GameObject[] ceilingObstacles;
    public float spawnInterval = 2f;
    // Curve representing how spawn rate increases with distance.
    public AnimationCurve spawnRateCurve = AnimationCurve.Linear(0f, 1f, 100f, 2f);
    public float spawnX = 10f;
    public float groundY = -3.5f;
    public float ceilingY = 3.5f;
    public bool usePooling = true;

    private System.Collections.Generic.Dictionary<GameObject, ObjectPool> pools = new System.Collections.Generic.Dictionary<GameObject, ObjectPool>();

    private float timer;

    /// <summary>
    /// Creates pools for all obstacle prefabs on startup when pooling is
    /// enabled.
    /// </summary>
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

    /// <summary>
    /// Generates obstacles according to <see cref="spawnRateCurve"/> while
    /// the game is running.
    /// </summary>
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
            Spawn();
            timer = spawnInterval / difficulty;
        }
    }

    /// <summary>
    /// Spawns either a ground or ceiling obstacle at the configured
    /// location using pooling when possible.
    /// </summary>
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

    /// <summary>
    /// Helper to create and register an <see cref="ObjectPool"/> for the
    /// given prefab.
    /// </summary>
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
