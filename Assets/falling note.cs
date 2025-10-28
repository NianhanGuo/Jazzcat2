using UnityEngine;

public class FallingNote : MonoBehaviour
{
    public AudioClip collectSFX;
    private bool consumed;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (consumed) return;

        Transform root = other.attachedRigidbody ? other.attachedRigidbody.transform : other.transform;

        bool isPlayer =
            (root != null && root.CompareTag("Player")) ||
            (root != null && root.GetComponentInParent<CatController2D>() != null);

        if (!isPlayer) return;
        consumed = true;

        // 计数
        if (NoteCounter.Instance != null)
            NoteCounter.Instance.AddOne();

        // 背景艺术效果
        if (BackgroundArtManager.Instance != null)
            BackgroundArtManager.Instance.OnNoteCollected();

        // —— 这里接入音乐系统（关键）——
        var tag = GetComponent<NoteSoundTag>();
        if (MusicLayerManager.Instance != null)
        {
            var inst = tag ? tag.instrument : NoteInstrument.RandomAny;
            MusicLayerManager.Instance.OnNoteCollected(inst);
        }

        // 可选 SFX
        if (collectSFX) AudioSource.PlayClipAtPoint(collectSFX, transform.position);

        // 销毁音符
        Destroy(gameObject);
    }
}
