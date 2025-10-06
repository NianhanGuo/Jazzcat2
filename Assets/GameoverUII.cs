using UnityEngine;
using UnityEngine.SceneManagement;

public class GameOverUI : MonoBehaviour
{
    [Header("Assign in Inspector")]
    public GameObject gameOverPanel; // 指到 GameOverPanel

    bool shown;

    void Awake()
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        Time.timeScale = 1f; // 确保进入场景时是正常速度
    }

    public void Show()
    {
        if (shown) return;
        shown = true;
        if (gameOverPanel != null) gameOverPanel.SetActive(true);
        Time.timeScale = 0f; // 暂停游戏（UI 仍然响应）
    }

    // 绑定到 Yes 按钮
    public void OnClickYes()
    {
        Time.timeScale = 1f;
        var idx = SceneManager.GetActiveScene().buildIndex;
        SceneManager.LoadScene(idx); // 重新加载当前场景
    }

    // 绑定到 No 按钮
    public void OnClickNo()
    {
        Time.timeScale = 1f;
        // 打包后的退出
        Application.Quit();

        // 在编辑器里停止 Play 模式
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
