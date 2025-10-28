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
    public Color crackingColor = new Color(1f, 0.55f, 0.15f, 1f);
    public Color boostColor    = new Color(0.3f, 0.6f, 1f, 1f);
    public Color stickyColor   = new Color(0.25f, 0.9f, 0.4f, 1f);
    public Color trapColor     = new Color(0.05f, 0.05f, 0.05f, 1f);

    [Header("Cracking Settings")]
    public float warnTime = 0.9f;
    public float fadeOut = 0.35f;
    public float blinkSpeed = 9f;

    [Header("Boost Settings")]
    public float boostImpulse = 12f;

    [Header("Sticky Settings")]
    public float stickyFriction = 12f;

    [Header("Detection")]
    public string playerTag = "Player";

    [Header("Top Contact Gate")]
    public float topContactTolerance = 0.02f;
    public float requireDownwardVy = -0.01f;

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
        ApplyTypeColor();
    }

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
            if (renderers[i]) renderers[i].color = target;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!Application.isPlaying)
        {
            renderers = GetComponentsInChildren<SpriteRenderer>(true);
            ApplyTypeColor();
        }
    }
#endif

    bool IsPlayerCollider(Collider2D other, out Rigidbody2D playerRB, out Transform root)
    {
        playerRB = null;
        root = other.attachedRigidbody ? other.attachedRigidbody.transform : other.transform;
        if (root == null) return false;
        bool isPlayer = root.CompareTag(playerTag) || root.GetComponentInParent<CatController2D>() != null;
        if (!isPlayer) return false;
        playerRB = root.GetComponent<Rigidbody2D>();
        return playerRB != null;
    }

    bool LandedFromAbove(Collision2D c, Rigidbody2D playerRB)
    {
        if (!col) return false;
        var platformTop = col.bounds.max.y;
        var playerMinY = c.collider.bounds.min.y;
        bool fromAboveByBounds = playerMinY >= platformTop - topContactTolerance;
        bool downward = playerRB.linearVelocity.y <= requireDownwardVy;
        return fromAboveByBounds && downward;
    }

    void OnCollisionEnter2D(Collision2D c)
    {
        if (!IsPlayerCollider(c.collider, out var playerRB, out var root)) return;

        switch (type)
        {
            case PlatformType.Cracking:
                if (!crackingArmed && LandedFromAbove(c, playerRB))
                    StartCoroutine(CrackRoutine());
                break;

            case PlatformType.Boost:
                var v = playerRB.linearVelocity;
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

    IEnumerator CrackRoutine()
    {
        crackingArmed = true;
        float t = 0f;
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
            if (renderers[i]) renderers[i].color = originals[i];
    }
}
