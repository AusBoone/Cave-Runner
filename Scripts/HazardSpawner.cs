using UnityEngine;

/// <summary>
/// Creates pits and flying hazards as the game progresses. Spawning
/// frequency increases with the player's distance. Supports object
/// pooling for performance. Stage-specific spawn weights and a
/// difficulty multiplier are applied by <see cref="StageManager"/>.
/// </summary>
public class HazardSpawner : MonoBehaviour
{
    public GameObject[] pitPrefabs;
    public GameObject[] batPrefabs;
    // New enemy variations
    public GameObject[] zigZagPrefabs;
    public GameObject[] swoopPrefabs;
    public GameObject[] shooterPrefabs;
    [Header("Stage Parameters")]
    [Tooltip("Multiplier applied to spawn rate from the current stage.")]
    public float spawnMultiplier = 1f;
    [Tooltip("Relative chance to spawn pits.")]
    public float pitChance = 1f;
    [Tooltip("Relative chance to spawn bats.")]
    public float batChance = 1f;
    [Tooltip("Relative chance to spawn zig-zag enemies.")]
    public float zigZagChance = 1f;
    [Tooltip("Relative chance to spawn swooping enemies.")]
    public float swoopChance = 1f;
    [Tooltip("Relative chance to spawn shooter enemies.")]
    public float shooterChance = 1f;
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
            float mult = Mathf.Max(0.01f, spawnMultiplier);
            if (GameManager.Instance != null && GameManager.Instance.HardcoreMode)
            {
                // Hardcore mode increases hazard frequency using the configured multiplier
                mult *= Mathf.Max(0.01f, GameManager.Instance.hardcoreSpawnMultiplier);
            }
            timer = spawnInterval / (difficulty * mult);
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
        var chance = new System.Collections.Generic.List<float>();

        if (pitPrefabs != null && pitPrefabs.Length > 0 && pitChance > 0f)
        {
            lists.Add(pitPrefabs);
            heights.Add(groundY);
            chance.Add(pitChance);
        }
        if (batPrefabs != null && batPrefabs.Length > 0 && batChance > 0f)
        {
            lists.Add(batPrefabs);
            heights.Add(airY);
            chance.Add(batChance);
        }
        if (zigZagPrefabs != null && zigZagPrefabs.Length > 0 && zigZagChance > 0f)
        {
            lists.Add(zigZagPrefabs);
            heights.Add(airY);
            chance.Add(zigZagChance);
        }
        if (swoopPrefabs != null && swoopPrefabs.Length > 0 && swoopChance > 0f)
        {
            lists.Add(swoopPrefabs);
            heights.Add(airY);
            chance.Add(swoopChance);
        }
        if (shooterPrefabs != null && shooterPrefabs.Length > 0 && shooterChance > 0f)
        {
            lists.Add(shooterPrefabs);
            heights.Add(airY);
            chance.Add(shooterChance);
        }

        if (lists.Count == 0) return;

        float total = 0f;
        foreach (float c in chance) total += c;
        float roll = Random.Range(0f, total);
        int groupIndex = 0;
        float accum = 0f;
        for (int i = 0; i < chance.Count; i++)
        {
            accum += chance[i];
            if (roll <= accum)
            {
                groupIndex = i;
                break;
            }
        }
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

        // Provide the freshly spawned enemy with the player's transform so it
        // can immediately begin chasing without performing a scene search. When
        // the GameManager or player reference is missing the enemy remains idle
        // thanks to the null checks inside EnemyBehavior.Update().
        if (obj != null && GameManager.Instance != null)
        {
            var behavior = obj.GetComponent<EnemyBehavior>();
            behavior?.SetTarget(GameManager.Instance.PlayerTransform);
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
