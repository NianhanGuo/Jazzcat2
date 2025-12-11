using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class GameInstructionUI : MonoBehaviour
{
    public GameObject instructionPanel;
    public GameObject[] slides;
    public Button continueButton;
    public Button startGameButton;
    public float animationDuration = 0.3f;

    static bool hasShown;
    int currentIndex;

    void Start()
    {
        if (continueButton != null) continueButton.onClick.AddListener(OnClickContinue);
        if (startGameButton != null) startGameButton.onClick.AddListener(OnClickStartGame);

        if (hasShown)
        {
            if (instructionPanel != null) instructionPanel.SetActive(false);
            Time.timeScale = 1f;
        }
        else
        {
            hasShown = true;
            if (instructionPanel != null) instructionPanel.SetActive(true);
            Time.timeScale = 0f;
            currentIndex = 0;
            ShowCurrentSlide();
        }
    }

    void ShowCurrentSlide()
    {
        if (slides == null || slides.Length == 0) return;

        for (int i = 0; i < slides.Length; i++)
        {
            if (slides[i] != null) slides[i].SetActive(i == currentIndex);
        }

        if (continueButton != null) continueButton.gameObject.SetActive(currentIndex < slides.Length - 1);
        if (startGameButton != null) startGameButton.gameObject.SetActive(currentIndex == slides.Length - 1);

        StopAllCoroutines();
        if (slides[currentIndex] != null) StartCoroutine(AnimateSlide(slides[currentIndex].transform));
    }

    IEnumerator AnimateSlide(Transform slideTransform)
    {
        Vector3 startScale = Vector3.zero;
        Vector3 endScale = Vector3.one;
        float t = 0f;
        slideTransform.localScale = startScale;

        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / animationDuration;
            slideTransform.localScale = Vector3.Lerp(startScale, endScale, t);
            yield return null;
        }

        slideTransform.localScale = endScale;
    }

    void OnClickContinue()
    {
        if (slides == null || slides.Length == 0) return;
        if (currentIndex < slides.Length - 1)
        {
            currentIndex++;
            ShowCurrentSlide();
        }
    }

    void OnClickStartGame()
    {
        Time.timeScale = 1f;
        if (instructionPanel != null) instructionPanel.SetActive(false);
    }
}
