using UnityEngine;

public class CoinMagnet : MonoBehaviour
{
    public float magnetRadius = 3f;
    public float magnetSpeed = 10f;

    private float magnetTimer;
    private bool magnetActive;

    void Update()
    {
        if (magnetActive)
        {
            magnetTimer -= Time.deltaTime;
            if (magnetTimer <= 0f)
            {
                magnetActive = false;
            }
            else
            {
                AttractCoins();
            }
        }
    }

    void AttractCoins()
    {
        Collider2D[] coins = Physics2D.OverlapCircleAll(transform.position, magnetRadius);
        foreach (Collider2D c in coins)
        {
            if (c.CompareTag("Coin"))
            {
                c.transform.position = Vector3.MoveTowards(c.transform.position, transform.position, magnetSpeed * Time.deltaTime);
            }
        }
    }

    public void ActivateMagnet(float duration)
    {
        magnetActive = true;
        magnetTimer = duration;
    }
}
