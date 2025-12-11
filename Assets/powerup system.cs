using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class PowerupSystem : MonoBehaviour
{
    public GameObject poisonPrefab;
    public GameObject fishPrefab;
    public float spawnInterval = 5f;
    public Rect spawnArea;
    public Image greenFilter;
    public CatController2D cat;

    float originalMoveSpeed;
    bool inverted;
    Coroutine poisonCoroutine;
    Coroutine speedCoroutine;
    GameObject currentPoison;
    GameObject currentFish;

    void Start()
    {
        if (cat != null) originalMoveSpeed = cat.moveSpeed;
        if (greenFilter != null) greenFilter.gameObject.SetActive(false);
        StartCoroutine(SpawnLoop());
    }

    IEnumerator SpawnLoop()
    {
        while (true)
        {
            if (poisonPrefab != null && currentPoison == null)
            {
                currentPoison = Instantiate(poisonPrefab, RandomPoint(), Quaternion.identity);
            }
            if (fishPrefab != null && currentFish == null)
            {
                currentFish = Instantiate(fishPrefab, RandomPoint(), Quaternion.identity);
            }
            yield return new WaitForSeconds(spawnInterval);
        }
    }

    Vector3 RandomPoint()
    {
        float x = Random.Range(spawnArea.xMin, spawnArea.xMax);
        float y = Random.Range(spawnArea.yMin, spawnArea.yMax);
        return new Vector3(x, y, 0f);
    }

    public void TriggerPoison()
    {
        if (poisonCoroutine != null) StopCoroutine(poisonCoroutine);
        poisonCoroutine = StartCoroutine(PoisonEffect());
    }

    public void TriggerFish()
    {
        if (speedCoroutine != null) StopCoroutine(speedCoroutine);
        speedCoroutine = StartCoroutine(SpeedEffect());
    }

    IEnumerator PoisonEffect()
    {
        inverted = true;
        float time = 10f;
        float flashStart = 3f;
        if (greenFilter != null) greenFilter.gameObject.SetActive(true);

        while (time > flashStart)
        {
            time -= Time.deltaTime;
            yield return null;
        }

        float remaining = flashStart;
        bool on = true;
        float interval = 0.2f;

        while (remaining > 0f)
        {
            on = !on;
            if (greenFilter != null) greenFilter.gameObject.SetActive(on);
            remaining -= interval;
            yield return new WaitForSeconds(interval);
        }

        if (greenFilter != null) greenFilter.gameObject.SetActive(false);
        inverted = false;
    }

    IEnumerator SpeedEffect()
    {
        float speedMult = 1.7f;
        float buffDuration = 5f;
        float fadeDuration = 1.5f;

        if (cat != null)
        {
            cat.moveSpeed = originalMoveSpeed * speedMult;
        }

        yield return new WaitForSeconds(buffDuration);

        float t = 0f;
        float startSpeed = cat != null ? cat.moveSpeed : 0f;

        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            if (cat != null)
            {
                float k = t / fadeDuration;
                cat.moveSpeed = Mathf.Lerp(startSpeed, originalMoveSpeed, k);
            }
            yield return null;
        }

        if (cat != null) cat.moveSpeed = originalMoveSpeed;
    }

    public float ProcessInput(float input)
    {
        if (inverted) return -input;
        return input;
    }
}
