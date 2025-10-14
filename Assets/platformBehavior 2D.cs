using System.Collections;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class PlatformBehavior2D : MonoBehaviour
{
    public enum PlatformType { Normal, Cracking, Boost, Sticky, Trap }

    [Header("Type")]
    public PlatformType type = PlatformType.Normal;

    [Header("Visual Hints")]
    public Color normalColor   = Color.white;
    public Color crackingColor = new Color(1f, 0.55f, 0.15f, 1f); // orange
    public Color boostColor    = new Color(0.3f, 0.6f, 1f, 1f);   // blue
    public Color stickyColor   = new Color(0.25f, 0.9f, 0.4f, 1f);// green
    public Color trapColor     = new Color(0.05f, 0.05f, 0.05f, 1f);// black

    [Header("Cracking Settings")]
    public float warnTime = 0.9f;       // time blinking before vanish
    public float fadeOut = 0.35f;       // fade after collider off
    public float blinkSpeed = 9f;

    [Header("Boost Settings")]
    public float boostImpulse = 12f;    // upward impulse (tune to your jump)

    [Header("Sticky Settings")]
    [Tooltip("How strongly we damp X velocity while standing on Sticky.")]
    public float stickyFriction = 12f;  // higher = stickier

    [Header("Detection")]
    public string playerTag = "Player";

    // NOTE: Now we support any prefab shape: sprite can be on this object or children.
    SpriteRenderer[] renderers;
    BoxCollider2D col;
    bool crackingArmed;
    bool trapTriggered;
    Transform playerRoot;

    void Awake()
    {
        col = GetComponent<BoxCollider2D>();
        renderers = GetComponentsInChildren<SpriteRenderer>(true);
        ApplyTypeColor();
    }

    void OnEnable()
    {
        // In case pooled or modified at runtime
        ApplyTypeColor();
    }

    // Let the spawner call this after it sets 'type'
    public void ApplyTypeColor()
    {
        if (renderers == null || renderers.Length == 0) return;

        Color target = normalColor;
        switch (type)
        {
            case PlatformType.Cracking: target = crackingColor; break;
            case PlatformType.Boost:    target = boostColor;    break;
            case PlatformType.Sticky:   target = stickyColor;   break;
            case PlatformType.Trap:     target = trapColor;     break;
            default:                    target = normalColor;   break;
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i]) renderers[i].color = target;
        }
    }

#if UNITY_EDITOR
    // So you can see the color in-editor when you change fields
    void OnValidate()
    {
        if (!Application.isPlaying)
        {
            renderers = GetComponentsInChildren<SpriteRenderer>(true);
            ApplyTypeColor();
        }
    }
#endif

    // ---------------- Player contact helpers ----------------
    bool IsPlayerCollider(Collider2D other, out Rigidbody2D playerRB, out Transform root)
    {
        playerRB = null;
        root = other.attachedRigidbody ? other.attachedRigidbody.transform : other.transform;
        if (root == null) return false;

        bool isPlayer = root.CompareTag(playerTag) ||
                        root.GetComponentInParent<CatController2D>() != null;

        if (!isPlayer) return false;

        playerRB = root.GetComponent<Rigidbody2D>();
        return playerRB != null;
    }

    // -------- Collisions (platform collider is NOT a trigger) --------
    void OnCollisionEnter2D(Collision2D c)
    {
        if (!IsPlayerCollider(c.collider, out var playerRB, out var root)) return;

        switch (type)
        {
            case PlatformType.Cracking:
                if (!crackingArmed) StartCoroutine(CrackRoutine());
                break;

            case PlatformType.Boost:
                // zero-out downward speed then add impulse up
                var v = playerRB.linearVelocity;           // use velocity (not linearVelocity)
                if (v.y < 0f) v.y = 0f;
                playerRB.linearVelocity = v;
                playerRB.AddForce(Vector2.up * boostImpulse, ForceMode2D.Impulse);
                StartCoroutine(FlashOnce(0.12f));
                break;

            case PlatformType.Trap:
                if (!trapTriggered) StartCoroutine(TrapVanish());
                break;
        }
    }

    void OnCollisionStay2D(Collision2D c)
    {
        if (type != PlatformType.Sticky) return;
        if (!IsPlayerCollider(c.collider, out var playerRB, out var root)) return;

        // damp horizontal velocity while on sticky
        var v = playerRB.linearVelocity;
        float t = Mathf.Clamp01(Time.deltaTime * stickyFriction);
        v.x = Mathf.Lerp(v.x, 0f, t);
        playerRB.linearVelocity = v;

        playerRoot = root;
    }

    void OnCollisionExit2D(Collision2D c)
    {
        if (type == PlatformType.Sticky && playerRoot != null)
            playerRoot = null;
    }

    // ---------------- Behaviors ----------------
    IEnumerator CrackRoutine()
    {
        crackingArmed = true;

        float t = 0f;
        // blink across all renderers
        while (t < warnTime)
        {
            t += Time.deltaTime;
            float s = (Mathf.Sin(Time.time * blinkSpeed) + 1f) * 0.5f;
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (!r) continue;
                var c = Color.Lerp(r.color, Color.white, s);
                c.a = r.color.a;
                r.color = c;
            }
            yield return null;
        }

        if (col) col.enabled = false;

        // fade out
        float f = 0f;
        while (f < 1f)
        {
            f += Time.deltaTime / Mathf.Max(0.0001f, fadeOut);
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (!r) continue;
                var c = r.color; c.a = Mathf.Lerp(c.a, 0f, f);
                r.color = c;
            }
            yield return null;
        }
        Destroy(gameObject);
    }

    IEnumerator TrapVanish()
    {
        trapTriggered = true;

        yield return FlashOnce(0.10f);

        if (col) col.enabled = false;

        // fast fade
        for (float f = 0; f < 1f; f += Time.deltaTime / 0.2f)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (!r) continue;
                var c = r.color; c.a = 1f - f;
                r.color = c;
            }
            yield return null;
        }
        Destroy(gameObject);
    }

    IEnumerator FlashOnce(float dur)
    {
        float t = 0f;
        // cache original colors
        Color[] originals = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++) originals[i] = renderers[i] ? renderers[i].color : Color.white;

        while (t < dur)
        {
            t += Time.deltaTime;
            float s = (Mathf.Sin(t / dur * Mathf.PI * 2f) + 1f) * 0.5f;
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (!r) continue;
                r.color = Color.Lerp(originals[i], Color.white, s);
            }
            yield return null;
        }
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i]) renderers[i].color = originals[i];
        }
    }
}
