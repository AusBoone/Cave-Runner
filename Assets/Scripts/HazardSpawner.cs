using UnityEngine;

/// <summary>
/// Creates pits and flying bat hazards as the game progresses. Spawning
/// frequency increases with the player's distance. Supports object
/// pooling for performance.
/// </summary>
public class HazardSpawner : MonoBehaviour
{
    public GameObject[] pitPrefabs;
    public GameObject[] batPrefabs;
    public float spawnInterval = 5f;
    // Controls how the spawn interval scales with player distance.
    public AnimationCurve spawnRateCurve = AnimationCurve.Linear(0f, 1f, 100f, 2f);
    public float spawnX = 10f;
    public float groundY = -3.5f;
    public float airY = 1.5f;
    public bool usePooling = true;

    private System.Collections.Generic.Dictionary<GameObject, ObjectPool> pools = new System.Collections.Generic.Dictionary<GameObject, ObjectPool>();

    private float timer;

    /// <summary>
    /// Initializes object pools for all hazard prefabs when pooling is
    /// enabled.
    /// </summary>
    void Start()
    {
        if (usePooling)
        {
            if (pitPrefabs != null)
                foreach (GameObject prefab in pitPrefabs)
                {
                    CreatePool(prefab);
                }
            if (batPrefabs != null)
                foreach (GameObject prefab in batPrefabs)
                {
                    CreatePool(prefab);
                }
        }
    }

    /// <summary>
    /// Spawns hazards at intervals determined by <see cref="spawnRateCurve"/>.
    /// No hazards are generated while the game is not running.
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
            SpawnHazard();
            timer = spawnInterval / difficulty;
        }
    }

    /// <summary>
    /// Chooses either a pit or bat prefab and places it at the appropriate
    /// height using pooling if available.
    /// </summary>
    void SpawnHazard()
    {
        bool spawnPit = Random.value > 0.5f;
        GameObject[] prefabs = spawnPit ? pitPrefabs : batPrefabs;
        if (prefabs.Length == 0) return;
        GameObject prefab = prefabs[Random.Range(0, prefabs.Length)];
        float y = spawnPit ? groundY : airY;
        Vector3 pos = new Vector3(spawnX, y, 0f);
        GameObject obj = null;
        if (usePooling && pools.TryGetValue(prefab, out ObjectPool pool))
        {
            obj = pool.GetObject(pos, Quaternion.identity);
        }
        else
        {
            obj = Instantiate(prefab, pos, Quaternion.identity);
        }
        if (!spawnPit && obj != null && obj.GetComponent<EnemyBehavior>() == null)
        {
            obj.AddComponent<EnemyBehavior>();
        }
    }

    /// <summary>
    /// Utility to create a pool for the provided hazard prefab.
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
