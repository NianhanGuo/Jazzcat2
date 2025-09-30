using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class RidePlayer : MonoBehaviour
{
    public MusicClock clock;
    public AudioClip rideClip;          // 拖一个 ride/钹 的 wav 进来
    public VisualDirector visual;       // 可选：用来触发视觉脉冲

    AudioSource src;
    int beatIndex = 0;                  // 下一次要排程的拍序号
    const double lookAhead = 0.08;      // 80ms 安全提前量，避免丢拍

    void Awake()
    {
        src = GetComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 0f;          // 2D 声音
    }

    void Update()
    {
        if (rideClip == null || clock == null) return;

        double now = AudioSettings.dspTime;
        double nextBeatTime = clock.GetNextBeatTime(beatIndex);

        // 只要“现在 + 提前量”超过了下一个拍点，就把这个拍点安排出去
        if (now + lookAhead >= nextBeatTime)
        {
            ScheduleRide(nextBeatTime);
            beatIndex++; // 指向下一个拍
        }
    }

    void ScheduleRide(double dspTime)
    {
        src.clip = rideClip;
        src.PlayScheduled(dspTime);

        // 可选：把“将要发生的击打”告诉视觉，让它在同一 dspTime 触发
        if (visual != null) visual.ScheduleKickAt(dspTime);
    }
}
