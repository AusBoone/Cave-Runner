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
    public GameObject[] movingPlatforms;
    public GameObject[] rotatingHazards;

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
        if (groundObstacles.Length == 0 && groundObstacleNames != null && groundObstacleNames.Length > 0)
        {
            groundObstacles = LoadPrefabs(groundObstacleNames);
        }
        if (ceilingObstacles.Length == 0 && ceilingObstacleNames != null && ceilingObstacleNames.Length > 0)
        {
            ceilingObstacles = LoadPrefabs(ceilingObstacleNames);
        }
        if (movingPlatforms.Length == 0 && movingPlatformNames != null && movingPlatformNames.Length > 0)
        {
            movingPlatforms = LoadPrefabs(movingPlatformNames);
        }
        if (rotatingHazards.Length == 0 && rotatingHazardNames != null && rotatingHazardNames.Length > 0)
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
            timer = spawnInterval / difficulty;
        }
    }

    /// <summary>
    /// Spawns either a ground or ceiling obstacle at the configured
    /// location using pooling when possible.
    /// </summary>
    void Spawn()
    {
        var prefabsList = new System.Collections.Generic.List<GameObject[]>();
        var yList = new System.Collections.Generic.List<float>();
        if (groundObstacles.Length > 0)
        {
            prefabsList.Add(groundObstacles);
            yList.Add(groundY);
        }
        if (ceilingObstacles.Length > 0)
        {
            prefabsList.Add(ceilingObstacles);
            yList.Add(ceilingY);
        }
        if (movingPlatforms.Length > 0)
        {
            prefabsList.Add(movingPlatforms);
            yList.Add(middleY);
        }
        if (rotatingHazards.Length > 0)
        {
            prefabsList.Add(rotatingHazards);
            yList.Add(middleY);
        }
        if (prefabsList.Count == 0) return;

        int index = Random.Range(0, prefabsList.Count);
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
