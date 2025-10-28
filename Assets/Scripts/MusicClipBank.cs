using UnityEngine;

[CreateAssetMenu(fileName = "MusicClipBank", menuName = "Audio/Music Clip Bank")]
public class MusicClipBank : ScriptableObject
{
    public string displayName = "Instrument";
    public AudioClip[] variants;
    public bool[] isLeadFlags;

    public bool HasLead()
    {
        if (variants == null || variants.Length == 0) return false;
        if (isLeadFlags == null || isLeadFlags.Length != variants.Length) return false;
        for (int i = 0; i < isLeadFlags.Length; i++) if (isLeadFlags[i]) return true;
        return false;
    }

    public bool HasNonLead()
    {
        if (variants == null || variants.Length == 0) return false;
        if (isLeadFlags == null || isLeadFlags.Length != variants.Length) return true;
        for (int i = 0; i < variants.Length; i++)
        {
            bool flag = i < isLeadFlags.Length ? isLeadFlags[i] : false;
            if (!flag) return true;
        }
        return false;
    }

    public bool GetIsLead(int variantIndex)
    {
        if (isLeadFlags == null || variantIndex < 0 || variantIndex >= isLeadFlags.Length) return false;
        return isLeadFlags[variantIndex];
    }
}
