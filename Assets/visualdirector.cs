using UnityEngine;
using System.Collections;

public class VisualDirector : MonoBehaviour
{
    public Camera cam;
    public SpriteRenderer bgSprite;   // drag your "pure red" here
    private float kickEnv;

    public void ScheduleKickAt(double dspTime)
    {
        StartCoroutine(FireAt(dspTime));
    }

    IEnumerator FireAt(double targetDsp)
    {
        while (AudioSettings.dspTime < targetDsp) yield return null;
        kickEnv = 1f;
    }

    void Update()
    {
        kickEnv = Mathf.Max(0, kickEnv - Time.deltaTime * 2f);

        if (bgSprite != null)
        {
            // fade between dark red and bright red
            Color baseCol = new Color(0.2f, 0, 0);
            Color pulseCol = new Color(1f, 0.2f, 0.2f);
            bgSprite.color = Color.Lerp(baseCol, pulseCol, kickEnv);
        }
        else if (cam != null)
        {
            cam.backgroundColor = Color.Lerp(Color.black, Color.red, kickEnv);
        }
    }
}
