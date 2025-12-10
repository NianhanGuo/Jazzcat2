using UnityEngine;

public class HatSpawner : MonoBehaviour
{
    [Header("Spawner Settings")]
    public GameObject hatPickupPrefab;   // 掉下来的问号帽子预制体
    public float spawnInterval = 15f;    // 每 15 秒
    public float xRange = 8f;            // 水平方向随机范围

    private float timer;

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= spawnInterval)
        {
            timer = 0f;

            Vector3 spawnPos = new Vector3(
                Random.Range(-xRange, xRange),
                transform.position.y,
                0f
            );

            Instantiate(hatPickupPrefab, spawnPos, Quaternion.identity);
        }
    }
}
