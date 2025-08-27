using UnityEngine;

/// <summary>
/// Periodically spawns ground or ceiling obstacles that the player must
/// avoid. Spawn timing accelerates as the run progresses and objects can
/// be pooled for efficiency. This class now supports stage-specific
/// probability weights and a spawn rate multiplier supplied by
/// <see cref="StageManager"/>.
/// </summary>
/// <remarks>
/// Updated to include explicit null checks before inspecting obstacle arrays
/// in both <see cref="Start"/> and <see cref="Spawn"/>. This prevents
/// <see cref="System.NullReferenceException"/> when obstacle collections are
/// left unassigned in the Unity inspector or reset at runtime.
/// </remarks>
public class ObstacleSpawner : MonoBehaviour
{
    public GameObject[] groundObstacles;
    public GameObject[] ceilingObstacles;
    public GameObject[] movingPlatforms;
    public GameObject[] rotatingHazards;

    [Header("Stage Parameters")]
    [Tooltip("Multiplier applied to spawn rate from the current stage.")]
    public float spawnMultiplier = 1f;
    [Tooltip("Relative chance to pick ground obstacles.")]
    public float groundChance = 1f;
    [Tooltip("Relative chance to pick ceiling obstacles.")]
    public float ceilingChance = 1f;
    [Tooltip("Relative chance to pick moving platforms.")]
    public float platformChance = 1f;
    [Tooltip("Relative chance to pick rotating hazards.")]
    public float rotatingChance = 1f;

    // Names of obstacle prefabs located under Assets/Art/Resources.
    public string[] groundObstacleNames;
    public string[] ceilingObstacleNames;
    public string[] movingPlatformNames;
    public string[] rotatingHazardNames;
    public float spawnInterval = 2f;
    // Curve representing how spawn rate increases with distance.
    public AnimationCurve spawnRateCurve = AnimationCurve.Linear(0f, 1f, 100f, 2f);
    public float spawnX = 10f;
    public float groundY = -3.5f;
    public float ceilingY = 3.5f;
    public float middleY = 0f;
    public bool usePooling = true;

    private System.Collections.Generic.Dictionary<GameObject, ObjectPool> pools = new System.Collections.Generic.Dictionary<GameObject, ObjectPool>();

    private float timer;

    /// <summary>
    /// Creates pools for all obstacle prefabs on startup when pooling is
    /// enabled.
    /// </summary>
    void Start()
    {
        // Load ground obstacle prefabs only when the array is null or empty
        // and valid prefab names are provided. This defensive check prevents
        // null reference errors when obstacles are not assigned in the
        // inspector.
        if ((groundObstacles == null || groundObstacles.Length == 0) &&
            groundObstacleNames != null && groundObstacleNames.Length > 0)
        {
            groundObstacles = LoadPrefabs(groundObstacleNames);
        }
        // Repeat the same null-safe loading logic for ceiling obstacles.
        if ((ceilingObstacles == null || ceilingObstacles.Length == 0) &&
            ceilingObstacleNames != null && ceilingObstacleNames.Length > 0)
        {
            ceilingObstacles = LoadPrefabs(ceilingObstacleNames);
        }
        // Apply null checks for moving platforms to avoid accessing an
        // uninitialized array.
        if ((movingPlatforms == null || movingPlatforms.Length == 0) &&
            movingPlatformNames != null && movingPlatformNames.Length > 0)
        {
            movingPlatforms = LoadPrefabs(movingPlatformNames);
        }
        // Ensure rotating hazards are loaded safely by verifying the array is
        // not null before evaluating its length.
        if ((rotatingHazards == null || rotatingHazards.Length == 0) &&
            rotatingHazardNames != null && rotatingHazardNames.Length > 0)
        {
            rotatingHazards = LoadPrefabs(rotatingHazardNames);
        }
        if (usePooling)
        {
            if (groundObstacles != null)
                foreach (GameObject prefab in groundObstacles)
                {
                    CreatePool(prefab);
                }
            if (ceilingObstacles != null)
                foreach (GameObject prefab in ceilingObstacles)
                {
                    CreatePool(prefab);
                }
            if (movingPlatforms != null)
                foreach (GameObject prefab in movingPlatforms)
                {
                    CreatePool(prefab);
                }
            if (rotatingHazards != null)
                foreach (GameObject prefab in rotatingHazards)
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
            float mult = Mathf.Max(0.01f, spawnMultiplier);
            if (GameManager.Instance != null && GameManager.Instance.HardcoreMode)
            {
                // Hardcore mode spawns obstacles more frequently
                mult *= Mathf.Max(0.01f, GameManager.Instance.hardcoreSpawnMultiplier);
            }
            timer = spawnInterval / (difficulty * mult);
        }
    }

    /// <summary>
    /// Spawns either a ground or ceiling obstacle at the configured
    /// location using pooling when possible. Each obstacle array is
    /// checked for null before accessing its length to guard against
    /// unassigned references.
    /// </summary>
    void Spawn()
    {
        // Lists accumulate eligible prefab arrays, their spawn heights and
        // selection weights. Only non-null arrays with at least one element
        // are considered valid.
        var prefabsList = new System.Collections.Generic.List<GameObject[]>();
        var yList = new System.Collections.Generic.List<float>();
        var chanceList = new System.Collections.Generic.List<float>();

        // Ground obstacles: verify array exists before checking length to
        // avoid NullReferenceException when groundObstacles is unassigned.
        if (groundObstacles != null && groundObstacles.Length > 0 && groundChance > 0f)
        {
            prefabsList.Add(groundObstacles);
            yList.Add(groundY);
            chanceList.Add(groundChance);
        }

        // Ceiling obstacles: same defensive null check pattern as above.
        if (ceilingObstacles != null && ceilingObstacles.Length > 0 && ceilingChance > 0f)
        {
            prefabsList.Add(ceilingObstacles);
            yList.Add(ceilingY);
            chanceList.Add(ceilingChance);
        }

        // Moving platforms: ensure the array was provided before use.
        if (movingPlatforms != null && movingPlatforms.Length > 0 && platformChance > 0f)
        {
            prefabsList.Add(movingPlatforms);
            yList.Add(middleY);
            chanceList.Add(platformChance);
        }

        // Rotating hazards: null check protects against missing assignments.
        if (rotatingHazards != null && rotatingHazards.Length > 0 && rotatingChance > 0f)
        {
            prefabsList.Add(rotatingHazards);
            yList.Add(middleY);
            chanceList.Add(rotatingChance);
        }

        // If no eligible arrays were found, do not attempt to spawn anything.
        if (prefabsList.Count == 0) return;

        float total = 0f;
        foreach (float c in chanceList) total += c;
        float roll = Random.Range(0f, total);
        int index = 0;
        float accum = 0f;
        for (int i = 0; i < chanceList.Count; i++)
        {
            accum += chanceList[i];
            if (roll <= accum)
            {
                index = i;
                break;
            }
        }
        GameObject[] prefabs = prefabsList[index];
        float y = yList[index];
        GameObject prefab = prefabs[Random.Range(0, prefabs.Length)];
        Vector3 pos = new Vector3(spawnX, y, 0f);
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

    /// <summary>
    /// Retrieves obstacle prefabs by name from Resources/Art and
    /// returns the loaded array.
    /// </summary>
    GameObject[] LoadPrefabs(string[] names)
    {
        var list = new System.Collections.Generic.List<GameObject>();
        foreach (string n in names)
        {
            GameObject obj = Resources.Load<GameObject>("Art/" + n);
            if (obj != null)
            {
                list.Add(obj);
            }
        }
        return list.ToArray();
    }
}
