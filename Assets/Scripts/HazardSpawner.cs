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
    // New enemy variations
    public GameObject[] zigZagPrefabs;
    public GameObject[] swoopPrefabs;
    public GameObject[] shooterPrefabs;
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
            if (zigZagPrefabs != null)
                foreach (GameObject prefab in zigZagPrefabs)
                {
                    CreatePool(prefab);
                }
            if (swoopPrefabs != null)
                foreach (GameObject prefab in swoopPrefabs)
                {
                    CreatePool(prefab);
                }
            if (shooterPrefabs != null)
                foreach (GameObject prefab in shooterPrefabs)
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
    /// Chooses a hazard prefab from any configured list and places it at the
    /// appropriate height using pooling when available.
    /// </summary>
    void SpawnHazard()
    {
        var lists = new System.Collections.Generic.List<GameObject[]>();
        var heights = new System.Collections.Generic.List<float>();

        if (pitPrefabs != null && pitPrefabs.Length > 0)
        {
            lists.Add(pitPrefabs);
            heights.Add(groundY);
        }
        if (batPrefabs != null && batPrefabs.Length > 0)
        {
            lists.Add(batPrefabs);
            heights.Add(airY);
        }
        if (zigZagPrefabs != null && zigZagPrefabs.Length > 0)
        {
            lists.Add(zigZagPrefabs);
            heights.Add(airY);
        }
        if (swoopPrefabs != null && swoopPrefabs.Length > 0)
        {
            lists.Add(swoopPrefabs);
            heights.Add(airY);
        }
        if (shooterPrefabs != null && shooterPrefabs.Length > 0)
        {
            lists.Add(shooterPrefabs);
            heights.Add(airY);
        }

        if (lists.Count == 0) return;

        int groupIndex = Random.Range(0, lists.Count);
        GameObject[] prefabs = lists[groupIndex];
        float y = heights[groupIndex];
        GameObject prefab = prefabs[Random.Range(0, prefabs.Length)];
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
        if (obj != null &&
            obj.GetComponent<EnemyBehavior>() == null &&
            obj.GetComponent<ZigZagEnemy>() == null &&
            obj.GetComponent<SwoopingEnemy>() == null &&
            obj.GetComponent<ShooterEnemy>() == null)
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
