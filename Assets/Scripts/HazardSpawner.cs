using UnityEngine;

public class HazardSpawner : MonoBehaviour
{
    public GameObject[] pitPrefabs;
    public GameObject[] batPrefabs;
    public float spawnInterval = 5f;
    public float spawnX = 10f;
    public float groundY = -3.5f;
    public float airY = 1.5f;

    private float timer;

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
            difficulty += GameManager.Instance.GetDistance() / 200f;
        }
        if (timer <= 0f)
        {
            SpawnHazard();
            timer = spawnInterval / difficulty;
        }
    }

    void SpawnHazard()
    {
        bool spawnPit = Random.value > 0.5f;
        GameObject[] prefabs = spawnPit ? pitPrefabs : batPrefabs;
        if (prefabs.Length == 0) return;
        GameObject prefab = prefabs[Random.Range(0, prefabs.Length)];
        float y = spawnPit ? groundY : airY;
        Vector3 pos = new Vector3(spawnX, y, 0f);
        Instantiate(prefab, pos, Quaternion.identity);
    }
}
