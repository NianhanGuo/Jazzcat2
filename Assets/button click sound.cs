using UnityEngine;
using UnityEngine.UI;

public class ButtonClickSound : MonoBehaviour
{
    public AudioSource audioSource;
    public AudioClip clickSound;

    void Start()
    {
        Button[] buttons = FindObjectsOfType<Button>();

        foreach (var btn in buttons)
        {
            btn.onClick.AddListener(() =>
            {
                if (audioSource && clickSound)
                    audioSource.PlayOneShot(clickSound);
            });
        }
    }
}
