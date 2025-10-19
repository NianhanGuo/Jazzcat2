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
    public float coyoteTime = 0.12f;      // 允许离地后短暂起跳
    public float jumpBuffer = 0.12f;      // 提前按跳跃的缓冲
    public float cutJumpGravityMult = 2f; // 松开跳跃键时更强重力（小跳）
    public float fallGravityMult = 1.3f;  // 下落更顺滑

    [Header("Ground Check")]
    public LayerMask groundMask;          // 记得把地面物体的 Layer 设为 Ground
    public float checkRadius = 0.08f;     // (仅作占位，不再使用圆形检测)
    public Vector2 checkOffset = new Vector2(0f, -0.5f); // (占位，不再使用)

    [Header("Animation (Jump Once + Mid-Air Hold, No Controller Changes)")]
    public Animator animator;                 // 猫的 Animator（可在子物体）
    public string jumpStateName = "CatJump";  // 与 Animator 的跳跃 State 名完全一致
    [Range(0f, 1f)] public float holdAtNormalizedTime = 0.5f; // 空中卡住的动画进度
    public bool autoFindAnimatorOnChildren = true;            // 自动查找

    [Header("Idle Visual (without Animator)")]
    public SpriteRenderer spriteRenderer; // 用来显示待机静态图
    public Sprite idleSprite;             // 待机静态图（建议用站立帧）

    [Header("Input (旧输入系统 + 兜底按键)")]
    public KeyCode jumpKey = KeyCode.Space;

    Rigidbody2D rb;
    Collider2D col;

    float xInput;
    bool isGrounded;
    float coyoteCounter;
    float bufferCounter;
    bool jumpHeld;

    // 动画控制状态
    bool jumpAnimPlaying;        // 本次跳跃动画是否正在播放
    bool holdingMidFrame;        // 是否已在空中把动画定格在中间帧
    bool animatorEnabledByUs;    // 起跳时是否由我们启用（落地后关回去）

    // ---- 统一速度读写（保证兼容性；主用 rb.velocity） ----
    Vector2 GetVel() => rb.linearVelocity;
    void SetVel(Vector2 v)
    {
        rb.linearVelocity = v;
        // 若工程里另有 linearVelocity，不影响；没有就忽略
        try { rb.GetType().GetProperty("linearVelocity")?.SetValue(rb, v, null); } catch { }
    }
    // -----------------------------------------------------

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

        // 开场禁用 Animator，避免默认状态自动播放
        if (animator) animator.enabled = false;

        // 显示待机静态图
        if (spriteRenderer && idleSprite)
            spriteRenderer.sprite = idleSprite;

        jumpAnimPlaying = false;
        holdingMidFrame = false;
        animatorEnabledByUs = false;
    }

    void Start()
    {
        // 双保险
        if (animator) animator.enabled = false;
    }

    void Update()
    {
        // 1) 输入
        xInput = Input.GetAxisRaw("Horizontal");
        if (Input.GetButtonDown("Jump") || Input.GetKeyDown(jumpKey))
            bufferCounter = jumpBuffer;
        jumpHeld = Input.GetButton("Jump") || Input.GetKey(jumpKey);

        // 2) 落地检测（稳健版：脚下薄盒 BoxCast）
        // 贴合自身碰撞体宽度，在脚下很薄的一条区域向下探测一点点
        Bounds b = col.bounds;
        Vector2 boxCenter = new Vector2(b.center.x, b.min.y + 0.02f);
        Vector2 boxSize   = new Vector2(b.size.x * 0.9f, 0.08f);
        float   castDist  = 0.04f;
        RaycastHit2D hit = Physics2D.BoxCast(boxCenter, boxSize, 0f, Vector2.down, castDist, groundMask);
        isGrounded = hit.collider != null;

        // 3) 土狼/缓冲计时
        if (isGrounded) coyoteCounter = coyoteTime;
        else            coyoteCounter -= Time.deltaTime;
        if (bufferCounter > 0f) bufferCounter -= Time.deltaTime;

        // 4) 尝试起跳（允许地面或土狼时间内）
        if (bufferCounter > 0f && (isGrounded || coyoteCounter > 0f))
        {
            Jump();
            bufferCounter = 0f;
        }

        // 5) 跳跃手感与下落
        var v = GetVel();
        if (v.y > 0f && !jumpHeld)
        {
            v.y *= 0.98f;            // 轻微削顶
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

        // 6) 跳跃动画：只播放一次 + 空中定格中间帧
        HandleJumpAnimation();
    }

    void FixedUpdate()
    {
        // 水平移动：带加/减速；空中控制降低
        var v = GetVel();
        float target = xInput * moveSpeed;
        float a = (Mathf.Abs(target) > 0.01f) ? accel : decel;
        if (!isGrounded) a *= airControl;

        v.x = Mathf.MoveTowards(v.x, target, a * Time.fixedDeltaTime);
        SetVel(v);
    }

    void Jump()
    {
        // 垂直速度清零后施加冲力
        var v = GetVel();
        v.y = 0f;
        SetVel(v);
        rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
        coyoteCounter = 0f;

        // 启用 Animator 并播放一次跳跃动画
        if (animator)
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
            animator.Play(jumpStateName, 0, 0f); // 从头播放
            jumpAnimPlaying = true;
            holdingMidFrame = false;
        }
    }

    void HandleJumpAnimation()
    {
        if (!animator) return;

        // 落地：恢复速度 & 关回 Animator & 显示待机图
        if (isGrounded && jumpAnimPlaying)
        {
            animator.speed = 1f;
            jumpAnimPlaying = false;
            holdingMidFrame = false;

            if (animatorEnabledByUs)
            {
                animator.enabled = false;
                animatorEnabledByUs = false;

                if (spriteRenderer && idleSprite)
                    spriteRenderer.sprite = idleSprite;
            }
            return;
        }

        if (!jumpAnimPlaying) return;

        // 空中定格中间帧
        if (!isGrounded && animator.enabled)
        {
            AnimatorStateInfo st = animator.GetCurrentAnimatorStateInfo(0);

            if (st.IsName(jumpStateName))
            {
                if (!holdingMidFrame && st.normalizedTime >= holdAtNormalizedTime)
                {
                    animator.speed = 0f; // 停住这一帧
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

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        if (col)
        {
            Bounds b = col.bounds;
            Vector2 boxCenter = new Vector2(b.center.x, b.min.y + 0.02f);
            Vector2 boxSize   = new Vector2(b.size.x * 0.9f, 0.08f);
            Gizmos.DrawWireCube(boxCenter, boxSize);
        }
        else
        {
            // 旧的圆形可视化（备用）
            Vector2 origin = (Vector2)transform.position + checkOffset;
            Gizmos.DrawWireSphere(origin, checkRadius);
        }
    }
}
