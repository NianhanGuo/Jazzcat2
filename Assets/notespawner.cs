using UnityEngine;

public class NoteSpawner : MonoBehaviour
{
    [Header("Spawner Settings")]
    public GameObject notePrefab;      // assign your Note prefab here
    public float spawnInterval = 4f;   // time between spawns
    public float xRange = 8f;          // horizontal spread of spawns

    float timer;

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= spawnInterval)
        {
            timer = 0f;

            // pick random x within range
            Vector3 spawnPos = new Vector3(
                Random.Range(-xRange, xRange),
                transform.position.y,
                0f
            );

            Instantiate(notePrefab, spawnPos, Quaternion.identity);
        }
    }
}
