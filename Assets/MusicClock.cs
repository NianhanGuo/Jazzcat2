using UnityEngine;

public class MusicClock : MonoBehaviour
{
    [Range(40, 240)] public double bpm = 120;
    public int beatsPerBar = 4;

    private double secPerBeat;
    private double dspStart;

    void Awake()
    {
        Recalc();
    }

    void Start()
    {
        // Start a little in the future so we can schedule safely
        dspStart = AudioSettings.dspTime + 0.2;
    }

    void OnValidate()
    {
        Recalc();
    }

    private void Recalc()
    {
        // avoid division by zero
        secPerBeat = 60.0 / (bpm <= 0 ? 1.0 : bpm);
    }

    public double SecPerBeat { get { return secPerBeat; } }
    public double BarDuration { get { return secPerBeat * beatsPerBar; } }
    public double StartDspTime { get { return dspStart; } }

    // Absolute dspTime for the given beat index (0-based)
    public double TimeOfBeat(int index)
    {
        return dspStart + index * secPerBeat;
    }

    // Convenience: the next beat after "now"
    public double NextBeatTime()
    {
        double now = AudioSettings.dspTime;
        double beatsSinceStart = (now - dspStart) / secPerBeat;
        int nextIndex = Mathf.Max(0, Mathf.FloorToInt((float)beatsSinceStart) + 1);
        return TimeOfBeat(nextIndex);
    }

    // Quantize to the start of the next bar from an arbitrary time
    public double NextBarTime(double now)
    {
        double beats = (now - dspStart) / secPerBeat;
        int nextBarIndex = Mathf.Max(0, Mathf.CeilToInt((float)(beats / beatsPerBar)));
        return dspStart + nextBarIndex * BarDuration;
    }

    // For compatibility with your RidePlayer call
    public double GetNextBeatTime(int index)
    {
        return TimeOfBeat(index);
    }
    public double NextBarTimeFromNow(int barsAhead = 0)
{
    double now = AudioSettings.dspTime;
    double nextBar = NextBarTime(now);
    return nextBar + barsAhead * BarDuration;
}

}
