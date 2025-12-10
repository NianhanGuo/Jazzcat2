using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class HatGalleryUI : MonoBehaviour
{
    public static HatGalleryUI Instance { get; private set; }

    [Header("References")]
    public CatHatManager catHatManager;   // drag cat placeholder here
    public Transform galleryParent;       // Gallery parent (RectTransform)
    public GameObject hatSlotPrefab;      // Hatslot prefab (with HatSlotUI)

    [Header("Texts")]
    public TextMeshProUGUI hatCountText;       // "Hats: X/19"
    public TextMeshProUGUI allHatsMessageText; // "You collected all hats, congrats!"
    public TextMeshProUGUI notesCountText;     // "Notes caught: X"

    bool[] collected;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (catHatManager != null && catHatManager.hatSprites != null)
        {
            collected = new bool[catHatManager.hatSprites.Count];
        }
    }

    // Called from CatHatManager when a hat is equipped
    public static void RegisterHat(int hatIndex)
    {
        if (Instance == null || Instance.collected == null)
            return;

        if (hatIndex < 0 || hatIndex >= Instance.collected.Length)
            return;

        Instance.collected[hatIndex] = true;
    }

    // Runs when game end panel is activated
    void OnEnable()
    {
        RefreshUI();
    }

    void RefreshUI()
    {
        if (catHatManager == null || catHatManager.hatSprites == null || collected == null)
            return;

        // clear old children
        if (galleryParent != null)
        {
            for (int i = galleryParent.childCount - 1; i >= 0; i--)
            {
                Destroy(galleryParent.GetChild(i).gameObject);
            }
        }

        int totalHats = catHatManager.hatSprites.Count;
        int collectedCount = 0;

        for (int i = 0; i < totalHats; i++)
        {
            if (!collected[i]) continue;

            collectedCount++;

            if (galleryParent != null && hatSlotPrefab != null)
            {
                GameObject slotObj = Instantiate(hatSlotPrefab, galleryParent);
                HatSlotUI slotUI = slotObj.GetComponent<HatSlotUI>();

                if (slotUI != null && slotUI.hatIcon != null)
                {
                    slotUI.hatIcon.sprite = catHatManager.hatSprites[i];
                    slotUI.hatIcon.enabled = true;
                }
            }
        }

        // Hats: X / N
        if (hatCountText != null)
            hatCountText.text = $"Hats: {collectedCount}/{totalHats}";

        // Congrats if all
        if (allHatsMessageText != null)
        {
            if (collectedCount == totalHats)
                allHatsMessageText.text = "You collected all hats, congrats!";
            else
                allHatsMessageText.text = "";
        }

        // Notes caught
        if (notesCountText != null)
        {
            if (NoteCounter.Instance != null)
                notesCountText.text = "Notes caught: " + NoteCounter.Instance.Count;
            else
                notesCountText.text = "Notes caught: 0";
        }
    }
}
