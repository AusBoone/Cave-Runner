using UnityEngine;

public class CoinMagnet : MonoBehaviour
{
    public float magnetRadius = 3f;
    public float magnetSpeed = 10f;
    // Layer mask so only coins are searched when attracting them.
    public LayerMask coinLayer;

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
        // Only check colliders on the specified coin layer for better performance.
        Collider2D[] coins = Physics2D.OverlapCircleAll(transform.position, magnetRadius, coinLayer);
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
