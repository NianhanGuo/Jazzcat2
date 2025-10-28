using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public class MusicLayerManager : MonoBehaviour
{
    public static MusicLayerManager Instance { get; private set; }

    [Header("Routing")]
    public AudioMixerGroup outputGroup;

    [Header("Clip Libraries (index 对应 NoteInstrument)")]
    public MusicClipBank[] libraries = new MusicClipBank[6]; // 和你的 NoteInstrument 顺序一致

    [Header("RandomAny 权重（长度需与 libraries 一致）")]
    public float[] randomAnyWeights = new float[] { 4, 3, 2, 2, 1, 1 };

    [Header("Lead（主旋律）控制")]
    [Tooltip("把被视为主旋律的库索引填进来（例如 2=Guitar, 5=Piano）")]
    public int[] leadLibraryIndices = new int[] { };
    [Tooltip("同一时间最多允许多少条主旋律")]
    public int maxLeadLayers = 1;

    [Header("Limits & Fades")]
    public int maxLayers = 8;
    public float fadeInTime = 0.8f;
    public float fadeOutTime = 1.0f;

    [Header("Generative Tweaks")]
    [Tooltip("从这个半音集合里挑（量化后更好听）")]
    public int[] transposePool = new int[] { -2, -1, 0, 1, 2 };
    [Range(0f, 1f)]
    public float nonZeroTransposeProbability = 0.7f;
    public float startPhaseJitter = 0.02f; // 秒
    public float baseVolume = 0.7f;
    public float volumeJitter = 0.08f;

    [Header("Bar Sync")]
    public MusicConductor conductor;
    [Tooltip("每隔 N 小节自动让一条层演化（0=关闭）")]
    public int autoEvolveEveryNBars = 4;

    class ActiveLayer
    {
        public AudioSource src;
        public int libIndex;
        public MusicClipBank set;
        public int variantIndex;
        public int semitone;
        public float targetVolume;
        public bool isLead;
    }

    readonly List<ActiveLayer> layers = new List<ActiveLayer>();
    System.Random rng = new System.Random();
    HashSet<int> leadIndexSet = new HashSet<int>();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        // 缓存 lead 索引集合
        leadIndexSet.Clear();
        if (leadLibraryIndices != null)
            foreach (var i in leadLibraryIndices) leadIndexSet.Add(i);

        if (conductor != null && autoEvolveEveryNBars > 0)
            StartCoroutine(AutoEvolveLoop());
    }

    IEnumerator AutoEvolveLoop()
    {
        // 定期（按小节）对一条现有层做微小演化
        while (true)
        {
            double bars = autoEvolveEveryNBars;
            double waitSec = (conductor != null) ? bars * conductor.BarDuration : bars * (60.0 / 100.0 * 4.0);
            yield return new WaitForSeconds((float)waitSec);

            if (layers.Count == 0) continue;
            var target = layers[rng.Next(0, layers.Count)];
            EvolveLayer(target);
        }
    }

    public void OnNoteCollected(NoteInstrument requested)
    {
        int libIndex = (requested == NoteInstrument.RandomAny)
            ? WeightedPickLibrary()
            : (int)requested;

        // Lead 限流：若抽到的是 lead 且已达上限，则改抽非 lead
        if (IsLeadLibrary(libIndex) && CountActiveLeads() >= maxLeadLayers)
        {
            libIndex = WeightedPickLibrary(nonLeadOnly: true);
        }

        // 是否已有同类库的层？已有则演化；否则新建
        ActiveLayer existing = layers.Find(l => l.libIndex == libIndex);
        if (existing != null) { EvolveLayer(existing); return; }

        // 满载则随机踢掉一条（优先踢非 lead，尽量保留主干）
        if (layers.Count >= maxLayers)
        {
            int idx = PickKickIndex();
            StartCoroutine(FadeOutAndRemove(layers[idx]));
        }

        CreateNewLayer(libIndex);
    }

    int PickKickIndex()
    {
        // 尝试优先踢非 lead；如果全是非 lead/lead 比例不满足，则随机
        var nonLeads = new List<int>();
        for (int i = 0; i < layers.Count; i++)
            if (!layers[i].isLead) nonLeads.Add(i);

        if (nonLeads.Count > 0)
            return nonLeads[rng.Next(0, nonLeads.Count)];

        return rng.Next(0, layers.Count);
    }

    int WeightedPickLibrary(bool nonLeadOnly = false)
    {
        // 修正权重数组长度
        float[] w = (randomAnyWeights != null && randomAnyWeights.Length == libraries.Length)
            ? randomAnyWeights
            : BuildOnes(libraries.Length);

        // 如果只允许非 lead，则把 lead 的权重清零
        float total = 0f;
        float[] acc = new float[w.Length];
        for (int i = 0; i < w.Length; i++)
        {
            float wi = w[i];
            if (nonLeadOnly && IsLeadLibrary(i)) wi = 0f;
            if (!HasClips(i)) wi = 0f;

            total += wi;
            acc[i] = total;
        }

        if (total <= 0f)
        {
            // 兜底：找第一个可用的
            for (int i = 0; i < libraries.Length; i++)
                if (HasClips(i)) return i;
            return 0;
        }

        double r = rng.NextDouble() * total;
        for (int i = 0; i < acc.Length; i++)
            if (r <= acc[i]) return i;

        return 0;
    }

    bool HasClips(int libIndex)
    {
        if (libIndex < 0 || libIndex >= libraries.Length) return false;
        var set = libraries[libIndex];
        return set != null && set.variants != null && set.variants.Length > 0;
    }

    bool IsLeadLibrary(int libIndex) => leadIndexSet.Contains(libIndex);

    int CountActiveLeads()
    {
        int c = 0;
        foreach (var l in layers) if (l.isLead) c++;
        return c;
    }

    void CreateNewLayer(int libIndex)
    {
        if (!HasClips(libIndex)) return;
        var set = libraries[libIndex];

        int varIndex = rng.Next(0, set.variants.Length);
        var clip = set.variants[varIndex];
        if (!clip) return;

        var go = new GameObject($"Layer_{libIndex}_{varIndex}");
        go.transform.SetParent(transform, false);

        var src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.loop = true;
        src.playOnAwake = false;
        src.outputAudioMixerGroup = outputGroup;
        src.volume = 0f;
        src.spatialBlend = 0f;

        int semi = PickTransposeSemitone(excludeZero: rng.NextDouble() < nonZeroTransposeProbability);
        src.pitch = Mathf.Pow(2f, semi / 12f);

        float targetVol = Mathf.Clamp01(baseVolume + Rand(-volumeJitter, volumeJitter));

        var layer = new ActiveLayer
        {
            src = src,
            libIndex = libIndex,
            set = set,
            variantIndex = varIndex,
            semitone = semi,
            targetVolume = targetVol,
            isLead = IsLeadLibrary(libIndex)
        };
        layers.Add(layer);

        double startAt = conductor ? conductor.PeekNextBar() : AudioSettings.dspTime + 0.1;
        startAt += startPhaseJitter * Rand(0f, 1f);
        src.PlayScheduled(startAt);
        if (conductor) conductor.ConsumeNextBarAndAdvance();

        StartCoroutine(FadeTo(src, 0f, targetVol, fadeInTime));
    }

    void EvolveLayer(ActiveLayer layer)
    {
        if (layer == null || layer.set == null || layer.set.variants == null || layer.set.variants.Length == 0)
            return;

        // 60% 概率换变体（与当前不同）
        bool changeVariant = rng.NextDouble() < 0.6;
        // 90% 概率重新挑移调（且尽量不同于当前）
        bool retune = rng.NextDouble() < 0.9;

        if (changeVariant && layer.set.variants.Length > 1)
        {
            int next = rng.Next(0, layer.set.variants.Length);
            if (next == layer.variantIndex && layer.set.variants.Length > 1)
                next = (next + 1) % layer.set.variants.Length;

            var newClip = layer.set.variants[next];
            if (newClip)
            {
                double switchAt = conductor ? conductor.PeekNextBar() : AudioSettings.dspTime + 0.05;
                layer.src.SetScheduledEndTime(switchAt);
                layer.src.clip = newClip;
                layer.src.PlayScheduled(switchAt);
                if (conductor) conductor.ConsumeNextBarAndAdvance();
                layer.variantIndex = next;
            }
        }

        if (retune)
        {
            int newSemi = layer.semitone;
            // 尽量不同
            for (int tries = 0; tries < 4; tries++)
            {
                int cand = PickTransposeSemitone(excludeZero: rng.NextDouble() < nonZeroTransposeProbability);
                if (cand != layer.semitone) { newSemi = cand; break; }
            }
            layer.semitone = newSemi;
            layer.src.pitch = Mathf.Pow(2f, newSemi / 12f);
        }

        float newTarget = Mathf.Clamp01(baseVolume + Rand(-volumeJitter, volumeJitter));
        layer.targetVolume = newTarget;
        StartCoroutine(FadeTo(layer.src, layer.src.volume, newTarget, 0.4f));
    }

    int PickTransposeSemitone(bool excludeZero)
    {
        if (transposePool == null || transposePool.Length == 0)
            transposePool = new int[] { -2, -1, 0, 1, 2 };

        // 如果要求“尽量非 0”，就先过滤 0
        List<int> pool = new List<int>(transposePool);
        if (excludeZero && pool.Contains(0) && pool.Count > 1) pool.Remove(0);

        int pick = pool[rng.Next(0, pool.Count)];
        return pick;
    }

    IEnumerator FadeOutAndRemove(ActiveLayer layer)
    {
        yield return FadeTo(layer.src, layer.src.volume, 0f, fadeOutTime);
        layers.Remove(layer);
        Destroy(layer.src.gameObject);
    }

    IEnumerator FadeTo(AudioSource src, float from, float to, float t)
    {
        float time = 0f;
        while (time < t)
        {
            time += Time.deltaTime;
            float k = t > 0f ? time / t : 1f;
            src.volume = Mathf.Lerp(from, to, k);
            yield return null;
        }
        src.volume = to;
    }

    float Rand(float a, float b) => (float)(a + rng.NextDouble() * (b - a));

    float[] BuildOnes(int n)
    {
        var arr = new float[n];
        for (int i = 0; i < n; i++) arr[i] = 1f;
        return arr;
    }
}
