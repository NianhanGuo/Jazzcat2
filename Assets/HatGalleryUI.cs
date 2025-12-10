using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class HatGalleryUI : MonoBehaviour
{
    public static HatGalleryUI Instance { get; private set; }

    [Header("References")]
    public CatHatManager catHatManager;   // drag your cat here
    public Transform galleryParent;       // parent object for hat icons
    public GameObject hatSlotPrefab;      // UI prefab with an Image component

    [Header("Texts")]
    public TextMeshProUGUI hatCountText;       // "Hats: X / 19"
    public TextMeshProUGUI allHatsMessageText; // "You got all hats!"
    public TextMeshProUGUI notesCountText;     // "Notes caught: X"

    bool[] collected;   // which hats have been collected

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

    // Called by CatHatManager when a hat is picked
    public static void RegisterHat(int hatIndex)
    {
        if (Instance == null || Instance.collected == null)
            return;

        if (hatIndex < 0 || hatIndex >= Instance.collected.Length)
            return;

        Instance.collected[hatIndex] = true;
    }

    // This runs when the GameOver panel turns ON (SetActive(true))
    void OnEnable()
    {
        RefreshUI();
    }

    void RefreshUI()
    {
        if (catHatManager == null || catHatManager.hatSprites == null || collected == null)
            return;

        // --- clear old gallery items ---
        if (galleryParent != null)
        {
            for (int i = galleryParent.childCount - 1; i >= 0; i--)
            {
                Destroy(galleryParent.GetChild(i).gameObject);
            }
        }

        int totalHats = catHatManager.hatSprites.Count;
        int collectedCount = 0;

        // --- create an icon for each collected hat ---
        for (int i = 0; i < totalHats; i++)
        {
            if (!collected[i]) continue;

            collectedCount++;

            if (galleryParent != null && hatSlotPrefab != null)
            {
                GameObject slot = Instantiate(hatSlotPrefab, galleryParent);
                Image img = slot.GetComponentInChildren<Image>();
                if (img != null)
                    img.sprite = catHatManager.hatSprites[i];
            }
        }

        // --- text: hats X / 19 ---
        if (hatCountText != null)
            hatCountText.text = $"Hats: {collectedCount}/{totalHats}";

        // --- text: congrats if all ---
        if (allHatsMessageText != null)
        {
            if (collectedCount == totalHats)
                allHatsMessageText.text = "You collected all hats, congrats!";
            else
                allHatsMessageText.text = "";
        }

        // --- text: how many notes caught ---
        if (notesCountText != null && NoteCounter.Instance != null)
        {
            notesCountText.text = "Notes caught: " + NoteCounter.Instance.Count.ToString();
        }
    }
}
