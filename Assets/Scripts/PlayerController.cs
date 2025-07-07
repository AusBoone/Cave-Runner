using UnityEngine;

/// <summary>
/// Handles all player movement including jumping, variable jump height, sliding
/// and collision responses. The controller now queries input through
/// <see cref="InputManager"/> so it works with both the new Input System and the
/// legacy input manager. Uses simple physics based controls and communicates
/// with the <see cref="GameManager"/> for game state. Recent revisions add a
/// short jump buffering window so presses just before landing still register,
/// slide buffering with an optional air dive when sliding mid-air, dynamic
/// gravity scaling, an optional fast-fall when holding the down key, an air dash
/// triggered by sliding with horizontal input, and slide canceling so releasing
/// the key ends the move immediately. These tweaks keep jumps snappy and give
/// the player crisp mid-air control.
/// </summary>
public class PlayerController : MonoBehaviour
{
    // Force applied when jumping.
    public float jumpForce = 400f;
    // Maximum time additional upward force is applied while the jump button is held.
    public float variableJumpTime = 0.2f;
    // Grace period after leaving the ground where the player can still jump.
    public float coyoteTime = 0.1f;
    // Time window after pressing jump where the action is buffered
    // so the player will jump upon landing. Helps make controls
    // responsive even with tight timing.
    public float jumpBufferTime = 0.1f;
    // Time window after pressing slide while airborne where the slide
    // action will trigger automatically upon landing.
    public float slideBufferTime = 0.1f;
    // Downward impulse applied when initiating a slide in the air
    // so players can quickly drop to the ground.
    public float airDiveForce = 5f;
    // Multiplier applied to gravity while falling so descents feel faster.
    public float fallGravityMultiplier = 2.5f;
    // Multiplier applied when the jump key is released early so short hops are
    // crisp. Values above 1 increase downward acceleration.
    public float lowJumpGravityMultiplier = 2f;
    // Additional multiplier when the player holds the down input mid-air.
    // Causes a fast fall for quicker descents without requiring a slide.
    public float fastFallGravityMultiplier = 1.5f;
    // Impulse applied when performing an air dash.
    public float dashForce = 8f;
    // Minimum time between air dashes.
    public float dashCooldown = 0.5f;
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
    private float jumpBufferTimer;
    private float slideBufferTimer;
    private bool airDivePending;
    private float dashTimer;

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
        jumpBufferTimer = 0f;
        slideBufferTimer = 0f;
        airDivePending = false;
        dashTimer = 0f;
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
        // Update grounded state and cooldown timers before processing input
        CheckGrounded();
        if (dashTimer > 0f)
        {
            dashTimer -= Time.deltaTime;
        }

        // Handle jump input and variable jump height using custom bindings
        if (InputManager.GetJumpDown())
        {
            // Buffer the jump so it can occur on landing if the timing
            // was slightly early.
            jumpBufferTimer = jumpBufferTime;
        }

        if (jumpBufferTimer > 0f)
        {
            if (isGrounded || coyoteTimer > 0f || jumpsRemaining > 0)
            {
                AttemptJump();
                jumpBufferTimer = 0f;
            }
            else
            {
                jumpBufferTimer -= Time.deltaTime;
            }
        }
        if (InputManager.GetJump() && isJumping)
        {
            // Apply extra upward force while the jump button is held
            if (variableJumpTimer > 0f)
            {
                rb.AddForce(Vector2.up * jumpForce * Time.deltaTime);
                variableJumpTimer -= Time.deltaTime;
            }
        }
        if (InputManager.GetJumpUp())
        {
            isJumping = false;
        }

