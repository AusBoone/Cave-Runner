using UnityEngine;

public class MagnetPowerUp : MonoBehaviour
{
    public float duration = 5f;
    public AudioClip collectClip;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            CoinMagnet magnet = other.GetComponent<CoinMagnet>();
            if (magnet != null)
            {
                magnet.ActivateMagnet(duration);
            }
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySound(collectClip);
            }
            Destroy(gameObject);
        }
    }
}
