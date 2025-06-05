using UnityEngine;

/// <summary>
/// Component placed on the player that can temporarily attract nearby
/// coins. When activated, all coins inside <see cref="magnetRadius"/>
/// will move toward the player for the given duration.
/// </summary>
public class CoinMagnet : MonoBehaviour
{
    public float magnetRadius = 3f;
    public float magnetSpeed = 10f;
    // Layer mask so only coins are searched when attracting them.
    public LayerMask coinLayer;
    // How many colliders can be detected each frame.
    [SerializeField]
    private int colliderBufferSize = 10;
    // Preallocated buffer to store detected colliders.
    private Collider2D[] _colliderBuffer;

    private float magnetTimer;
    private bool magnetActive;

    /// <summary>
    /// Allocates the collider buffer used for detecting nearby coins.
    /// </summary>
    void Awake()
    {
        _colliderBuffer = new Collider2D[colliderBufferSize];
    }

    /// <summary>
    /// Counts down the active timer and pulls coins toward the player
    /// each frame while the magnet is active.
    /// </summary>
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

    /// <summary>
    /// Finds coins within <see cref="magnetRadius"/> and moves them
    /// toward the player's position.
    /// </summary>
    void AttractCoins()
    {
        // Only check colliders on the specified coin layer for better performance.
        int count = Physics2D.OverlapCircleNonAlloc(transform.position, magnetRadius, _colliderBuffer, coinLayer);
        for (int i = 0; i < count; i++)
        {
            Collider2D c = _colliderBuffer[i];
            if (c != null && c.CompareTag("Coin"))
            {
                c.transform.position = Vector3.MoveTowards(c.transform.position, transform.position, magnetSpeed * Time.deltaTime);
            }
        }
    }

    /// <summary>
    /// Enables the magnet effect for the specified duration in seconds.
    /// </summary>
    public void ActivateMagnet(float duration)
    {
        magnetActive = true;
        magnetTimer = duration;
    }
}
