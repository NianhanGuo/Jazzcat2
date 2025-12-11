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

    AudioSource introSource;
    bool introStopped;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        PlayIntroLead();
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

    void PlayIntroLead()
    {
        if (libraries == null || libraries.Length == 0) return;

        List<int> libCandidates = new List<int>();
        for (int i = 0; i < libraries.Length; i++)
        {
            if (LibraryHasLead(i)) libCandidates.Add(i);
        }
        if (libCandidates.Count == 0) return;

        int libIndex = libCandidates[rng.Next(0, libCandidates.Count)];
        var set = libraries[libIndex];
        if (set == null || set.variants == null || set.variants.Length == 0) return;

        List<int> varCandidates = new List<int>();
        for (int i = 0; i < set.variants.Length; i++)
        {
            var c = set.variants[i];
            if (!c) continue;
            if (!set.GetIsLead(i)) continue;
            varCandidates.Add(i);
        }
        if (varCandidates.Count == 0) return;

        int varIndex = varCandidates[rng.Next(0, varCandidates.Count)];
        var clip = set.variants[varIndex];
        if (!clip) return;

        var go = new GameObject("IntroLead");
        go.transform.SetParent(transform, false);

        var src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.loop = true;
        src.playOnAwake = false;
        src.outputAudioMixerGroup = outputGroup;
        src.volume = 0f;
        src.spatialBlend = 0f;

        introSource = src;

        double startAt = conductor ? conductor.PeekNextBar() : AudioSettings.dspTime + 0.1;
        startAt += startPhaseJitter * Rand(0f, 1f);
        src.PlayScheduled(startAt);
        if (conductor) conductor.ConsumeNextBarAndAdvance();

        float targetVol = Mathf.Clamp01(baseVolume + Rand(-volumeJitter, volumeJitter));
        StartCoroutine(FadeTo(src, 0f, targetVol, fadeInTime));
    }

    IEnumerator FadeOutIntroLead()
    {
        if (introSource == null) yield break;
        yield return FadeTo(introSource, introSource.volume, 0f, fadeOutTime);
        Destroy(introSource.gameObject);
        introSource = null;
    }

    public void OnNoteCollected(NoteInstrument requested)
    {
        if (!introStopped && introSource != null)
        {
            introStopped = true;
            StartCoroutine(FadeOutIntroLead());
        }

        int libIndex = (requested == NoteInstrument.RandomAny)
            ? WeightedPickLibrary(false, false)
            : (int)requested;

        int leadCount = CountActiveLeads();
        int nonLeadCount = CountActiveNonLeads();

        if (leadCount == 0 && nonLeadCount == 0)
        {
            bool ok = TryCreateNewLayer(libIndex, false, true);
            if (!ok)
            {
                int alt = WeightedPickLibrary(false, false);
                TryCreateNewLayer(alt, false, true);
            }
            return;
        }

        if (leadCount == 1 && nonLeadCount == 0)
        {
            bool ok = TryCreateNewLayer(libIndex, true, false);
            if (!ok)
            {
                int alt = WeightedPickLibrary(true, true);
                TryCreateNewLayer(alt, true, false);
            }
            return;
        }

        if (leadCount == 0 && nonLeadCount >= 1)
        {
            bool ok = TryCreateNewLayer(libIndex, false, true);
            if (!ok)
            {
                int alt = WeightedPickLibrary(false, false);
                TryCreateNewLayer(alt, false, true);
            }
            return;
        }

        if (layers.Count == 0)
            return;

        ActiveLayer kicked = layers[rng.Next(0, layers.Count)];
        bool kickedIsLead = kicked.isLead;
        StartCoroutine(FadeOutAndRemove(kicked));

        if (kickedIsLead)
        {
            bool ok = TryCreateNewLayer(libIndex, false, true);
            if (!ok)
            {
                int alt = WeightedPickLibrary(false, false);
                TryCreateNewLayer(alt, false, true);
            }
        }
        else
        {
            bool ok = TryCreateNewLayer(libIndex, true, false);
            if (!ok)
            {
                int alt = WeightedPickLibrary(true, true);
                TryCreateNewLayer(alt, true, false);
            }
        }
    }

    void EnforceLayerLimits()
    {
        int leadCount = 0;
        List<ActiveLayer> nonLeads = new List<ActiveLayer>();
        foreach (var l in layers)
        {
            if (l.isLead) leadCount++;
            else nonLeads.Add(l);
        }

        if (leadCount > 1)
        {
            for (int i = leadCount - 1; i >= 1; i--)
            {
                var layer = layers.FindLast(l => l.isLead);
                if (layer != null) StartCoroutine(FadeOutAndRemove(layer));
            }
        }

        if (nonLeads.Count > 1)
        {
            int removeCount = nonLeads.Count - 1;
            for (int i = 0; i < removeCount; i++)
            {
                var layer = nonLeads[rng.Next(0, nonLeads.Count)];
                nonLeads.Remove(layer);
                StartCoroutine(FadeOutAndRemove(layer));
            }
        }
    }

    bool TryCreateNewLayer(int libIndex, bool requireNonLead, bool requireLead = false)
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
                if (requireLead && !isLead) continue;
                candidates.Add(i);
            }
        }
        if (candidates.Count == 0) return false;

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

        int semi = 0;
        src.pitch = 1f;

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

    int CountActiveNonLeads()
    {
        int c = 0;
        foreach (var l in layers) if (!l.isLead) c++;
        return c;
    }

    void EvolveLayer(ActiveLayer layer, bool keepLeadType = false)
    {
        if (layer == null || layer.set == null || layer.set.variants == null || layer.set.variants.Length == 0)
            return;

        bool changeVariant = false;
        bool retune = false;

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
            int newSemi = 0;
            layer.semitone = newSemi;
            layer.src.pitch = 1f;
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
