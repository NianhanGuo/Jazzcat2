using UnityEngine;

public class PoisonItem : MonoBehaviour
{
    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponent<CatController2D>())
        {
            FindObjectOfType<PowerupSystem>().TriggerPoison();
            Destroy(gameObject);
        }
    }
}
