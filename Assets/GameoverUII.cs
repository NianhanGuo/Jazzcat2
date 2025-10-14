using UnityEngine;
using UnityEngine.SceneManagement;

public class GameOverUI : MonoBehaviour
{
    [Header("Assign in Inspector")]
    public GameObject gameOverPanel; // your GameOverPanel container

    bool shown;

    void Awake()
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        Time.timeScale = 1f; // normal speed on scene load

        // start counter fresh on load (safe even if NoteCounter is not present)
        if (NoteCounter.Instance != null)
            NoteCounter.Instance.ResetCounter();
    }

    public void Show()
    {
        if (shown) return;
        shown = true;

        if (gameOverPanel != null) gameOverPanel.SetActive(true);

        // stop gameplay & freeze the counter (no more increments)
        if (NoteCounter.Instance != null)
            NoteCounter.Instance.Freeze(true);

        Time.timeScale = 0f; // pause game while UI is up
    }

    // YES button
    public void OnClickYes()
    {
        Time.timeScale = 1f;
        var idx = SceneManager.GetActiveScene().buildIndex;
        SceneManager.LoadScene(idx); // NoteCounter resets in Awake
    }

    // NO button
    public void OnClickNo()
    {
        Time.timeScale = 1f;
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
