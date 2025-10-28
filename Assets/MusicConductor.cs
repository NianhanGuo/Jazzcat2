using UnityEngine;

public class MusicConductor : MonoBehaviour
{
    [Header("Tempo")]
    public double bpm = 100.0;
    public int beatsPerBar = 4;

    public double SecondsPerBeat => 60.0 / bpm;
    public double BarDuration => SecondsPerBeat * beatsPerBar;

    public double nextBarDspTime { get; private set; }

    void Start()
    {
        double dsp = AudioSettings.dspTime;
        nextBarDspTime = dsp + 0.2;
    }

    public double ConsumeNextBarAndAdvance()
    {
        double t = nextBarDspTime;
        nextBarDspTime += BarDuration;
        return t;
    }

    public double PeekNextBar()
    {
        return nextBarDspTime;
    }
}
