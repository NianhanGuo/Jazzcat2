using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class CatController2D : MonoBehaviour
{
    [Header("Move")]
    public float moveSpeed = 6f;
    public float accel = 60f;
    public float decel = 70f;
    public float airControl = 0.6f;

    [Header("Jump")]
    public float jumpForce = 11f;
    public float coyoteTime = 0.12f;      // allow jump shortly after leaving ground
    public float jumpBuffer = 0.12f;      // allow jump pressed slightly before landing
    public float cutJumpGravityMult = 2f; // higher gravity when releasing jump early

    [Header("Ground Check")]
    public LayerMask groundMask;          // set to "Ground" in Inspector
    public float checkRadius = 0.08f;
    public Vector2 checkOffset = new Vector2(0f, -0.5f); // from center of collider

    Rigidbody2D rb;
    Collider2D col;

    float xInput;
    bool isGrounded;
    float coyoteCounter;
    float bufferCounter;
    bool jumpHeld;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        // basic RB setup (you can tweak in Inspector)
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        if (rb.gravityScale < 2f) rb.gravityScale = 3.5f;
    }

    void Update()
    {
        // 1) Input
        xInput = Input.GetAxisRaw("Horizontal"); // A/D or ←/→
        if (Input.GetButtonDown("Jump")) bufferCounter = jumpBuffer;
        jumpHeld = Input.GetButton("Jump");

        // 2) Ground check (simple circle below feet)
        Vector2 origin = (Vector2)transform.position + checkOffset;
        isGrounded = Physics2D.OverlapCircle(origin, checkRadius, groundMask);

        // 3) Coyote / Buffer timers
        if (isGrounded) coyoteCounter = coyoteTime;
        else            coyoteCounter -= Time.deltaTime;

        if (bufferCounter > 0) bufferCounter -= Time.deltaTime;

        // 4) Try jump
        if (bufferCounter > 0 && coyoteCounter > 0)
        {
            Jump();
            bufferCounter = 0;
        }

        // 5) Better-jump: cut height if jump is released on the way up
        if (!jumpHeld && rb.linearVelocity.y > 0f)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * 0.98f);
            rb.gravityScale = Mathf.Max(3.5f, rb.gravityScale) * cutJumpGravityMult;
        }
        else
        {
            // restore normal gravity while falling or grounded
            rb.gravityScale = Mathf.Max(3.5f, rb.gravityScale / cutJumpGravityMult);
        }
    }

    void FixedUpdate()
    {
        // Horizontal movement with accel/decel; less control in air
        float target = xInput * moveSpeed;
        float a = (Mathf.Abs(target) > 0.01f) ? accel : decel;
        if (!isGrounded) a *= airControl;

        float newX = Mathf.MoveTowards(rb.linearVelocity.x, target, a * Time.fixedDeltaTime);
        rb.linearVelocity = new Vector2(newX, rb.linearVelocity.y);
    }

    void Jump()
    {
        // reset vertical speed so short hops are consistent
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
        rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
        coyoteCounter = 0f;
    }

    // gizmo to see ground check in Scene view
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Vector2 origin = (Vector2)transform.position + checkOffset;
        Gizmos.DrawWireSphere(origin, checkRadius);
    }
}
