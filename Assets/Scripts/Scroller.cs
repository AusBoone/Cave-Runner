using UnityEngine;

public class Scroller : MonoBehaviour
{
    private PooledObject pooledObject;

    void Awake()
    {
        pooledObject = GetComponent<PooledObject>();
    }

    void Update()
    {
        float speed = GameManager.Instance != null ? GameManager.Instance.GetSpeed() : 5f;
        transform.Translate(Vector3.left * speed * Time.deltaTime);
        if (transform.position.x < -20f)
        {
            if (pooledObject != null && pooledObject.Pool != null)
            {
                pooledObject.Pool.ReturnObject(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}
