using UnityEngine;

public class Coin : MonoBehaviour
{
    public int value = 1;
    public AudioClip collectClip;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.AddCoins(value);
            }
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySound(collectClip);
            }
            Destroy(gameObject);
        }
    }
}
