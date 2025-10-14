using UnityEngine;

public class PlatformSpawner2D : MonoBehaviour
{
    [Header("Prefab & Timing")]
    public GameObject platformPrefab;                 // assign Platform.prefab
    public Vector2 spawnEverySeconds = new Vector2(0.9f, 1.6f);

    [Header("Platform Size (world units)")]
    public Vector2 widthRange = new Vector2(2.5f, 6f); // min/max width
    public float height = 0.5f;                        // platform thickness

    [Header("Movement")]
    public float downSpeed = 1.8f;                     // base fall speed
    public float speedRampPerMinute = 0.6f;            // difficulty ramp

    [Header("Spawn Placement")]
    public float topPadding = 0.8f;    // how far above the top edge to spawn
    public float sidePadding = 0.4f;   // keep a little gap from screen edges

    [Header("Anti-Overlap (no vertical alignment)")]
    [Tooltip("Minimum X distance from the last platform so it’s not straight above.")]
    public float minHorizontalSeparation = 2.5f;
    [Tooltip("Extra separation scaled by the new platform width (helps when it’s very wide).")]
    public float separationPerWidth = 0.35f;
    [Tooltip("How many random tries before we force a far-side position.")]
    public int maxPositionTries = 6;

    float _timer;
    float _nextInterval;
    bool _hasLastX;
    float _lastSpawnX; // in world units

    void Start()
    {
        ScheduleNext();
    }

    void Update()
    {
        _timer += Time.deltaTime;
        if (_timer >= _nextInterval)
        {
            _timer = 0f;
            ScheduleNext();
            SpawnOne();
        }
    }

    void ScheduleNext()
    {
        _nextInterval = Random.Range(spawnEverySeconds.x, spawnEverySeconds.y);
    }

    void SpawnOne()
    {
        var cam = Camera.main;
        if (!cam || !platformPrefab) return;

        // Camera bounds
        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;
        Vector3 c = cam.transform.position;

        // Choose size
        float w = Mathf.Clamp(Random.Range(widthRange.x, widthRange.y), 0.5f, halfW * 1.9f);
        float h = Mathf.Max(0.1f, height);

        // Allowed X taking into account platform width and side padding
        float usableHalfW = Mathf.Max(0.1f, halfW - sidePadding - w * 0.5f);

        // Required separation this spawn
        float requiredSep = Mathf.Max(minHorizontalSeparation, w * separationPerWidth);

        // Find an X that’s separated from last X
        float x = 0f;
        bool placed = false;

        for (int i = 0; i < maxPositionTries; i++)
        {
            float candidate = Random.Range(-usableHalfW, usableHalfW);

            if (!_hasLastX || Mathf.Abs(candidate - _lastSpawnX) >= requiredSep)
            {
                x = candidate;
                placed = true;
                break;
            }
        }

        if (!placed)
        {
            // Force a far-side position opposite from last X
            float side = (_lastSpawnX >= 0f) ? -1f : 1f;
            x = Mathf.Clamp(_lastSpawnX + side * requiredSep, -usableHalfW, usableHalfW);
        }

        // Y just above the visible top
        float y = halfH + topPadding;
        Vector3 spawnPos = new Vector3(c.x + x, c.y + y, 0f);

        // Instantiate
        GameObject go = Instantiate(platformPrefab, spawnPos, Quaternion.identity);
        go.transform.localScale = new Vector3(w, h, 1f);

        // Speed with ramp
        float ramp = 1f + (Time.timeSinceLevelLoad / 60f) * speedRampPerMinute;
        var mover = go.GetComponent<PlatformMover2D>();
        if (mover) mover.SetSpeed(downSpeed * ramp);

        // Remember last X
        _lastSpawnX = x;
        _hasLastX = true;
    }
}
