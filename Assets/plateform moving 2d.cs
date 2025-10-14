using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
public class PlatformMover2D : MonoBehaviour
{
    [Tooltip("Downward speed (units/second). Spawner can override.")]
    public float speed = 1.8f;

    [Tooltip("How far below the camera bottom before auto-destroy.")]
    public float destroyMargin = 2f;

    Rigidbody2D rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;   // moving collider = kinematic
        rb.freezeRotation = true;
    }

    void OnEnable()
    {
        if (rb != null) rb.linearVelocity = Vector2.down * speed;
    }

    void FixedUpdate()
    {
        // keep velocity stable (in case something zeroes it)
        if (rb != null) rb.linearVelocity = Vector2.down * speed;
    }

    void Update()
    {
        var cam = Camera.main;
        if (!cam) return;

        float bottomY = cam.transform.position.y - cam.orthographicSize;
        if (transform.position.y < bottomY - destroyMargin)
        {
            Destroy(gameObject);
        }
    }

    public void SetSpeed(float newSpeed)
    {
        speed = newSpeed;
        if (rb != null) rb.linearVelocity = Vector2.down * speed;
    }
}
