using UnityEngine;

public class FallingNote : MonoBehaviour
{
    public float fallSpeed = 2f;   // if you want manual movement instead of Rigidbody
    public AudioClip collectSFX;   // optional: sound when picked

    void Update()
    {
        // if not using gravity, uncomment this:
        // transform.Translate(Vector2.down * fallSpeed * Time.deltaTime);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player")) // make sure cat has tag "Player"
        {
            // TODO: call your music/art manager here
            Debug.Log("Note collected!");

            if (collectSFX) AudioSource.PlayClipAtPoint(collectSFX, transform.position);

            Destroy(gameObject);
        }
    }
}