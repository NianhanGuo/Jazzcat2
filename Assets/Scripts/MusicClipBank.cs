using UnityEngine;

[CreateAssetMenu(fileName = "MusicClipBank", menuName = "Audio/Music Clip Bank")]
public class MusicClipBank : ScriptableObject
{
    public string displayName = "Instrument";
    public AudioClip[] variants;
}
