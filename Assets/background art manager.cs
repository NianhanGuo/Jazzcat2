using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// BackgroundArtManager v2
/// - Keeps ALL prior behavior (background color lerp + random burst)
/// - Adds multiple generative patterns. Each time a note is collected, one pattern
///   is chosen at random so the visuals feel different every time.
/// - No other scripts need to change. FallingNote just calls OnNoteCollected().
/// </summary>
public class BackgroundArtManager : MonoBehaviour
{
    public static BackgroundArtManager Instance { get; private set; }

    [Header("Camera & Color")]
    public Camera targetCamera;               // Assign Main Camera
    public Color[] palette =                  // You can edit this in Inspector
    {
        new Color(0.10f,0.10f,0.14f), // deep blue
        new Color(0.27f,0.12f,0.38f), // purple
        new Color(0.05f,0.25f,0.25f), // teal
        new Color(0.30f,0.10f,0.10f), // wine red
        new Color(0.20f,0.25f,0.05f), // olive
        new Color(0.06f,0.06f,0.06f)  // charcoal
    };
    public float bgLerpDuration = 0.6f;       // Background color transition time

    [Header("Shapes")]
    [Tooltip("Provide 1+ simple SpriteRenderer-based prefabs (circle, square, triangle).")]
    public GameObject[] shapePrefabs;
    [Tooltip("Used by several effects as the baseline spawn amount.")]
    public int burstCount = 8;

    [Tooltip("Uniform scale range used by some effects.")]
    public Vector2 scaleRange = new Vector2(0.4f, 1.4f);

    [Tooltip("Extra upward drift distance used by ShapeBurst.")]
    public Vector2 extraYFloatRange = new Vector2(0.4f, 1.2f);

    [Tooltip("Lifetime (seconds) until a spawned piece fades out and self-destroys.")]
    public float shapeLifetime = 1.6f;

    [Tooltip("SpriteRenderer.sortingOrder applied to spawned shapes.")]
    public int sortingOrder = 1;

    [Header("Parent (optional)")]
    public Transform shapesParent;

    // -------- New: pattern knobs (safe defaults) --------
    [Header("Pattern Settings")]
    public int ringPoints = 14;
    public float ringRadiusMin = 2.5f;
    public float ringRadiusMax = 5.5f;

    public int spiralPoints = 18;
    public float spiralTurns = 1.5f;          // how many rotations in spiral
    public float spiralRadius = 5.0f;

    public int gridCols = 6;
    public int gridRows = 4;
    public float gridPadding = 0.8f;          // spacing factor

    public int raysCount = 10;                // number of radial rays
    public float raysLength = 6f;             // visual length (scale x)
    public float raysWidth = 0.25f;           // visual width (scale y)

    public float sweepRows = 4f;              // how many rows in a sweep
    public float sweepJitter = 0.6f;          // randomness on sweep lines

