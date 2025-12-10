using System.Collections.Generic;
using UnityEngine;

public class CatHatManager : MonoBehaviour
{
    [Header("Hat Settings")]
    public Transform hatAnchorRight;
    public Transform hatAnchorLeft;
    public List<Sprite> hatSprites;
    public int hatSortingOrderOffset = 1;

    [Header("Per-Hat Position Offsets")]
    public List<Vector2> hatPositionOffsets;

    private SpriteRenderer hatRenderer;
    private SpriteRenderer catSpriteRenderer;
    private List<int> remainingIndices = new List<int>();

    private int currentHatIndex = -1;
    private Vector2 currentHatOffset = Vector2.zero;

    void Awake()
    {
        catSpriteRenderer = GetComponentInChildren<SpriteRenderer>();

        Transform initialParent = hatAnchorRight != null
            ? hatAnchorRight
            : (hatAnchorLeft != null ? hatAnchorLeft : transform);

        GameObject hatObj = new GameObject("CatHatSprite");
        hatObj.transform.SetParent(initialParent, false);
        hatObj.transform.localPosition = Vector3.zero;

        hatRenderer = hatObj.AddComponent<SpriteRenderer>();
        hatRenderer.enabled = false;

        if (catSpriteRenderer != null)
        {
            hatRenderer.sortingLayerID = catSpriteRenderer.sortingLayerID;
            hatRenderer.sortingOrder = catSpriteRenderer.sortingOrder + hatSortingOrderOffset;
            hatRenderer.flipX = catSpriteRenderer.flipX;
        }

        ResetHatPool();
    }

    void ResetHatPool()
    {
        remainingIndices.Clear();
        if (hatSprites == null) return;

        for (int i = 0; i < hatSprites.Count; i++)
        {
            if (hatSprites[i] != null)
                remainingIndices.Add(i);
        }
    }

    public void EquipRandomHat()
    {
        if (hatSprites == null || hatSprites.Count == 0)
            return;

        if (remainingIndices.Count == 0)
            ResetHatPool();

        if (remainingIndices.Count == 0)
            return;

        int rIndex = Random.Range(0, remainingIndices.Count);
        int hatIndex = remainingIndices[rIndex];
        remainingIndices.RemoveAt(rIndex);

        Sprite chosen = hatSprites[hatIndex];
        if (chosen == null) return;

        currentHatIndex = hatIndex;

        currentHatOffset = Vector2.zero;
        if (hatPositionOffsets != null &&
            hatIndex >= 0 &&
            hatIndex < hatPositionOffsets.Count)
        {
            currentHatOffset = hatPositionOffsets[hatIndex];
        }

        hatRenderer.sprite = chosen;
        hatRenderer.enabled = true;

        HatGalleryUI.RegisterHat(hatIndex);

        UpdateAnchor();
        if (catSpriteRenderer != null)
            hatRenderer.flipX = catSpriteRenderer.flipX;

        if (BackgroundArtManager.Instance != null)
        {
            BackgroundArtManager.Instance.ApplyTheme(hatIndex, 3f);
        }
    }

    void LateUpdate()
    {
        UpdateAnchor();
    }

    void UpdateAnchor()
    {
        if (!hatRenderer.enabled) return;
        if (catSpriteRenderer == null) return;

        bool facingLeft = catSpriteRenderer.flipX;

        Transform targetAnchor = null;
        if (facingLeft)
            targetAnchor = hatAnchorLeft != null ? hatAnchorLeft : hatAnchorRight;
        else
            targetAnchor = hatAnchorRight != null ? hatAnchorRight : hatAnchorLeft;

        if (targetAnchor == null) return;

        if (hatRenderer.transform.parent != targetAnchor)
        {
            hatRenderer.transform.SetParent(targetAnchor, false);
        }

        Vector3 localPos = Vector3.zero;
        localPos.x += currentHatOffset.x;
        localPos.y += currentHatOffset.y;
        hatRenderer.transform.localPosition = localPos;

        hatRenderer.flipX = catSpriteRenderer.flipX;
    }
}
