using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BackgroundArtManager : MonoBehaviour
{
    public static BackgroundArtManager Instance { get; private set; }

    [Header("Camera & Color")]
    public Camera targetCamera;
    public Color[] palette =
    {
        new Color(0.10f,0.10f,0.14f),
        new Color(0.27f,0.12f,0.38f),
        new Color(0.05f,0.25f,0.25f),
        new Color(0.30f,0.10f,0.10f),
        new Color(0.20f,0.25f,0.05f),
        new Color(0.06f,0.06f,0.06f)
    };
    public float bgLerpDuration = 0.6f;

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

    [Header("Pattern Settings")]
    public int ringPoints = 14;
    public float ringRadiusMin = 2.5f;
    public float ringRadiusMax = 5.5f;

    public int spiralPoints = 18;
    public float spiralTurns = 1.5f;
    public float spiralRadius = 5.0f;

    public int gridCols = 6;
    public int gridRows = 4;
    public float gridPadding = 0.8f;

    public int raysCount = 10;
    public float raysLength = 6f;
    public float raysWidth = 0.25f;

    public float sweepRows = 4f;
    public float sweepJitter = 0.6f;

    [Header("Always-Visible Safeguards")]
    [Tooltip("Clamp all spawn positions inside camera view with this margin (world units).")]
    public float viewMargin = 0.25f;
    [Tooltip("Guarantee at least this scale on X/Y so shapes are not microscopic.")]
    public float minVisibleScale = 0.35f;
    [Tooltip("Force spawned shapes to this sorting layer (leave empty to keep prefab's layer).")]
    public string forceSortingLayerName = "";
    [Tooltip("If true, override sortingOrder to 'sortingOrder' value below for all spawned shapes.")]
    public bool forceSortingOrder = false;
    [Tooltip("If true, ensure final alpha = 1 for spawned SpriteRenderers.")]
    public bool forceOpaque = true;

    // internal
    Color _bgLerpFrom, _bgLerpTo;
    float _bgLerpT;
    int _spawnedThisCall;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (targetCamera == null) targetCamera = Camera.main;
    }

    public void OnNoteCollected()
    {
        LerpBackgroundColor();

        _spawnedThisCall = 0;
        PlayRandomPattern();

        // Fallback: if nothing spawned (should be rare), do a tiny burst at center
        if (_spawnedThisCall == 0)
        {
            Pattern_FallbackBurst();
        }
    }

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

    // --- Patterns ---

    void Pattern_Burst()
    {
        Bounds b = ViewBoundsInset();
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

    void Pattern_Ring()
    {
        Bounds b = ViewBoundsInset();
        Vector3 c = b.center;
        float r = Mathf.Clamp(Random.Range(ringRadiusMin, ringRadiusMax), 0.5f, Mathf.Min(b.extents.x, b.extents.y) - viewMargin);
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

    void Pattern_Spiral()
    {
        Bounds b = ViewBoundsInset();
        Vector3 c = b.center;
        int n = Mathf.Max(6, spiralPoints);
        float turns = Mathf.Max(0.25f, spiralTurns);

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)(n - 1);
            float ang = t * turns * Mathf.PI * 2f;
            float r = t * Mathf.Min(spiralRadius, Mathf.Min(b.extents.x, b.extents.y) - viewMargin);
            Vector3 pos = c + new Vector3(Mathf.Cos(ang) * r, Mathf.Sin(ang) * r, 0f);

            float s = Mathf.Lerp(scaleRange.x, scaleRange.y, t);
            float spin = Mathf.Lerp(-120f, 120f, t);
            SpawnShape(pos, ang * Mathf.Rad2Deg, new Vector2(s, s), 0.8f, spin);
        }
    }

    void Pattern_Rays()
    {
        Bounds b = ViewBoundsInset();
        Vector3 c = b.center;
        int n = Mathf.Max(4, raysCount);

        for (int i = 0; i < n; i++)
        {
            float t = (i / (float)n) * Mathf.PI * 2f + Random.Range(-0.12f, 0.12f);
            float angleDeg = t * Mathf.Rad2Deg;
            Vector2 scl = new Vector2(
                Mathf.Max(minVisibleScale, raysLength * Random.Range(0.75f, 1.15f)),
                Mathf.Max(minVisibleScale * 0.5f, raysWidth * Random.Range(0.7f, 1.3f))
            );
            SpawnShape(c, angleDeg, scl, 0.2f, 0f);
        }
    }

    void Pattern_Sweep()
    {
        Bounds b = ViewBoundsInset();
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

                Vector3 pos = ClampToBounds(new Vector3(x, y, 0f), b);
                float s = Random.Range(scaleRange.x * 0.8f, scaleRange.y);
                float up = Random.Range(extraYFloatRange.x * 0.6f, extraYFloatRange.y);
                float spin = Random.Range(-40f, 40f);
                SpawnShape(pos, 0f, new Vector2(s, s), up, spin);
            }
        }
    }

    void Pattern_Grid()
    {
        Bounds b = ViewBoundsInset();
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

                float jitterX = (Random.value - 0.5f) * gridPadding;
                float jitterY = (Random.value - 0.5f) * gridPadding;

                Vector3 pos = ClampToBounds(new Vector3(x + jitterX, y + jitterY, 0f), b);
                float s = Random.Range(scaleRange.x * 0.9f, scaleRange.y);
                float spin = Random.Range(-45f, 45f);
                SpawnShape(pos, 0f, new Vector2(s, s), 0.5f, spin);
            }
        }
    }

    void Pattern_FallbackBurst()
    {
        Bounds b = ViewBoundsInset();
        Vector3 c = b.center;
        for (int i = 0; i < Mathf.Max(3, burstCount / 2); i++)
        {
            float ang = Random.Range(0f, 360f);
            float s = Mathf.Max(minVisibleScale, Random.Range(scaleRange.x, scaleRange.y));
            SpawnShape(c, ang, new Vector2(s, s), Random.Range(0.3f, 0.8f), Random.Range(-50f, 50f));
        }
    }

    // --- Helpers ---

    Bounds ViewBounds()
    {
        float ortho = targetCamera.orthographicSize;
        float h = ortho * 2f;
        float w = h * targetCamera.aspect;
        Vector3 c = targetCamera.transform.position;
        return new Bounds(c, new Vector3(w, h, 0f));
    }

    Bounds ViewBoundsInset()
    {
        Bounds b = ViewBounds();
        b.Expand(new Vector3(-2f * viewMargin, -2f * viewMargin, 0f));
        return b;
    }

    Vector3 ClampToBounds(Vector3 p, Bounds b)
    {
        p.x = Mathf.Clamp(p.x, b.min.x, b.max.x);
        p.y = Mathf.Clamp(p.y, b.min.y, b.max.y);
        return p;
    }

    void SpawnShape(Vector3 position, float zRotationDeg, Vector2 scaleXY, float floatUp, float spin)
    {
        if (shapePrefabs == null || shapePrefabs.Length == 0) return;

        GameObject prefab = shapePrefabs[Random.Range(0, shapePrefabs.Length)];
        if (prefab == null) return;

        // clamp to visible area
        position = ClampToBounds(position, ViewBoundsInset());

        GameObject go = Instantiate(prefab, position, Quaternion.Euler(0, 0, zRotationDeg), shapesParent);

        // guarantee minimal visible scale
        float sx = Mathf.Max(minVisibleScale, scaleXY.x);
        float sy = Mathf.Max(minVisibleScale, scaleXY.y);
        go.transform.localScale = new Vector3(sx, sy, 1f);

        // color & sorting
        var sr = go.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            Color baseCol = palette[Random.Range(0, palette.Length)];
            float v = Random.Range(0.8f, 1.25f);
            Color c = new Color(baseCol.r * v, baseCol.g * v, baseCol.b * v, forceOpaque ? 1f : sr.color.a);
            sr.color = c;

            if (!string.IsNullOrEmpty(forceSortingLayerName))
            {
                sr.sortingLayerName = forceSortingLayerName;
            }
            if (forceSortingOrder)
            {
                sr.sortingOrder = sortingOrder;
            }
            else
            {
                sr.sortingOrder = Mathf.Max(sr.sortingOrder, sortingOrder);
            }
        }

        // motion/fade
        var fx = go.GetComponent<ShapeBurst>();
        if (fx == null) fx = go.AddComponent<ShapeBurst>();
        fx.life    = shapeLifetime;
        fx.floatUp = floatUp;
        fx.spin    = spin;

        _spawnedThisCall++;
    }
}
