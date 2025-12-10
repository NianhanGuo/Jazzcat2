using UnityEngine;

public class FallingHat : MonoBehaviour
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

        CatHatManager hatManager = root.GetComponentInChildren<CatHatManager>();
        if (hatManager == null) return;

        consumed = true;

        // 给猫戴随机帽子（优先没用过的）
        hatManager.EquipRandomHat();

        // 可选 SFX
        if (collectSFX)
            AudioSource.PlayClipAtPoint(collectSFX, transform.position);

        // 销毁掉落的问号帽子图标
        Destroy(gameObject);
    }
}
