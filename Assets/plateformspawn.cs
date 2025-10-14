using UnityEngine;

public class PlatformSpawner2D : MonoBehaviour
{
    [Header("Prefab & Timing")]
    public GameObject platformPrefab;
    public Vector2 spawnEverySeconds = new Vector2(0.9f, 1.6f);

    [Header("Platform Size (world units)")]
    public Vector2 widthRange = new Vector2(2.5f, 6f);
    public float height = 0.5f;

    [Header("Movement")]
    public float downSpeed = 1.8f;
    public float speedRampPerMinute = 0.6f;

    [Header("Spawn Placement")]
    public float topPadding = 0.8f;
    public float sidePadding = 0.4f;

    [Header("Anti-Alignment")]
    public float minHorizontalSeparation = 2.5f;
    public float separationPerWidth = 0.35f;
    public int maxPositionTries = 6;

    [Header("Type Weights (sum doesn’t need to be 1)")]
    public float wNormal = 6f;
    public float wCracking = 3f;
    public float wBoost = 2f;
    public float wSticky = 2f;
    public float wTrap = 1f;

    float timer, nextInterval;
    bool hasLastX;
    float lastX;

    void Start() => ScheduleNext();

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= nextInterval)
        {
            timer = 0f;
            ScheduleNext();
            SpawnOne();
        }
    }

    void ScheduleNext()
    {
        nextInterval = Random.Range(spawnEverySeconds.x, spawnEverySeconds.y);
    }

    void SpawnOne()
    {
        var cam = Camera.main;
        if (!cam || !platformPrefab) return;

        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;
        Vector3 c = cam.transform.position;

        float w = Mathf.Clamp(Random.Range(widthRange.x, widthRange.y), 0.5f, halfW * 1.9f);
        float h = Mathf.Max(0.1f, height);

        float usableHalfW = Mathf.Max(0.1f, halfW - sidePadding - w * 0.5f);
        float requiredSep = Mathf.Max(minHorizontalSeparation, w * separationPerWidth);

        float x = 0f;
        bool placed = false;
        for (int i = 0; i < maxPositionTries; i++)
        {
            float candidate = Random.Range(-usableHalfW, usableHalfW);
            if (!hasLastX || Mathf.Abs(candidate - lastX) >= requiredSep)
            {
                x = candidate; placed = true; break;
            }
        }
        if (!placed)
        {
            float side = (lastX >= 0f) ? -1f : 1f;
            x = Mathf.Clamp(lastX + side * requiredSep, -usableHalfW, usableHalfW);
        }

        float y = halfH + topPadding;
        Vector3 pos = new Vector3(c.x + x, c.y + y, 0f);
        GameObject go = Instantiate(platformPrefab, pos, Quaternion.identity);
        go.transform.localScale = new Vector3(w, h, 1f);

        // set downward speed (with ramp)
        float ramp = 1f + (Time.timeSinceLevelLoad / 60f) * speedRampPerMinute;
        var mover = go.GetComponent<PlatformMover2D>();
        if (mover) mover.SetSpeed(downSpeed * ramp);

        // assign a random type + color
        var behavior = go.GetComponent<PlatformBehavior2D>();
        if (behavior != null)
        {
            behavior.type = PickType();
            behavior.ApplyTypeColor();
        }

        lastX = x; hasLastX = true;
    }

    PlatformBehavior2D.PlatformType PickType()
    {
        float total =
            Mathf.Max(0, wNormal) +
            Mathf.Max(0, wCracking) +
            Mathf.Max(0, wBoost) +
            Mathf.Max(0, wSticky) +
            Mathf.Max(0, wTrap);

        float r = Random.value * total;

        if ((r -= Mathf.Max(0, wNormal))  <= 0f) return PlatformBehavior2D.PlatformType.Normal;
        if ((r -= Mathf.Max(0, wCracking))<= 0f) return PlatformBehavior2D.PlatformType.Cracking;
        if ((r -= Mathf.Max(0, wBoost))   <= 0f) return PlatformBehavior2D.PlatformType.Boost;
        if ((r -= Mathf.Max(0, wSticky))  <= 0f) return PlatformBehavior2D.PlatformType.Sticky;
        return PlatformBehavior2D.PlatformType.Trap;
    }
}
