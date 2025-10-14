using UnityEngine;

public class FallingNote : MonoBehaviour
{
    public AudioClip collectSFX;   // optional pickup sound

    void OnTriggerEnter2D(Collider2D other)
    {
        // Only react to the player (make sure your cat GameObject is tagged "Player")
        if (!other.CompareTag("Player")) return;

        // 1) Increment the on-screen counter (unchanged behavior)
        if (NoteCounter.Instance != null)
            NoteCounter.Instance.AddOne();

        // 2) Trigger background color/shape burst (new line; no other logic changed)
        if (BackgroundArtManager.Instance != null)
            BackgroundArtManager.Instance.OnNoteCollected();

        // 3) Optional SFX (unchanged)
        if (collectSFX) AudioSource.PlayClipAtPoint(collectSFX, transform.position);

        // 4) Destroy the note (unchanged)
        Destroy(gameObject);
    }
}
