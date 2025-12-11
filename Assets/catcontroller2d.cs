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
    public float coyoteTime = 0.12f;
    public float jumpBuffer = 0.12f;
    public float cutJumpGravityMult = 2f;
    public float fallGravityMult = 1.3f;

    [Header("Ground Check")]
    public LayerMask groundMask;
    public float checkRadius = 0.08f;
    public Vector2 checkOffset = new Vector2(0f, -0.5f);

    [Header("Animation (Jump Once + Mid-Air Hold, No Controller Changes)")]
    public Animator animator;
    public string jumpStateName = "CatJump";
    [Range(0f, 1f)] public float holdAtNormalizedTime = 0.5f;
    public bool autoFindAnimatorOnChildren = true;
    public float groundCancelGrace = 0.06f;

    [Header("Walk / Idle Animation")]
    public string walkStateName = "CatWalk";
    public string idleStateName = "CatIdle";

    [Header("Idle Visual (without Animator)")]
    public SpriteRenderer spriteRenderer;
    public Sprite idleSprite;

    [Header("Input")]
    public KeyCode jumpKey = KeyCode.Space;

    Rigidbody2D rb;
    Collider2D col;

    float xInput;
    bool isGrounded;
    float coyoteCounter;
    float bufferCounter;
    bool jumpHeld;

    bool jumpAnimPlaying;
    bool holdingMidFrame;
    bool animatorEnabledByUs;

    float groundCancelTimer;
    int jumpStateHash;
    int walkStateHash;
    int idleStateHash;

    Vector2 GetVel() => rb.linearVelocity;
    void SetVel(Vector2 v)
    {
        rb.linearVelocity = v;
        try { rb.GetType().GetProperty("linearVelocity")?.SetValue(rb, v, null); } catch { }
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        if (rb.gravityScale < 2f) rb.gravityScale = 3.5f;

        if (!animator && autoFindAnimatorOnChildren)
            animator = GetComponentInChildren<Animator>();
        if (!spriteRenderer)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (spriteRenderer && idleSprite)
            spriteRenderer.sprite = idleSprite;

        jumpAnimPlaying = false;
        holdingMidFrame = false;
        animatorEnabledByUs = false;
        groundCancelTimer = 0f;
        jumpStateHash = Animator.StringToHash(jumpStateName);
        walkStateHash = 0;
        idleStateHash = 0;
    }

    void Start()
    {
        if (animator)
            animator.enabled = true;
    }

    void Update()
    {
        xInput = FindObjectOfType<PowerupSystem>().ProcessInput(Input.GetAxisRaw("Horizontal"));
        if (Input.GetButtonDown("Jump") || Input.GetKeyDown(jumpKey))
            bufferCounter = jumpBuffer;
        jumpHeld = Input.GetButton("Jump") || Input.GetKey(jumpKey);

        if (spriteRenderer && Mathf.Abs(xInput) > 0.01f)
            spriteRenderer.flipX = xInput > 0f;

        Bounds b = col.bounds;
        Vector2 boxCenter = new Vector2(b.center.x, b.min.y + 0.02f);
        Vector2 boxSize = new Vector2(b.size.x * 0.9f, 0.08f);
        float castDist = 0.04f;
        RaycastHit2D hit = Physics2D.BoxCast(boxCenter, boxSize, 0f, Vector2.down, castDist, groundMask);
        isGrounded = hit.collider != null;

        if (groundCancelTimer > 0f) groundCancelTimer -= Time.deltaTime;

        if (isGrounded) coyoteCounter = coyoteTime;
        else coyoteCounter -= Time.deltaTime;
        if (bufferCounter > 0f) bufferCounter -= Time.deltaTime;

        if (bufferCounter > 0f && (isGrounded || coyoteCounter > 0f))
        {
            Jump();
            bufferCounter = 0f;
        }

        var v = GetVel();
        if (v.y > 0f && !jumpHeld)
        {
            v.y *= 0.98f;
            SetVel(v);
            rb.gravityScale = 3.5f * cutJumpGravityMult;
        }
        else if (v.y < 0f && !isGrounded)
        {
            rb.gravityScale = 3.5f * fallGravityMult;
        }
        else
        {
            rb.gravityScale = 3.5f;
        }

        HandleJumpAnimation();
        HandleWalkIdleAnimation();
    }

    void FixedUpdate()
    {
        var v = GetVel();
        float target = xInput * moveSpeed;
        float a = (Mathf.Abs(target) > 0.01f) ? accel : decel;
        if (!isGrounded) a *= airControl;

        v.x = Mathf.MoveTowards(v.x, target, a * Time.fixedDeltaTime);
        SetVel(v);
    }

    void Jump()
    {
        var v = GetVel();
        v.y = 0f;
        SetVel(v);
        rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
        coyoteCounter = 0f;

        if (animator && animator.runtimeAnimatorController)
        {
            bool wasEnabled = animator.enabled;
            if (!wasEnabled)
            {
                animator.enabled = true;
                animator.Rebind();
                animator.Update(0f);
            }
            animatorEnabledByUs = !wasEnabled;
            animator.speed = 1f;
            if (jumpStateHash == 0) jumpStateHash = Animator.StringToHash(jumpStateName);
            animator.Play(jumpStateHash, 0, 0f);
            jumpAnimPlaying = true;
            holdingMidFrame = false;
            groundCancelTimer = groundCancelGrace;
        }
    }

    void HandleJumpAnimation()
    {
        if (!animator) return;

        if (isGrounded && jumpAnimPlaying && groundCancelTimer <= 0f)
        {
            animator.speed = 1f;
            jumpAnimPlaying = false;
            holdingMidFrame = false;

            if (animatorEnabledByUs)
            {
                animatorEnabledByUs = false;

                if (spriteRenderer && idleSprite && !animator.runtimeAnimatorController)
                    spriteRenderer.sprite = idleSprite;
            }
            return;
        }

        if (!jumpAnimPlaying) return;

        if (!isGrounded && animator.enabled)
        {
            AnimatorStateInfo st = animator.GetCurrentAnimatorStateInfo(0);
            if (st.shortNameHash == jumpStateHash || st.IsName(jumpStateName))
            {
                if (!holdingMidFrame && st.normalizedTime >= holdAtNormalizedTime)
                {
                    animator.speed = 0f;
                    holdingMidFrame = true;
                }
            }
            else
            {
                animator.speed = 1f;
                holdingMidFrame = false;
            }
        }
    }

    void HandleWalkIdleAnimation()
    {
        if (!animator || !animator.runtimeAnimatorController) return;
        if (jumpAnimPlaying) return;

        bool moving = Mathf.Abs(xInput) > 0.01f && isGrounded;

        if (moving)
        {
            if (!string.IsNullOrEmpty(walkStateName))
            {
                if (walkStateHash == 0) walkStateHash = Animator.StringToHash(walkStateName);
                AnimatorStateInfo st = animator.GetCurrentAnimatorStateInfo(0);
                if (st.shortNameHash != walkStateHash)
                {
                    animator.speed = 1f;
                    animator.Play(walkStateHash, 0, 0f);
                }
            }
        }
        else
        {
            if (!string.IsNullOrEmpty(idleStateName))
            {
                if (idleStateHash == 0) idleStateHash = Animator.StringToHash(idleStateName);
                AnimatorStateInfo st = animator.GetCurrentAnimatorStateInfo(0);
                if (st.shortNameHash != idleStateHash)
                {
                    animator.speed = 1f;
                    animator.Play(idleStateHash, 0, 0f);
                }
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        if (col)
        {
            Bounds b = col.bounds;
            Vector2 boxCenter = new Vector2(b.center.x, b.min.y + 0.02f);
            Vector2 boxSize = new Vector2(b.size.x * 0.9f, 0.08f);
            Gizmos.DrawWireCube(boxCenter, boxSize);
        }
        else
        {
            Vector2 origin = (Vector2)transform.position + checkOffset;
            Gizmos.DrawWireSphere(origin, checkRadius);
        }
    }
}
