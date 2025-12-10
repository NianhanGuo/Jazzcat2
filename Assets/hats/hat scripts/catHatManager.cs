using System.Collections.Generic;
using UnityEngine;

public class CatHatManager : MonoBehaviour
{
    [Header("Hat Settings")]
    public Transform hatAnchorRight;      // hat position when cat faces right
    public Transform hatAnchorLeft;       // hat position when cat faces left
    public List<Sprite> hatSprites;       // hat library
    public int hatSortingOrderOffset = 1; // hat renders above cat

    private SpriteRenderer hatRenderer;
    private SpriteRenderer catSpriteRenderer;
    private List<int> remainingIndices = new List<int>();

    void Awake()
    {
        // get the cat's main SpriteRenderer (the one that flips)
        catSpriteRenderer = GetComponentInChildren<SpriteRenderer>();

        // choose an initial parent for the hat (prefer right, then left, then self)
        Transform initialParent = hatAnchorRight != null
            ? hatAnchorRight
            : (hatAnchorLeft != null ? hatAnchorLeft : transform);

        // create the hat object as a child of that parent
        GameObject hatObj = new GameObject("CatHatSprite");
        hatObj.transform.SetParent(initialParent, false);
        hatObj.transform.localPosition = Vector3.zero;

        hatRenderer = hatObj.AddComponent<SpriteRenderer>();
        hatRenderer.enabled = false;

        // match sorting layer / order and initial flip
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

        hatRenderer.sprite = chosen;
        hatRenderer.enabled = true;

        // NEW: register this hat with the gallery system
        HatGalleryUI.RegisterHat(hatIndex);

        // make sure position + facing are correct when the hat is first equipped
        UpdateAnchor();
        if (catSpriteRenderer != null)
            hatRenderer.flipX = catSpriteRenderer.flipX;
    }

    void LateUpdate()
    {
        UpdateAnchor();
    }

    void UpdateAnchor()
    {
        if (!hatRenderer.enabled) return;
        if (catSpriteRenderer == null) return;

        // which way is the cat facing?
        bool facingLeft = catSpriteRenderer.flipX;

        // pick target anchor based on facing direction (with fallback)
        Transform targetAnchor = null;
        if (facingLeft)
            targetAnchor = hatAnchorLeft != null ? hatAnchorLeft : hatAnchorRight;
        else
            targetAnchor = hatAnchorRight != null ? hatAnchorRight : hatAnchorLeft;

        if (targetAnchor == null) return;

        // parent hat under the correct anchor
        if (hatRenderer.transform.parent != targetAnchor)
        {
            hatRenderer.transform.SetParent(targetAnchor, false);
        }

        // snap to anchor origin
        hatRenderer.transform.localPosition = Vector3.zero;

        // mirror hat horizontally with the cat
        hatRenderer.flipX = catSpriteRenderer.flipX;
    }
}
