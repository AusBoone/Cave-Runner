using UnityEngine;

public class ObstacleSpawner : MonoBehaviour
{
    public GameObject[] groundObstacles;
    public GameObject[] ceilingObstacles;
    public float spawnInterval = 2f;
    public float spawnX = 10f;
    public float groundY = -3.5f;
    public float ceilingY = 3.5f;

    private float timer;

    void Update()
    {
        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            Spawn();
            timer = spawnInterval;
        }
    }

    void Spawn()
    {
        bool fromCeiling = Random.value > 0.5f;
        GameObject[] prefabs = fromCeiling ? ceilingObstacles : groundObstacles;
        if (prefabs.Length == 0) return;
        GameObject prefab = prefabs[Random.Range(0, prefabs.Length)];
        Vector3 pos = new Vector3(spawnX, fromCeiling ? ceilingY : groundY, 0f);
        Instantiate(prefab, pos, Quaternion.identity);
    }
}
