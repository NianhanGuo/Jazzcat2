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
        new Color(0.93f,0.88f,0.82f),
        new Color(0.86f,0.70f,0.76f),
        new Color(0.55f,0.53f,0.75f),
        new Color(0.92f,0.80f,0.74f),
        new Color(0.75f,0.64f,0.86f)
    };
    public float bgLerpDuration = 0.6f;

    [Header("Shapes")]
    public GameObject[] shapePrefabs;
    public int burstCount = 8;
    public Vector2 scaleRange = new Vector2(0.4f, 1.4f);
    public Vector2 extraYFloatRange = new Vector2(0.4f, 1.2f);
    public float shapeLifetime = 1.6f;
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
    public float viewMargin = 0.25f;
    public float minVisibleScale = 0.35f;
    public string forceSortingLayerName = "";
    public bool forceSortingOrder = false;
    public bool forceOpaque = true;

    [Header("Ambient Settings")]
    public bool enableAmbient = true;
    public float ambientMinDelay = 0.25f;
    public float ambientMaxDelay = 0.7f;
    public int ambientMinBurst = 1;
    public int ambientMaxBurst = 3;
    public float ambientScaleMultiplier = 0.6f;
    public float ambientFloatMultiplier = 0.5f;

    [System.Serializable]
    public class BackgroundElement
    {
        public Transform target;
        public float moveAmplitude = 0.5f;
        public float moveSpeed = 0.4f;
        public float rotationAmplitude = 6f;
        public float rotationSpeed = 0.5f;
        public float scaleAmplitude = 0.12f;
        public float scaleSpeed = 0.6f;

        [HideInInspector] public Vector3 basePosition;
        [HideInInspector] public Vector3 baseScale;
        [HideInInspector] public float baseRotation;
        [HideInInspector] public float movePhase;
        [HideInInspector] public float rotPhase;
        [HideInInspector] public float scalePhase;
        [HideInInspector] public SpriteRenderer spriteRenderer;
    }

    [System.Serializable]
    public class BackgroundTheme
    {
        public string name;
        public Sprite[] elementSprites;
        public Color cameraColor = new Color(0.96f,0.92f,0.88f);
        public Color[] themePalette;
    }

    [Header("Large Abstract Elements (always moving)")]
    public BackgroundElement[] backgroundElements;

    [Header("Hat Themes (triggered when cat changes hat)")]
    public BackgroundTheme[] themes;
    public int startingThemeIndex = 0;
    public float themeTransitionDuration = 3f;
    public AnimationCurve themeTransitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Procedural Line Background")]
    public Transform lineParent;
    public int lineLayerCount = 2;
    public int linesPerLayer = 32;
    public float lineLengthMin = 6f;
    public float lineLengthMax = 22f;
    public float lineWidthMin = 0.03f;
    public float lineWidthMax = 0.18f;
    public float lineMoveAmplitude = 1.8f;
    public float lineMoveSpeedMin = 0.12f;
    public float lineMoveSpeedMax = 0.38f;
    public float lineAlphaMin = 0.25f;
    public float lineAlphaMax = 0.9f;

    int _currentThemeIndex = -1;
    bool _themeTransitionRunning;

    Color _bgLerpFrom, _bgLerpTo;
    float _bgLerpT;
    int _spawnedThisCall;
    float _nextAmbientTime;

    class ProceduralLine
    {
        public Vector3 center;
        public Vector2 dir;
        public float length;
        public float width;
        public float moveAmplitude;
        public float moveSpeed;
        public float phase;
        public int paletteIndex;
        public float alpha;
        public LineRenderer lr;
    }

    static Material _lineMaterial;
    List<ProceduralLine> _proceduralLines = new List<ProceduralLine>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (targetCamera == null) targetCamera = Camera.main;
    }

    void Start()
    {
        ScheduleNextAmbient();
        SetupBackgroundElements();

        if (themes != null && themes.Length > 0)
        {
            int idx = Mathf.Clamp(startingThemeIndex, 0, themes.Length - 1);
            InstantApplyTheme(idx);
        }

        CreateProceduralLines();
    }

    void Update()
    {
        HandleAmbient();
        UpdateBackgroundElementsMotion();
        UpdateProceduralLines();
    }

    public void OnNoteCollected()
    {
        LerpBackgroundColor();
        _spawnedThisCall = 0;
        PlayRandomPattern();
        if (_spawnedThisCall == 0)
        {
            Pattern_FallbackBurst();
        }
    }

    public void ApplyTheme(int themeIndex, float lerpTime)
    {
        if (themes == null || themes.Length == 0) return;
        themeIndex = Mathf.Clamp(themeIndex, 0, themes.Length - 1);
        if (themeIndex == _currentThemeIndex && _currentThemeIndex >= 0) return;

        float duration = themeTransitionDuration > 0f ? themeTransitionDuration : lerpTime;
        StopCoroutine(nameof(ThemeTransitionRoutine));
        StartCoroutine(ThemeTransitionRoutine(themeIndex, duration));
    }

    public void InstantApplyTheme(int themeIndex)
    {
        if (themes == null || themes.Length == 0) return;
        themeIndex = Mathf.Clamp(themeIndex, 0, themes.Length - 1);

        BackgroundTheme t = themes[themeIndex];
        _currentThemeIndex = themeIndex;

        if (backgroundElements != null && t.elementSprites != null)
        {
            int len = Mathf.Min(backgroundElements.Length, t.elementSprites.Length);
            for (int i = 0; i < len; i++)
            {
                if (backgroundElements[i] != null &&
                    backgroundElements[i].spriteRenderer != null &&
                    t.elementSprites[i] != null)
                {
                    backgroundElements[i].spriteRenderer.sprite = t.elementSprites[i];
                }
            }
        }

        if (targetCamera != null)
        {
            targetCamera.backgroundColor = t.cameraColor;
        }

        if (t.themePalette != null && t.themePalette.Length > 0)
        {
            palette = (Color[])t.themePalette.Clone();
        }

        UpdateProceduralLinesColors();
    }

    void HandleAmbient()
    {
        if (!enableAmbient) return;
        if (targetCamera == null) return;
        if (shapePrefabs == null || shapePrefabs.Length == 0) return;

        if (Time.time >= _nextAmbientTime)
        {
            PlayAmbientTick();
            ScheduleNextAmbient();
        }
    }

    void ScheduleNextAmbient()
    {
        float min = Mathf.Max(0.01f, ambientMinDelay);
        float max = Mathf.Max(min, ambientMaxDelay);
        _nextAmbientTime = Time.time + Random.Range(min, max);
    }

    void PlayAmbientTick()
    {
        Bounds b = ViewBoundsInset();
        int count = Mathf.Clamp(
            Random.Range(ambientMinBurst, ambientMaxBurst + 1),
            1,
            Mathf.Max(1, burstCount)
        );

        for (int i = 0; i < count; i++)
        {
            float x = Random.Range(b.min.x, b.max.x);
            float y = Random.Range(b.min.y, b.max.y);
            Vector3 pos = new Vector3(x, y, 0f);

            float baseScale = Random.Range(scaleRange.x, scaleRange.y);
            float s = Mathf.Max(
                minVisibleScale,
                baseScale * Mathf.Clamp01(ambientScaleMultiplier)
            );

            float baseFloat = Random.Range(extraYFloatRange.x, extraYFloatRange.y);
            float floatUp = baseFloat * Mathf.Clamp01(ambientFloatMultiplier);

            float rot = Random.Range(0f, 360f);
            float spin = Random.Range(-20f, 20f);

            SpawnShape(pos, rot, new Vector2(s, s), floatUp, spin);
        }
    }

    void LerpBackgroundColor()
    {
        if (targetCamera == null || palette == null || palette.Length == 0) return;

        _bgLerpFrom = targetCamera.backgroundColor;
        _bgLerpTo = palette[Random.Range(0, palette.Length)];
        _bgLerpT = 0f;
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
        float r = Mathf.Clamp(
            Random.Range(ringRadiusMin, ringRadiusMax),
            0.5f,
            Mathf.Min(b.extents.x, b.extents.y) - viewMargin
        );
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
            float r = t * Mathf.Min(
                spiralRadius,
                Mathf.Min(b.extents.x, b.extents.y) - viewMargin
            );
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

                Vector3 pos = ClampToBounds(
                    new Vector3(x + jitterX, y + jitterY, 0f),
                    b
                );
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
            SpawnShape(
                c,
                ang,
                new Vector2(s, s),
                Random.Range(0.3f, 0.8f),
                Random.Range(-50f, 50f)
            );
        }
    }

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

        position = ClampToBounds(position, ViewBoundsInset());

        GameObject go = Instantiate(
            prefab,
            position,
            Quaternion.Euler(0, 0, zRotationDeg),
            shapesParent
        );

        float sx = Mathf.Max(minVisibleScale, scaleXY.x);
        float sy = Mathf.Max(minVisibleScale, scaleXY.y);
        go.transform.localScale = new Vector3(sx, sy, 1f);

        var sr = go.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            Color baseCol = palette[Random.Range(0, palette.Length)];
            float v = Random.Range(0.8f, 1.25f);
            Color c = new Color(
                baseCol.r * v,
                baseCol.g * v,
                baseCol.b * v,
                forceOpaque ? 1f : sr.color.a
            );
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

        var fx = go.GetComponent<ShapeBurst>();
        if (fx == null) fx = go.AddComponent<ShapeBurst>();
        fx.life = shapeLifetime;
        fx.floatUp = floatUp;
        fx.spin = spin;

        _spawnedThisCall++;
    }

    void SetupBackgroundElements()
    {
        if (backgroundElements == null) return;

        foreach (var e in backgroundElements)
        {
            if (e == null || e.target == null) continue;

            e.basePosition = e.target.position;
            e.baseScale = e.target.localScale;
            e.baseRotation = e.target.rotation.eulerAngles.z;

            e.movePhase = Random.Range(0f, Mathf.PI * 2f);
            e.rotPhase = Random.Range(0f, Mathf.PI * 2f);
            e.scalePhase = Random.Range(0f, Mathf.PI * 2f);

            e.spriteRenderer = e.target.GetComponent<SpriteRenderer>();
        }
    }

    void UpdateBackgroundElementsMotion()
    {
        if (backgroundElements == null) return;

        float t = Time.time;
        float globalFlowX = Mathf.Sin(t * 0.05f) * 0.3f;
        float globalFlowY = Mathf.Cos(t * 0.03f) * 0.3f;

        foreach (var e in backgroundElements)
        {
            if (e == null || e.target == null) continue;

            Vector2 dir = new Vector2(
                Mathf.Sin(t * e.moveSpeed + e.movePhase),
                Mathf.Cos(t * e.moveSpeed * 0.7f + e.movePhase * 1.3f)
            );
            Vector3 offset = new Vector3(dir.x, dir.y, 0f) * e.moveAmplitude;
            offset.x += globalFlowX;
            offset.y += globalFlowY;

            float rotNoise = Mathf.Sin(t * e.rotationSpeed + e.rotPhase);
            float zRot = e.baseRotation + rotNoise * e.rotationAmplitude;

            float scaleNoise = Mathf.Sin(t * e.scaleSpeed + e.scalePhase);
            float scaleMul = 1f + scaleNoise * e.scaleAmplitude;
            if (scaleMul < 0.1f) scaleMul = 0.1f;

            e.target.position = e.basePosition + offset;
            e.target.rotation = Quaternion.Euler(0f, 0f, zRot);
            e.target.localScale = e.baseScale * scaleMul;
        }
    }

    void CreateProceduralLines()
    {
        _proceduralLines.Clear();
        if (targetCamera == null) return;

        if (_lineMaterial == null)
        {
            Shader s = Shader.Find("Sprites/Default");
            if (s != null)
            {
                _lineMaterial = new Material(s);
            }
        }

        Transform parent = lineParent;
        if (parent == null)
        {
            GameObject g = new GameObject("ProceduralLines");
            g.transform.SetParent(transform, false);
            parent = g.transform;
            lineParent = parent;
        }

        Bounds b = ViewBounds();
        b.Expand(new Vector3(6f, 6f, 0f));

        int layers = Mathf.Max(1, lineLayerCount);
        int perLayer = Mathf.Max(1, linesPerLayer);

        for (int layer = 0; layer < layers; layer++)
        {
            float baseAngle;
            float angleJitter;
            if (layer % 2 == 0)
            {
                baseAngle = 25f;
                angleJitter = 35f;
            }
            else
            {
                baseAngle = 110f;
                angleJitter = 45f;
            }

            for (int i = 0; i < perLayer; i++)
            {
                GameObject g = new GameObject("Line_" + layer + "_" + i);
                g.transform.SetParent(parent, false);
                LineRenderer lr = g.AddComponent<LineRenderer>();
                lr.positionCount = 2;
                lr.useWorldSpace = true;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;
                lr.textureMode = LineTextureMode.Stretch;
                lr.alignment = LineAlignment.TransformZ;
                if (_lineMaterial != null)
                {
                    lr.material = _lineMaterial;
                }

                float angleDeg = baseAngle + Random.Range(-angleJitter, angleJitter);
                float rad = angleDeg * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)).normalized;

                Vector3 center = new Vector3(
                    Random.Range(b.min.x, b.max.x),
                    Random.Range(b.min.y, b.max.y),
                    0f
                );

                float length = Random.Range(lineLengthMin, lineLengthMax);
                float width = Random.Range(lineWidthMin, lineWidthMax);

                float moveAmp = lineMoveAmplitude * Random.Range(0.4f, 1.2f);
                float speed = Random.Range(lineMoveSpeedMin, lineMoveSpeedMax);
                float phase = Random.Range(0f, Mathf.PI * 2f);

                int pIndex = palette != null && palette.Length > 0 ? Random.Range(0, palette.Length) : 0;
                float alpha = Random.Range(lineAlphaMin, lineAlphaMax);

                ProceduralLine pl = new ProceduralLine();
                pl.center = center;
                pl.dir = dir;
                pl.length = length;
                pl.width = width;
                pl.moveAmplitude = moveAmp;
                pl.moveSpeed = speed;
                pl.phase = phase;
                pl.paletteIndex = pIndex;
                pl.alpha = alpha;
                pl.lr = lr;

                _proceduralLines.Add(pl);
            }
        }

        UpdateProceduralLines();
    }

    void UpdateProceduralLines()
    {
        if (_proceduralLines == null || _proceduralLines.Count == 0) return;

        float t = Time.time;
        foreach (var line in _proceduralLines)
        {
            if (line.lr == null) continue;

            Vector2 normal = new Vector2(-line.dir.y, line.dir.x);
            float offset = Mathf.Sin(t * line.moveSpeed + line.phase) * line.moveAmplitude;
            Vector3 center = line.center + (Vector3)(normal * offset);

            Vector3 p0 = center - (Vector3)line.dir * line.length * 0.5f;
            Vector3 p1 = center + (Vector3)line.dir * line.length * 0.5f;

            line.lr.startWidth = line.width;
            line.lr.endWidth = line.width;
            line.lr.SetPosition(0, p0);
            line.lr.SetPosition(1, p1);
        }

        UpdateProceduralLinesColors();
    }

    void UpdateProceduralLinesColors()
    {
        if (_proceduralLines == null || _proceduralLines.Count == 0) return;
        if (palette == null || palette.Length == 0) return;

        foreach (var line in _proceduralLines)
        {
            if (line.lr == null) continue;
            int idx = Mathf.Abs(line.paletteIndex) % palette.Length;
            Color baseCol = palette[idx];
            baseCol.a = line.alpha;
            line.lr.startColor = baseCol;
            line.lr.endColor = baseCol;
        }
    }

    IEnumerator ThemeTransitionRoutine(int newIndex, float lerpTime)
    {
        if (themes == null || themes.Length == 0) yield break;

        lerpTime = Mathf.Max(0.0001f, lerpTime);
        _themeTransitionRunning = true;

        BackgroundTheme from = (_currentThemeIndex >= 0 && _currentThemeIndex < themes.Length)
            ? themes[_currentThemeIndex]
            : null;
        BackgroundTheme to = themes[newIndex];

        if (backgroundElements != null && to.elementSprites != null)
        {
            int len = Mathf.Min(backgroundElements.Length, to.elementSprites.Length);
            for (int i = 0; i < len; i++)
            {
                if (backgroundElements[i] != null &&
                    backgroundElements[i].spriteRenderer != null &&
                    to.elementSprites[i] != null)
                {
                    backgroundElements[i].spriteRenderer.sprite = to.elementSprites[i];
                }
            }
        }

        _currentThemeIndex = newIndex;

        Color camFrom = targetCamera != null ? targetCamera.backgroundColor : Color.black;
        Color camTo = to.cameraColor;

        Color[] paletteFrom = palette != null ? (Color[])palette.Clone() : null;
        Color[] paletteTo = to.themePalette != null && to.themePalette.Length > 0
            ? to.themePalette
            : paletteFrom;

        float time = 0f;
        while (time < lerpTime)
        {
            time += Time.deltaTime;
            float t = Mathf.Clamp01(time / lerpTime);
            float k = themeTransitionCurve != null ? themeTransitionCurve.Evaluate(t) : t;

            if (targetCamera != null)
            {
                targetCamera.backgroundColor = Color.Lerp(camFrom, camTo, k);
            }

            if (paletteFrom != null && paletteTo != null &&
                paletteFrom.Length > 0 && paletteTo.Length > 0)
            {
                int len = Mathf.Min(paletteFrom.Length, paletteTo.Length);
                Color[] mixed = new Color[len];
                for (int i = 0; i < len; i++)
                {
                    mixed[i] = Color.Lerp(paletteFrom[i], paletteTo[i], k);
                }
                palette = mixed;
            }

            UpdateProceduralLinesColors();
            yield return null;
        }

        if (targetCamera != null)
        {
            targetCamera.backgroundColor = camTo;
        }
        if (paletteTo != null && paletteTo.Length > 0)
        {
            palette = (Color[])paletteTo.Clone();
        }

        UpdateProceduralLinesColors();
        _themeTransitionRunning = false;
    }
}
