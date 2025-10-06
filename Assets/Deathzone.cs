using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class DeathZone2D : MonoBehaviour
{
    public string playerTag = "Player";
    public GameOverUI gameOverUI;

    void Reset()
    {
        // 自动把碰撞体设置为 Trigger
        var col = GetComponent<Collider2D>();
        col.isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;

        // 可选：只在“从上往下坠落”时判定
        var rb = other.attachedRigidbody;
        if (rb != null && rb.linearVelocity.y > 0f) return;

        // 关闭玩家控制（可选）
        if (rb != null) rb.simulated = false;

        if (gameOverUI != null) gameOverUI.Show();
        else Debug.LogWarning("DeathZone2D: 未绑定 GameOverUI 引用。");
    }
}
