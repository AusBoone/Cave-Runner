using UnityEngine;

/// <summary>
/// Handles all player movement including jumping, variable jump height,
/// sliding and collision responses. Uses simple physics based controls
/// and communicates with the <see cref="GameManager"/> for game state.
/// </summary>
public class PlayerController : MonoBehaviour
{
    // Force applied when jumping.
    public float jumpForce = 400f;
    // Maximum time additional upward force is applied while the jump button is held.
    public float variableJumpTime = 0.2f;
    // Grace period after leaving the ground where the player can still jump.
    public float coyoteTime = 0.1f;
    public float slideDuration = 0.5f;
    public LayerMask groundLayer;
    public AudioClip jumpClip;
    public AudioClip slideClip;
    public AudioClip hitClip;

    private Rigidbody2D rb;
    private CapsuleCollider2D coll;
    private Animator anim;
    private bool isGrounded;
    private bool isSliding;
    private float slideTimer;
    private Vector2 colliderSize;
    private Vector2 colliderOffset;

    private int jumpsRemaining;
    private float coyoteTimer;
    private float variableJumpTimer;
    private bool isJumping;

    /// <summary>
    /// Caches component references used for controlling the character.
    /// </summary>
    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        coll = GetComponent<CapsuleCollider2D>();
        anim = GetComponent<Animator>();
        colliderSize = coll.size;
        colliderOffset = coll.offset;
    }

    /// <summary>
    /// Handles player input for jumping and sliding each frame. Also
    /// updates timers controlling jump height and slide duration.
    /// </summary>
    void Update()
    {
        if (GameManager.Instance == null || !GameManager.Instance.IsRunning())
        {
            return;
        }
        // Update grounded state before processing input
        CheckGrounded();

        // Handle jump input and variable jump height using custom bindings
        if (Input.GetKeyDown(InputManager.JumpKey))
        {
            AttemptJump();
        }
        if (Input.GetKey(InputManager.JumpKey) && isJumping)
        {
            // Apply extra upward force while the jump button is held
            if (variableJumpTimer > 0f)
            {
                rb.AddForce(Vector2.up * jumpForce * Time.deltaTime);
                variableJumpTimer -= Time.deltaTime;
            }
        }
        if (Input.GetKeyUp(InputManager.JumpKey))
        {
            isJumping = false;
        }

        if (Input.GetKeyDown(InputManager.SlideKey))
        {
            StartSlide();
        }

        if (isSliding)
        {
            // Countdown slide duration and revert when it expires
            slideTimer -= Time.deltaTime;
            if (slideTimer <= 0f)
            {
                EndSlide();
            }
        }
    }

    /// <summary>
    /// Uses a raycast to determine if the player is touching a surface in the
    /// current gravity direction. Also manages the coyote-time grace period and
    /// available double jump.
    /// </summary>
    void CheckGrounded()
    {
        Vector2 origin = transform.position;
        float distance = 0.1f; // short ray in the direction of gravity
        Vector2 rayDir = Physics2D.gravity.y > 0f ? Vector2.up : Vector2.down;
        RaycastHit2D hit = Physics2D.Raycast(origin, rayDir, distance, groundLayer);
        bool wasGrounded = isGrounded;
        isGrounded = hit.collider != null;
        if (isGrounded)
        {
            // Reset coyote timer and available jumps when touching the ground
            coyoteTimer = coyoteTime;
            if (!wasGrounded)
            {
                jumpsRemaining = 1; // allow one extra jump in air
            }
        }
        else
        {
            // Count down the grace period once airborne
            coyoteTimer -= Time.deltaTime;
        }
    }

    /// <summary>
    /// Executes a jump if conditions are met. Supports coyote time and
    /// a single air jump.
    /// </summary>
    void AttemptJump()
    {
        // Allow jumping while grounded, during the coyote window, or if an extra jump remains.
        if (isGrounded || coyoteTimer > 0f || jumpsRemaining > 0)
        {
            rb.velocity = new Vector2(rb.velocity.x, 0f);
            rb.AddForce(Vector2.up * jumpForce);
            isJumping = true;
            variableJumpTimer = variableJumpTime;
            anim?.SetTrigger("Jump");
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySound(jumpClip);
            }
            if (!isGrounded && jumpsRemaining > 0)
            {
                jumpsRemaining--;
            }
            coyoteTimer = 0f;
        }
    }

    /// <summary>
    /// Begins the slide animation and adjusts the collider size.
    /// </summary>
    void StartSlide()
    {
        if (!isSliding)
        {
            isSliding = true;
            slideTimer = slideDuration;
            coll.size = new Vector2(colliderSize.x, colliderSize.y / 2f);
            coll.offset = new Vector2(colliderOffset.x, colliderOffset.y - colliderSize.y / 4f);
            anim?.SetBool("Slide", true);
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySound(slideClip);
            }
        }
    }

    /// <summary>
    /// Restores collider dimensions when the slide finishes.
    /// </summary>
    void EndSlide()
    {
        isSliding = false;
        coll.size = colliderSize;
        coll.offset = colliderOffset;
        anim?.SetBool("Slide", false);
    }

    /// <summary>
    /// If the player collides with an obstacle or hazard the game ends.
    /// </summary>
    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Obstacle") || collision.gameObject.CompareTag("Hazard"))
        {
            PlayerShield shield = GetComponent<PlayerShield>();
            if (shield != null && shield.IsActive)
            {
                shield.AbsorbHit();
                return;
            }
            anim?.SetTrigger("Hit");
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySound(hitClip);
            }
            if (GameManager.Instance != null && !GameManager.Instance.IsGameOver())
            {
                GameManager.Instance.GameOver();
            }
        }
    }
}
