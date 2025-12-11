using UnityEngine;

public class FishItem : MonoBehaviour
{
    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponent<CatController2D>())
        {
            FindObjectOfType<PowerupSystem>().TriggerFish();
            Destroy(gameObject);
        }
    }
}
