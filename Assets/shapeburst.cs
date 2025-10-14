using UnityEngine;

public class ShapeBurst : MonoBehaviour
{
    public float life = 1.6f;     // 存活时间
    public float floatUp = 0.8f;  // 总上升位移
    public float spin = 60f;      // 每秒旋转角速度

    float _t;
    Vector3 _startPos;
    SpriteRenderer _sr;
    Color _startColor;

    void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
        _startPos = transform.position;
        if (_sr != null) _startColor = _sr.color;
    }

    void Update()
    {
        _t += Time.deltaTime;
        float u = Mathf.Clamp01(_t / Mathf.Max(0.0001f, life));

        // 上升 & 旋转
        transform.position = _startPos + new Vector3(0f, floatUp * u, 0f);
        transform.Rotate(0f, 0f, spin * Time.deltaTime);

        // 淡出
        if (_sr != null)
        {
            Color c = _startColor;
            c.a = 1f - u;
            _sr.color = c;
        }

        if (_t >= life) Destroy(gameObject);
    }
}
