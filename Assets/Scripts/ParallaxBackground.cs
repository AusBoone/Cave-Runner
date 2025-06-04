using UnityEngine;

public class ParallaxBackground : MonoBehaviour
{
    public float scrollSpeed = 0.5f;
    public float resetPosition = -20f;
    public float startPosition = 20f;

    void Update()
    {
        float speed = GameManager.Instance != null ? GameManager.Instance.GetSpeed() : scrollSpeed;
        transform.Translate(Vector3.left * speed * Time.deltaTime);

        if (transform.position.x <= resetPosition)
        {
            Vector3 newPos = transform.position;
            newPos.x = startPosition;
            transform.position = newPos;
        }
    }
}
