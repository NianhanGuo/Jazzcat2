using UnityEngine;
using System.Collections;

[RequireComponent(typeof(AudioSource))]
public class SnareBackbeat : MonoBehaviour
{
    public MusicClock clock;          // drag in your MusicClock (system)
    public VisualDirector visual;     // drag Main Camera (VisualDirector)
    public AudioClip snare;           // assign a snare/clap WAV

    public int barsAhead = 2;         // schedule how far in advance
    public double lookAhead = 0.08;   // 80 ms

    private AudioSource src;
    private double scheduledUntil;
    private bool ready = false;

    void Awake()
    {
        src = GetComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 0f; // 2D
    }

    void Start()
    {
        StartCoroutine(InitWhenClockReady());
    }

    IEnumerator InitWhenClockReady()
    {
        while (clock == null || clock.StartDspTime <= 0.0001)
            yield return null;

        double firstBar = clock.NextBarTimeFromNow(0);
        if (firstBar <= AudioSettings.dspTime + 0.02)
            firstBar = clock.NextBarTimeFromNow(1);

        scheduledUntil = firstBar;
        ready = true;
        Debug.Log($"[SnareBackbeat] Ready. firstBar={firstBar:F3}");
    }

    void Update()
    {
        if (!ready || snare == null || clock == null) return;

        double now = AudioSettings.dspTime;
        double target = now + barsAhead * clock.BarDuration;

        while (scheduledUntil + lookAhead < target)
        {
            ScheduleBar(scheduledUntil);
            scheduledUntil += clock.BarDuration;
        }
    }

    void ScheduleBar(double barStart)
    {
        double spb = clock.SecPerBeat;

        // Beat 2
        double beat2 = barStart + spb * 1;
        Schedule(snare, beat2);

        // Beat 4
        double beat4 = barStart + spb * 3;
        Schedule(snare, beat4);
    }

    void Schedule(AudioClip clip, double t)
    {
        if (!clip) return;
        src.clip = clip;
        src.PlayScheduled(t);
        Debug.Log($"[SnareBackbeat] scheduled @ {t:F3}");

        if (visual != null) visual.ScheduleKickAt(t);
    }
}