    // internal
    Color _bgLerpFrom, _bgLerpTo;
    float _bgLerpT;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (targetCamera == null) targetCamera = Camera.main;
    }

    /// <summary>
    /// Call this once each time a note is collected.
    /// </summary>
    public void OnNoteCollected()
    {
        LerpBackgroundColor();
        PlayRandomPattern();
    }

    // ---------------------- Background color ----------------------
    void LerpBackgroundColor()
    {
        if (targetCamera == null || palette == null || palette.Length == 0) return;

        _bgLerpFrom = targetCamera.backgroundColor;
        _bgLerpTo   = palette[Random.Range(0, palette.Length)];
        _bgLerpT    = 0f;
        StopCoroutine(nameof(BgLerpRoutine));
        StartCoroutine(nameof(BgLerpRoutine));
    }

    IEnumerator BgLerpRoutine()
    {
        float dur = Mathf.Max(0.0001f, bgLerpDuration);
        while (_bgLerpT < 1f)
        {
            _bgLerpT += Time.deltaTime / dur;
            if (targetCamera != null)
                targetCamera.backgroundColor = Color.Lerp(_bgLerpFrom, _bgLerpTo, _bgLerpT);
            yield return null;
        }
    }

    // ---------------------- Patterns ----------------------
    enum Pattern { Burst, Ring, Spiral, Rays, Sweep, Grid }

    void PlayRandomPattern()
    {
        if (shapePrefabs == null || shapePrefabs.Length == 0 || targetCamera == null)
            return;

        var choice = (Pattern)Random.Range(0, System.Enum.GetValues(typeof(Pattern)).Length);
        switch (choice)
        {
            case Pattern.Burst:  Pattern_Burst();  break;
            case Pattern.Ring:   Pattern_Ring();   break;
            case Pattern.Spiral: Pattern_Spiral(); break;
            case Pattern.Rays:   Pattern_Rays();   break;
            case Pattern.Sweep:  Pattern_Sweep();  break;
            case Pattern.Grid:   Pattern_Grid();   break;
        }
    }

    // 1) classic burst (kept from the original)
    void Pattern_Burst()
    {
        Bounds b = ViewBounds();
        for (int i = 0; i < burstCount; i++)
        {
            Vector3 pos = new Vector3(
                Random.Range(b.min.x, b.max.x),
                Random.Range(b.min.y, b.max.y),
                0f
            );
            float s = Random.Range(scaleRange.x, scaleRange.y);
            float spin = Random.Range(-90f, 90f);
            float up = Random.Range(extraYFloatRange.x, extraYFloatRange.y);
            SpawnShape(pos, Random.Range(0f, 360f), new Vector2(s, s), up, spin);
        }
    }

    // 2) ring of shapes around the screen center
    void Pattern_Ring()
    {
        Bounds b = ViewBounds();
        Vector3 c = b.center;
        float r = Random.Range(ringRadiusMin, ringRadiusMax);
        int n = Mathf.Max(3, ringPoints);

        for (int i = 0; i < n; i++)
        {
            float t = (i / (float)n) * Mathf.PI * 2f;
            Vector3 pos = c + new Vector3(Mathf.Cos(t) * r, Mathf.Sin(t) * r, 0f);
            float s = Random.Range(scaleRange.x, scaleRange.y);
            float spin = Random.Range(-60f, 60f);
            SpawnShape(pos, t * Mathf.Rad2Deg, new Vector2(s, s), 0.6f, spin);
        }
    }

    // 3) spiral from center outward
    void Pattern_Spiral()
    {
        Bounds b = ViewBounds();
        Vector3 c = b.center;
        int n = Mathf.Max(6, spiralPoints);
        float turns = Mathf.Max(0.25f, spiralTurns);

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)(n - 1);
            float ang = t * turns * Mathf.PI * 2f;
            float r = t * spiralRadius;
            Vector3 pos = c + new Vector3(Mathf.Cos(ang) * r, Mathf.Sin(ang) * r, 0f);

            float s = Mathf.Lerp(scaleRange.x, scaleRange.y, t);
            float spin = Mathf.Lerp(-120f, 120f, t);
            SpawnShape(pos, ang * Mathf.Rad2Deg, new Vector2(s, s), 0.8f, spin);
        }
    }

    // 4) radial rays (long rectangles). Works even if your prefab is a circle—scale x/y stretches it.
    void Pattern_Rays()
    {
        Bounds b = ViewBounds();
        Vector3 c = b.center;
        int n = Mathf.Max(4, raysCount);

        for (int i = 0; i < n; i++)
        {
            float t = (i / (float)n) * Mathf.PI * 2f + Random.Range(-0.12f, 0.12f);
            float angleDeg = t * Mathf.Rad2Deg;
            // Scale: long in X, thin in Y
            Vector2 scl = new Vector2(raysLength * Random.Range(0.75f, 1.15f), raysWidth * Random.Range(0.7f, 1.3f));
            SpawnShape(c, angleDeg, scl, 0.2f, 0f);
        }
    }

    // 5) horizontal sweep—several rows sweeping upward with jitter
    void Pattern_Sweep()
    {
        Bounds b = ViewBounds();
        float rows = Mathf.Max(1f, sweepRows);
        int perRow = Mathf.Max(3, burstCount / 2);

        for (int r = 0; r < Mathf.CeilToInt(rows); r++)
        {
            float y = Mathf.Lerp(b.min.y, b.max.y, r / Mathf.Max(1f, rows - 1f));
            for (int i = 0; i < perRow; i++)
            {
                float x = Mathf.Lerp(b.min.x, b.max.x, i / (float)(perRow - 1));
                x += Random.Range(-sweepJitter, sweepJitter);
                y += Random.Range(-sweepJitter * 0.3f, sweepJitter * 0.3f);

                float s = Random.Range(scaleRange.x * 0.8f, scaleRange.y);
                float up = Random.Range(extraYFloatRange.x * 0.6f, extraYFloatRange.y);
                float spin = Random.Range(-40f, 40f);
                SpawnShape(new Vector3(x, y, 0f), 0f, new Vector2(s, s), up, spin);
            }
        }
    }

    // 6) sparse grid across the view
    void Pattern_Grid()
    {
        Bounds b = ViewBounds();
        int cols = Mathf.Max(2, gridCols);
        int rows = Mathf.Max(2, gridRows);

        for (int r = 0; r < rows; r++)
        {
            float ty = r / (float)(rows - 1);
            for (int c = 0; c < cols; c++)
            {
                float tx = c / (float)(cols - 1);

                float x = Mathf.Lerp(b.min.x, b.max.x, tx);
                float y = Mathf.Lerp(b.min.y, b.max.y, ty);

                // add padding & jitter so it looks organic
                float jitterX = (Random.value - 0.5f) * gridPadding;
                float jitterY = (Random.value - 0.5f) * gridPadding;

                float s = Random.Range(scaleRange.x * 0.9f, scaleRange.y);
                float spin = Random.Range(-45f, 45f);
                SpawnShape(new Vector3(x + jitterX, y + jitterY, 0f), 0f, new Vector2(s, s), 0.5f, spin);
            }
        }
    }

    // ---------------------- Helpers ----------------------
    Bounds ViewBounds()
    {
        float ortho = targetCamera.orthographicSize;
        float h = ortho * 2f;
        float w = h * targetCamera.aspect;
        Vector3 c = targetCamera.transform.position;
        return new Bounds(c, new Vector3(w, h, 0f));
    }

    void SpawnShape(Vector3 position, float zRotationDeg, Vector2 scaleXY, float floatUp, float spin)
    {
        GameObject prefab = shapePrefabs[Random.Range(0, shapePrefabs.Length)];
        if (prefab == null) return;

        GameObject go = Instantiate(prefab, position, Quaternion.Euler(0, 0, zRotationDeg), shapesParent);

        // Non-uniform scale supported
        go.transform.localScale = new Vector3(scaleXY.x, scaleXY.y, 1f);

        // Color + order
        var sr = go.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            Color baseCol = palette[Random.Range(0, palette.Length)];
            float v = Random.Range(0.8f, 1.25f);
            sr.color = new Color(baseCol.r * v, baseCol.g * v, baseCol.b * v, 1f);
            sr.sortingOrder = sortingOrder;
        }

        // Motion/fade
        var fx = go.GetComponent<ShapeBurst>();
        if (fx == null) fx = go.AddComponent<ShapeBurst>();
        fx.life    = shapeLifetime;
        fx.floatUp = floatUp;
        fx.spin    = spin;
    }
}
