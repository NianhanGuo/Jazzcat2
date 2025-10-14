using System.Collections;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class TimedCrackingPlatform : MonoBehaviour
{
    [Header("Timing")]
    public float delayBeforeWarning = 2.0f;   // seconds before showing warning
    public float warningDuration = 2.0f;      // warning phase length
    public float fadeOutDuration = 0.6f;      // fade + disable

    [Header("Warning Look")]
    public Color warningColor = new Color(1f, 0.4f, 0.4f, 1f);
    public float blinkSpeed = 7f;             // how fast it blinks
    public float shakeAmount = 0.06f;         // screen/world shake amplitude

    SpriteRenderer sr;
    Color baseColor;
    Vector3 startPos;
    BoxCollider2D col;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        col = GetComponent<BoxCollider2D>();
        startPos = transform.position;
        if (sr != null) baseColor = sr.color;
    }

    void OnEnable()
    {
        StartCoroutine(Lifecycle());
    }

    IEnumerator Lifecycle()
    {
        // calm phase
        yield return new WaitForSeconds(delayBeforeWarning);

        // warning phase (blink + shake)
        float t = 0f;
        while (t < warningDuration)
        {
            t += Time.deltaTime;

            // blink color
            if (sr != null)
            {
                float s = (Mathf.Sin(Time.time * blinkSpeed) + 1f) * 0.5f; // 0..1
                sr.color = Color.Lerp(baseColor, warningColor, s);
            }

            // subtle shake (local)
            float sx = (Random.value - 0.5f) * shakeAmount;
            float sy = (Random.value - 0.5f) * shakeAmount;
            transform.position = startPos + new Vector3(sx, sy, 0f);

            yield return null;
        }

        // stop shaking, reset pos
        transform.position = startPos;

        // turn off collider so player drops
        if (col != null) col.enabled = false;

        // fade out sprite while disabled
        float f = 0f;
        if (sr != null)
        {
            Color c0 = sr.color;
            while (f < 1f)
            {
                f += Time.deltaTime / Mathf.Max(0.0001f, fadeOutDuration);
                var c = Color.Lerp(c0, new Color(c0.r, c0.g, c0.b, 0f), f);
                sr.color = c;
                yield return null;
            }
        }

        // finally remove this ground
        Destroy(gameObject);
    }
}
