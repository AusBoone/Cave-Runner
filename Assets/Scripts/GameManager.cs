using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public float baseSpeed = 5f;
    public float speedIncrease = 0.1f;
    public Text scoreLabel;

    private float distance;
    private float currentSpeed;

    public static GameManager Instance { get; private set; }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
        currentSpeed = baseSpeed;
    }

    void Update()
    {
        currentSpeed += speedIncrease * Time.deltaTime;
        distance += currentSpeed * Time.deltaTime;
        if (scoreLabel != null)
        {
            scoreLabel.text = Mathf.FloorToInt(distance).ToString();
        }
    }

    public float GetSpeed()
    {
        return currentSpeed;
    }

    public void GameOver()
    {
        Scene currentScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(currentScene.buildIndex);
    }
}
