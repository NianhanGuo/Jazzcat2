using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public class MusicLayerManager : MonoBehaviour
{
    public static MusicLayerManager Instance { get; private set; }

    public AudioMixerGroup outputGroup;
    public MusicClipBank[] libraries = new MusicClipBank[6];
    public float[] randomAnyWeights = new float[] { 4, 3, 2, 2, 1, 1 };

    public int maxLeadLayers = 1;
    public int maxLayers = 8;
    public float fadeInTime = 0.8f;
    public float fadeOutTime = 1.0f;

    public int[] transposePool = new int[] { -2, -1, 0, 1, 2 };
    [Range(0f, 1f)]
    public float nonZeroTransposeProbability = 0.7f;
    public float startPhaseJitter = 0.02f;
    public float baseVolume = 0.7f;
    public float volumeJitter = 0.08f;

    public MusicConductor conductor;
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

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        if (conductor != null && autoEvolveEveryNBars > 0)
            StartCoroutine(AutoEvolveLoop());
    }

    IEnumerator AutoEvolveLoop()
    {
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
            ? WeightedPickLibrary(false, false)
            : (int)requested;

        bool needNonLead = CountActiveLeads() >= maxLeadLayers;
        if (!TryCreateNewLayer(libIndex, needNonLead))
        {
            if (needNonLead)
            {
                int alt = WeightedPickLibrary(true, true);
                TryCreateNewLayer(alt, true);
            }
            else
            {
                int alt = WeightedPickLibrary(false, false);
                TryCreateNewLayer(alt, false);
            }
        }
    }

    bool TryCreateNewLayer(int libIndex, bool requireNonLead)
    {
        if (!HasClips(libIndex)) return false;
        var set = libraries[libIndex];

        List<int> candidates = new List<int>();
        if (set.variants != null)
        {
            for (int i = 0; i < set.variants.Length; i++)
            {
                var c = set.variants[i];
                if (!c) continue;
                bool isLead = set.GetIsLead(i);
                if (requireNonLead && isLead) continue;
                candidates.Add(i);
            }
        }
        if (candidates.Count == 0) return false;

        ActiveLayer existing = layers.Find(l => l.libIndex == libIndex && l.isLead == set.GetIsLead(l.variantIndex));
        if (existing != null)
        {
            EvolveLayer(existing, keepLeadType: true);
            return true;
        }

        if (layers.Count >= maxLayers)
        {
            int idx = PickKickIndex();
            StartCoroutine(FadeOutAndRemove(layers[idx]));
        }

        int varIndex = candidates[rng.Next(0, candidates.Count)];
        var clip = set.variants[varIndex];
        var go = new GameObject($"Layer_{libIndex}_{varIndex}");
        go.transform.SetParent(transform, false);

        var src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.loop = true;
        src.playOnAwake = false;
        src.outputAudioMixerGroup = outputGroup;
        src.volume = 0f;
        src.spatialBlend = 0f;

        int semi = PickTransposeSemitone(rng.NextDouble() < nonZeroTransposeProbability);
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
            isLead = set.GetIsLead(varIndex)
        };
        layers.Add(layer);

        double startAt = conductor ? conductor.PeekNextBar() : AudioSettings.dspTime + 0.1;
        startAt += startPhaseJitter * Rand(0f, 1f);
        src.PlayScheduled(startAt);
        if (conductor) conductor.ConsumeNextBarAndAdvance();

        StartCoroutine(FadeTo(src, 0f, targetVol, fadeInTime));
        return true;
    }

    int PickKickIndex()
    {
        var nonLeads = new List<int>();
        for (int i = 0; i < layers.Count; i++)
            if (!layers[i].isLead) nonLeads.Add(i);

        if (nonLeads.Count > 0)
            return nonLeads[rng.Next(0, nonLeads.Count)];

        return rng.Next(0, layers.Count);
    }

    int WeightedPickLibrary(bool requireNonLead, bool preferNonLeadLibraries)
    {
        float[] w = (randomAnyWeights != null && randomAnyWeights.Length == libraries.Length)
            ? randomAnyWeights
            : BuildOnes(libraries.Length);

        float total = 0f;
        float[] acc = new float[w.Length];
        for (int i = 0; i < w.Length; i++)
        {
            float wi = w[i];
            if (!HasClips(i)) wi = 0f;
            if (requireNonLead && !LibraryHasNonLead(i)) wi = 0f;
            if (preferNonLeadLibraries && LibraryHasNonLead(i) && LibraryHasLead(i)) wi *= 1.1f;

            total += wi;
            acc[i] = total;
        }

        if (total <= 0f)
        {
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

    bool LibraryHasNonLead(int libIndex)
    {
        if (!HasClips(libIndex)) return false;
        return libraries[libIndex].HasNonLead();
    }

    bool LibraryHasLead(int libIndex)
    {
        if (!HasClips(libIndex)) return false;
        return libraries[libIndex].HasLead();
    }

    int CountActiveLeads()
    {
        int c = 0;
        foreach (var l in layers) if (l.isLead) c++;
        return c;
    }

    void EvolveLayer(ActiveLayer layer, bool keepLeadType = false)
    {
        if (layer == null || layer.set == null || layer.set.variants == null || layer.set.variants.Length == 0)
            return;

        bool changeVariant = rng.NextDouble() < 0.6;
        bool retune = rng.NextDouble() < 0.9;

        if (changeVariant && layer.set.variants.Length > 1)
        {
            List<int> pool = new List<int>();
            for (int i = 0; i < layer.set.variants.Length; i++)
            {
                var c = layer.set.variants[i];
                if (!c) continue;
                if (keepLeadType)
                {
                    bool isLead = layer.set.GetIsLead(i);
                    if (isLead != layer.isLead) continue;
                }
                if (i == layer.variantIndex && layer.set.variants.Length > 1) continue;
                pool.Add(i);
            }
            if (pool.Count > 0)
            {
                int next = pool[rng.Next(0, pool.Count)];
                var newClip = layer.set.variants[next];
                double switchAt = conductor ? conductor.PeekNextBar() : AudioSettings.dspTime + 0.05;
                layer.src.SetScheduledEndTime(switchAt);
                layer.src.clip = newClip;
                layer.src.PlayScheduled(switchAt);
                if (conductor) conductor.ConsumeNextBarAndAdvance();
                layer.variantIndex = next;
                layer.isLead = layer.set.GetIsLead(next);
            }
        }

        if (retune)
        {
            int newSemi = layer.semitone;
            for (int tries = 0; tries < 4; tries++)
            {
                int cand = PickTransposeSemitone(rng.NextDouble() < nonZeroTransposeProbability);
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
    float[] BuildOnes(int n) { var arr = new float[n]; for (int i = 0; i < n; i++) arr[i] = 1f; return arr; }
}
