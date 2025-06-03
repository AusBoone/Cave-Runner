using UnityEngine;

public class Scroller : MonoBehaviour
{
    void Update()
    {
        float speed = GameManager.Instance != null ? GameManager.Instance.GetSpeed() : 5f;
        transform.Translate(Vector3.left * speed * Time.deltaTime);
        if (transform.position.x < -20f)
        {
            Destroy(gameObject);
        }
    }
}
