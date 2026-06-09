using System;
using UnityEngine;

// Right-click in Project window → Create → Mousebus → Cutscene Data
// Make one asset per cutscene, e.g: "Level_01_June_Intro", "Level_01_June_Outro"
// Store them in Assets/_Mousebus/Data/Cutscenes/
[CreateAssetMenu(fileName = "CutsceneData", menuName = "Mousebus/Cutscene Data")]
public class CutsceneData : ScriptableObject
{
    public CutsceneSlide[] slides;

    [Header("Audio")]
    public AudioClip music;
    [Range(0f, 1f)] public float musicVolume = 1f;
}

[Serializable]
public class CutsceneSlide
{
    public Sprite image;

    [Tooltip("How long this slide stays on screen in seconds")]
    public float duration = 4f;

    public SubtitleLine[] subtitles;
}

[Serializable]
public class SubtitleLine
{
    [TextArea(2, 4)]
    public string text;

    [Tooltip("Seconds after this slide starts when this line appears")]
    public float showAtTime;
}
