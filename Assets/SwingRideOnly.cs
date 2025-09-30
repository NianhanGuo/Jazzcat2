using UnityEngine;
using System.Collections;

[RequireComponent(typeof(AudioSource))]
public class SwingRideOnly : MonoBehaviour
{
    public MusicClock clock;          // assign: system (MusicClock)
    public VisualDirector visual;     // assign: Main Camera (VisualDirector)
    public AudioClip ride;            // assign: your ride WAV

    [Range(0.50f, 0.70f)] public double swing = 0.62;  // 0.66 = classic swing
    public int barsAhead = 2;                           // schedule buffer bars
    public double lookAhead = 0.08;                    // 80 ms safety

    private AudioSource src;
    private double scheduledUntil;                     // dspTime scheduled up to
    private bool ready = false;

    void Awake()
    {
        src = GetComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 0f; // 2D
    }

    void Start()
    {
        // Wait until MusicClock has a valid StartDspTime, then start at NEXT bar
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
        Debug.Log($"[SwingRideOnly] Ready. firstBar={firstBar:F3}, now={AudioSettings.dspTime:F3}");
    }

    void Update()
    {
        // Manual sanity check: press P to play the ride immediately (no scheduling).
        if (Input.GetKeyDown(KeyCode.P) && ride != null)
        {
            src.PlayOneShot(ride);
            Debug.Log("[SwingRideOnly] Manual OneShot (P) fired.");
        }

        if (!ready || ride == null || clock == null) return;

        double now = AudioSettings.dspTime;
        double target = now + barsAhead * clock.BarDuration;

        while (scheduledUntil + lookAhead < target)
        {
            ScheduleBar(scheduledUntil);
            scheduledUntil += clock.BarDuration;
        }
    }

    // one 4/4 bar: ride at 1, 2&, 3, 4  (swung 8ths indices: 0,3,4,6)
    void ScheduleBar(double barStart)
    {
        double spb = clock.SecPerBeat;

        System.Func<int, double> E = (e8) =>
        {
            bool off = (e8 % 2 == 1);
            double longPart  = swing * (spb / 2.0);
            double shortPart = (1.0 - swing) * (spb / 2.0);
            int pair = e8 / 2;
            return barStart + pair * (longPart + shortPart) + (off ? longPart : 0.0);
        };

        Schedule(ride, E(0));  // 1
        Schedule(ride, E(3));  // 2 &
        Schedule(ride, E(4));  // 3
        Schedule(ride, E(6));  // 4
    }

    void Schedule(AudioClip clip, double t)
    {
        if (!clip) return;
        src.clip = clip;
        src.PlayScheduled(t);
        Debug.Log($"[SwingRideOnly] Ride scheduled @ {t:F3}, now={AudioSettings.dspTime:F3}");

        if (visual != null) visual.ScheduleKickAt(t);
    }
}
