using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public float jumpForce = 400f;
    public float slideDuration = 0.5f;
    public LayerMask groundLayer;
    public AudioClip jumpClip;
    public AudioClip slideClip;
    public AudioClip hitClip;

    private Rigidbody2D rb;
    private CapsuleCollider2D coll;
    private bool isGrounded;
    private bool isSliding;
    private float slideTimer;
    private Vector2 colliderSize;
    private Vector2 colliderOffset;

    private int jumpsRemaining;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        coll = GetComponent<CapsuleCollider2D>();
        colliderSize = coll.size;
        colliderOffset = coll.offset;
    }

    void Update()
    {
        if (GameManager.Instance == null || !GameManager.Instance.IsRunning())
        {
            return;
        }
        CheckGrounded();

        if (Input.GetButtonDown("Jump"))
        {
            AttemptJump();
        }

        if (Input.GetButtonDown("Fire1"))
        {
            StartSlide();
        }

        if (isSliding)
        {
            slideTimer -= Time.deltaTime;
            if (slideTimer <= 0f)
            {
                EndSlide();
            }
        }
    }

    void CheckGrounded()
    {
        Vector2 origin = transform.position;
        float distance = 0.1f;
        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, distance, groundLayer);
        bool wasGrounded = isGrounded;
        isGrounded = hit.collider != null;
        if (isGrounded && !wasGrounded)
        {
            jumpsRemaining = 1; // allow one extra jump in air
        }
    }

    void AttemptJump()
    {
        if (isGrounded || jumpsRemaining > 0)
        {
            rb.velocity = new Vector2(rb.velocity.x, 0f);
            rb.AddForce(Vector2.up * jumpForce);
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySound(jumpClip);
            }
            if (!isGrounded)
            {
                jumpsRemaining--;
            }
        }
    }

    void StartSlide()
    {
        if (!isSliding)
        {
            isSliding = true;
            slideTimer = slideDuration;
            coll.size = new Vector2(colliderSize.x, colliderSize.y / 2f);
            coll.offset = new Vector2(colliderOffset.x, colliderOffset.y - colliderSize.y / 4f);
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySound(slideClip);
            }
        }
    }

    void EndSlide()
    {
        isSliding = false;
        coll.size = colliderSize;
        coll.offset = colliderOffset;
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Obstacle") || collision.gameObject.CompareTag("Hazard"))
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySound(hitClip);
            }
            if (GameManager.Instance != null)
            {
                GameManager.Instance.GameOver();
            }
        }
    }
}
