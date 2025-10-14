using UnityEngine;

public class FallingNote : MonoBehaviour
{
    public AudioClip collectSFX;   // optional
    private bool consumed;         // 防止重复触发

    void OnTriggerEnter2D(Collider2D other)
    {
        if (consumed) return;

        // 优先取这个碰撞体所属的 Rigidbody 根对象
        Transform root = other.attachedRigidbody ? other.attachedRigidbody.transform : other.transform;

        
        bool isPlayer =
            (root != null && root.CompareTag("Player")) ||
            (root != null && root.GetComponentInParent<CatController2D>() != null);

        if (!isPlayer) return;
        consumed = true;

        // 1) 计数 +1（保持原功能）
        if (NoteCounter.Instance != null)
            NoteCounter.Instance.AddOne();

        // 2) 触发背景艺术效果（保持原功能）
        if (BackgroundArtManager.Instance != null)
            BackgroundArtManager.Instance.OnNoteCollected();

        // 3) 可选音效（保持原功能）
        if (collectSFX) AudioSource.PlayClipAtPoint(collectSFX, transform.position);

        // 4) 销毁音符（保持原功能）
        Destroy(gameObject);
    }
}
