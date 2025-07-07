using UnityEngine;

/// <summary>
/// Spawns temporary power-up items such as coin magnets or speed boosts.
/// Frequency is controlled by <see cref="spawnRateCurve"/> and objects can
/// be pooled to reduce instantiation overhead.
/// </summary>
public class PowerUpSpawner : MonoBehaviour
{
    public GameObject[] powerUpPrefabs;
    public float spawnInterval = 8f;
    // Curve determining how power-up spawn rate scales with distance.
    public AnimationCurve spawnRateCurve = AnimationCurve.Linear(0f, 1f, 100f, 1f);
    public float spawnX = 10f;
    public float minY = -3f;
    public float maxY = 3f;
    public bool usePooling = true;

    private System.Collections.Generic.Dictionary<GameObject, ObjectPool> pools = new System.Collections.Generic.Dictionary<GameObject, ObjectPool>();

    private float timer;

    /// <summary>
    /// Initializes object pools for each power-up prefab if pooling is
    /// enabled.
    /// </summary>
    void Start()
    {
        if (usePooling)
        {
            if (powerUpPrefabs != null)
                foreach (GameObject prefab in powerUpPrefabs)
                {
                    CreatePool(prefab);
                }
        }
    }

    /// <summary>
    /// Spawns power-ups periodically while the game is running. The
    /// interval shortens over distance using <see cref="spawnRateCurve"/>.
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
            SpawnPowerUp();
            float interval = spawnInterval / difficulty;
            if (GameManager.Instance != null && GameManager.Instance.HardcoreMode)
            {
                // Hardcore mode lowers the power-up frequency by scaling the interval
                float mult = Mathf.Max(0.01f, GameManager.Instance.hardcorePowerUpRateMultiplier);
                interval *= 1f / mult;
            }
            timer = interval;
        }
    }

    /// <summary>
    /// Instantiates or retrieves a power-up prefab at a random height.
    /// </summary>
    void SpawnPowerUp()
    {
        if (powerUpPrefabs.Length == 0) return;
        GameObject prefab = powerUpPrefabs[Random.Range(0, powerUpPrefabs.Length)];
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

    /// <summary>
    /// Helper to allocate an <see cref="ObjectPool"/> for a specific
    /// power-up prefab.
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