        if (InputManager.GetSlideDown())
        {
            if (isGrounded)
            {
                StartSlide();
            }
            else
            {
                float h = InputManager.GetHorizontal();
                if (Mathf.Abs(h) > 0.1f && dashTimer <= 0f)
                {
                    // Slide with horizontal input becomes an air dash.
                    TryAirDash(Mathf.Sign(h));
                }
                else
                {
                    // Otherwise buffer the slide for landing and dive downward.
                    slideBufferTimer = slideBufferTime;
                    airDivePending = true;
                    rb.velocity += Physics2D.gravity.normalized * airDiveForce;
                }
            }
        }

        if (slideBufferTimer > 0f)
        {
            if (isGrounded && !isSliding)
            {
                StartSlide();
                slideBufferTimer = 0f;
                airDivePending = false;
            }
            else
            {
                slideBufferTimer -= Time.deltaTime;
            }
        }

        if (isSliding)
        {
            // Countdown slide duration and revert when it expires
            slideTimer -= Time.deltaTime;
            if (InputManager.GetSlideUp())
            {
                // Allow the player to end the slide early for greater control
                slideTimer = 0f;
                EndSlide();
            }
            else if (slideTimer <= 0f)
            {
                EndSlide();
            }
        }

        // Apply additional gravity so falling feels responsive and short hops
        // are immediately affected when the jump key is released. Holding the
        // down input increases the effect for a fast fall.
        ApplyEnhancedGravity(Time.deltaTime, InputManager.GetDown());
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
            TutorialManager.Instance?.RegisterJump();
            rb.velocity = new Vector2(rb.velocity.x, 0f);
            rb.AddForce(Vector2.up * jumpForce);
            isJumping = true;
            variableJumpTimer = variableJumpTime;
            anim?.SetTrigger("Jump");
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySound(jumpClip);
            }
            // Provide haptic feedback so jumps feel responsive.
            InputManager.TriggerRumble(0.5f, 0.1f);
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
            TutorialManager.Instance?.RegisterSlide();
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
    /// Applies a horizontal impulse while airborne. Used when the player taps
    /// the slide key with horizontal input. Cooldown prevents repeated dashes.
    /// </summary>
    /// <param name="direction">-1 for left, 1 for right.</param>
    void TryAirDash(float direction)
    {
        if (dashTimer > 0f)
            return;

        rb.velocity = new Vector2(0f, rb.velocity.y); // clear existing x speed
        rb.AddForce(new Vector2(direction * dashForce, 0f), ForceMode2D.Impulse);
        dashTimer = dashCooldown;
    }

    /// <summary>
    /// Applies additional gravity for better jump feel. When moving in the
    /// direction of gravity the fall multiplier speeds up descent. If the jump
    /// button is released while rising, the low jump multiplier shortens the
    /// hop. Called from <see cref="Update"/>.
    /// </summary>
    /// <param name="deltaTime">Frame time step.</param>
    /// <param name="fastFall">When true the down input is held and additional
    /// gravity is applied for a faster descent.</param>
    void ApplyEnhancedGravity(float deltaTime, bool fastFall)
    {
        Vector2 gravity = Physics2D.gravity;
        Vector2 gravityDir = gravity.normalized;
        float velAlongGravity = Vector2.Dot(rb.velocity, gravityDir);

        if (velAlongGravity > 0f)
        {
            // Falling: accelerate to make descents snappier.
            float multiplier = fallGravityMultiplier;
            if (fastFall)
            {
                multiplier *= fastFallGravityMultiplier;
            }
            rb.velocity += gravityDir * gravity.magnitude * (multiplier - 1f) * deltaTime;
        }
        else if (velAlongGravity < 0f && !InputManager.GetJump())
        {
            // Rising without holding jump: apply extra downward force so short
            // hops feel responsive.
            rb.velocity += gravityDir * gravity.magnitude * (lowJumpGravityMultiplier - 1f) * deltaTime;
        }
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
            // Strong rumble feedback on taking damage.
            InputManager.TriggerRumble(1f, 0.3f);
            if (GameManager.Instance != null && !GameManager.Instance.IsGameOver())
            {
                GameManager.Instance.GameOver();
            }
        }
    }
}
