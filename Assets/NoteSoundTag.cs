using UnityEngine;

public enum NoteInstrument
{
    RandomAny = -1,
    Drums = 0,
    Bass = 1,
    Guitar = 2,
    Piano = 3,
    Organ = 4,
    Pac = 5
}

public class NoteSoundTag : MonoBehaviour
{
    public NoteInstrument instrument = NoteInstrument.RandomAny;
}
