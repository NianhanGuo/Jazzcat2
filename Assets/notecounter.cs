using UnityEngine;
using TMPro;  // Use TextMeshPro

[DisallowMultipleComponent]
public class NoteCounter : MonoBehaviour
{
    public static NoteCounter Instance { get; private set; }

    [Header("UI")]
    public TextMeshProUGUI counterText;   // Drag your Text (TMP) here
    public string label = "Notes: ";

    public int Count { get; private set; }
    bool frozen; // true after death (stop counting)

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        ResetCounter();  // start at 0 when scene loads
        UpdateText();
    }

    public void AddOne()
    {
        if (frozen) return;
        Count++;
        UpdateText();
    }

    // Called by GameOverUI.Show()
    public void Freeze(bool value)
    {
        frozen = value;
    }

    // Called on scene reload (fresh start)
    public void ResetCounter()
    {
        frozen = false;
        Count = 0;
        UpdateText();
    }

    void UpdateText()
    {
        if (counterText != null)
            counterText.text = label + Count.ToString();
    }
}
